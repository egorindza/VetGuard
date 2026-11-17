using System.Data;
using Dapper;
using FluentValidation;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using VetGuard.Application.Interfaces;
using VetGuard.Application.Models;
using VetGuard.Domain.Entities;
using VetGuard.Domain.Enums;
using VetGuard.Domain.Exceptions;
using VetGuard.Infrastructure.Persistence;

namespace VetGuard.Infrastructure.Services;

public sealed class EpizooticService : IEpizooticService
{
    private readonly VetGuardDbContext _db;
    private readonly IValidator<ExecuteGroupEpizooticActCommand> _actValidator;

    public EpizooticService(
        VetGuardDbContext db,
        IValidator<ExecuteGroupEpizooticActCommand> actValidator)
    {
        _db = db;
        _actValidator = actValidator;
    }

    public async Task<IReadOnlyList<EpizooticRulePlanDto>> GetEpizooticCalendarPlanAsync(CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;
        var rules = await _db.EpizooticRules
            .AsNoTracking()
            .Include(r => r.DefaultVetDrug)
            .OrderBy(r => r.DiseaseType)
            .ToListAsync(ct);

        var animals = await _db.Animals
            .AsNoTracking()
            .Include(a => a.EpizooticItems)
            .ThenInclude(i => i.EpizooticAct)
            .Where(a => !a.IsHistoricalPedigreeOnly && a.HealthStatus != HealthStatus.Culled)
            .OrderBy(a => a.TagNumber)
            .ToListAsync(ct);

        var result = new List<EpizooticRulePlanDto>();

        foreach (var rule in rules)
        {
            var actionAnimals = new List<EpizooticDueAnimalDto>();
            var compliantCount = 0;

            foreach (var animal in animals)
            {
                var ageDays = animal.GetAgeDays(nowUtc);
                if (ageDays < rule.MinAgeDays)
                {
                    continue;
                }

                var lastExecution = animal.EpizooticItems
                    .Where(i => i.EpizooticAct != null && i.EpizooticAct.EpizooticRuleId == rule.Id)
                    .Select(i => (DateTime?)i.EpizooticAct!.ExecutedAtUtc)
                    .Max();

                var dueAt = lastExecution.HasValue
                    ? lastExecution.Value.AddDays(rule.IntervalDays)
                    : nowUtc.AddDays(-5);

                var daysUntilDue = (int)Math.Floor((dueAt.Date - nowUtc.Date).TotalDays);
                EpizooticComplianceState state;

                if (daysUntilDue < 0)
                {
                    state = EpizooticComplianceState.Overdue;
                }
                else if (daysUntilDue <= 14)
                {
                    state = EpizooticComplianceState.DueSoon;
                }
                else
                {
                    state = EpizooticComplianceState.Compliant;
                }

                if (state == EpizooticComplianceState.Compliant)
                {
                    compliantCount++;
                }
                else
                {
                    actionAnimals.Add(new EpizooticDueAnimalDto(
                        animal.Id,
                        animal.TagNumber,
                        animal.Name,
                        animal.Breed,
                        animal.BarnName,
                        ageDays,
                        lastExecution,
                        dueAt,
                        daysUntilDue,
                        state));
                }
            }

            result.Add(new EpizooticRulePlanDto(
                rule.Id,
                rule.Title,
                rule.DiseaseType,
                rule.EventType,
                rule.MinAgeDays,
                rule.IntervalDays,
                rule.RegulatoryBasis,
                rule.DefaultVetDrugId,
                rule.DefaultVetDrug?.Name,
                rule.DefaultVetDrug?.BatchSeries,
                rule.DefaultVetDrug?.StockDoses ?? 0m,
                rule.StandardDosePerAnimal,
                actionAnimals.OrderBy(a => a.DaysUntilDue).ToList(),
                compliantCount));
        }

        return result;
    }

    public async Task<IReadOnlyList<EpizooticAct>> GetRecentActsAsync(int count = 25, CancellationToken ct = default)
    {
        return await _db.EpizooticActs
            .AsNoTracking()
            .Include(a => a.EpizooticRule)
            .Include(a => a.Items)
            .ThenInclude(i => i.Animal)
            .OrderByDescending(a => a.ExecutedAtUtc)
            .Take(count)
            .ToListAsync(ct);
    }

    public async Task<EpizooticAct?> GetActByIdAsync(Guid actId, CancellationToken ct = default)
    {
        return await _db.EpizooticActs
            .AsNoTracking()
            .Include(a => a.EpizooticRule)
            .Include(a => a.VetDrug)
            .Include(a => a.Items)
            .ThenInclude(i => i.Animal)
            .FirstOrDefaultAsync(a => a.Id == actId, ct);
    }

    public async Task<IReadOnlyList<Animal>> GetQuarantineIsolatorAnimalsAsync(CancellationToken ct = default)
    {
        return await _db.Animals
            .AsNoTracking()
            .Include(a => a.Treatments)
            .Include(a => a.Inspections)
            .Where(a => !a.IsHistoricalPedigreeOnly && (a.HealthStatus == HealthStatus.Quarantine || a.BarnName == "Карантинный изолятор"))
            .OrderByDescending(a => a.QuarantineEnteredAtUtc)
            .ToListAsync(ct);
    }

    public async Task<EpizooticAct> ExecuteGroupActAsync(ExecuteGroupEpizooticActCommand command, CancellationToken ct = default)
    {
        var validation = await _actValidator.ValidateAsync(command, ct);
        if (!validation.IsValid)
        {
            throw new DomainRuleException(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var nowUtc = DateTime.UtcNow;
        var isRelational = _db.Database.IsRelational();
        await using var tx = isRelational
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct)
            : null;

        try
        {
            var rule = await _db.EpizooticRules.FirstOrDefaultAsync(r => r.Id == command.RuleId, ct)
                ?? throw new DomainRuleException("Противоэпизоотическое правило не найдено.");

            var drug = await _db.VetDrugs.FirstOrDefaultAsync(d => d.Id == command.VetDrugId, ct)
                ?? throw new DomainRuleException("Биопрепарат/вакцина не найдена на складе.");

            var distinctIds = command.AnimalIds.Distinct().ToList();
            var animals = await _db.Animals
                .Where(a => distinctIds.Contains(a.Id) && !a.IsHistoricalPedigreeOnly)
                .ToListAsync(ct);

            if (animals.Count == 0)
            {
                throw new DomainRuleException("Не выбрано ни одного действующего животного стада.");
            }

            var totalDoses = animals.Count * command.DosePerAnimal;

            // Строгая проверка срока годности и списание вакцины по серии с защитой SQL Server rowversion!
            drug.EnsureValidForAdministration(nowUtc);
            drug.DeductStock(totalDoses);

            var actNumber = $"АКТ-ЭПИЗ-{nowUtc:yyyyMMdd-HHmmss}";
            var resultLabel = rule.EventType == EpizooticEventType.Vaccination
                ? "Иммунизировано планово, поствакцинальных реакций нет"
                : "Исследовано — реакция отрицательная (норма)";

            var act = new EpizooticAct
            {
                ActNumber = actNumber,
                EpizooticRuleId = rule.Id,
                EpizooticRule = rule,
                VetDrugId = drug.Id,
                VetDrug = drug,
                DrugNameSnapshot = drug.Name,
                DrugSeriesSnapshot = drug.BatchSeries,
                ExecutedAtUtc = nowUtc,
                TargetBarn = string.IsNullOrWhiteSpace(command.TargetBarn) ? "Сводная группа по ферме ИрГАУ" : command.TargetBarn.Trim(),
                DosePerAnimal = command.DosePerAnimal,
                TotalDosesUsed = totalDoses,
                ExecutedByVeterinarian = string.IsNullOrWhiteSpace(command.ExecutedByVeterinarian) ? "Главный ветврач ИрГАУ" : command.ExecutedByVeterinarian.Trim(),
                WitnessZootechnician = string.IsNullOrWhiteSpace(command.WitnessZootechnician) ? "Ильина Е.С." : command.WitnessZootechnician.Trim(),
                Notes = command.Notes?.Trim()
            };

            foreach (var animal in animals)
            {
                act.Items.Add(new EpizooticActItem
                {
                    EpizooticActId = act.Id,
                    AnimalId = animal.Id,
                    Animal = animal,
                    ResultText = resultLabel,
                    NextDueAtUtc = nowUtc.AddDays(rule.IntervalDays)
                });
            }

            _db.EpizooticActs.Add(act);
            await _db.SaveChangesAsync(ct);

            if (tx is not null)
            {
                await tx.CommitAsync(ct);
            }

            return act;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            if (tx is not null)
            {
                await tx.RollbackAsync(ct);
            }

            throw new ConcurrencyConflictException(
                "Конфликт конкурентного списания вакцины по серии (SQL rowversion): остаток биопрепарата был изменен параллельной транзакцией.",
                ex);
        }
    }
}

public sealed class TemporalAuditService : ITemporalAuditService
{
    private readonly VetGuardDbContext _db;
    private readonly string? _connectionString;

    public TemporalAuditService(VetGuardDbContext db, IConfiguration configuration)
    {
        _db = db;
        _connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    public async Task<IReadOnlyList<TreatmentTemporalHistoryRowDto>> GetTreatmentTemporalHistoryAsync(
        Guid? animalId = null,
        CancellationToken ct = default)
    {
        if (!_db.Database.IsRelational() || string.IsNullOrWhiteSpace(_connectionString))
        {
            return Array.Empty<TreatmentTemporalHistoryRowDto>();
        }

        const string sql = @"
SELECT TOP (80)
    tr.Id,
    tr.AnimalId,
    ISNULL(a.TagNumber, N'—') AS AnimalTag,
    ISNULL(a.Name, N'—') AS AnimalName,
    tr.DrugNameSnapshot,
    tr.DrugBatchSeriesSnapshot,
    tr.IsPotentDrugSnapshot,
    tr.DosageAmount,
    tr.DosageUnit,
    tr.Diagnosis,
    tr.VeterinarianName,
    tr.AdministeredAtUtc,
    tr.MilkWithdrawalEndsAtUtc,
    tr.SysStartTime,
    tr.SysEndTime
FROM dbo.TreatmentRecords FOR SYSTEM_TIME ALL AS tr
LEFT JOIN dbo.Animals AS a ON a.Id = tr.AnimalId
WHERE (@AnimalId IS NULL OR tr.AnimalId = @AnimalId)
ORDER BY tr.SysStartTime DESC;";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var cmd = new CommandDefinition(sql, new { AnimalId = animalId }, cancellationToken: ct);
        var rows = await conn.QueryAsync<TreatmentTemporalHistoryRowDto>(cmd);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<AnimalTemporalHistoryRowDto>> GetAnimalStatusTemporalHistoryAsync(
        Guid? animalId = null,
        CancellationToken ct = default)
    {
        if (!_db.Database.IsRelational() || string.IsNullOrWhiteSpace(_connectionString))
        {
            return Array.Empty<AnimalTemporalHistoryRowDto>();
        }

        const string sql = @"
SELECT TOP (80)
    a.Id,
    a.TagNumber,
    a.Name,
    a.HealthStatus,
    a.BarnName,
    a.SectionCode,
    a.QuarantineReason,
    a.UpdatedAtUtc,
    a.SysStartTime,
    a.SysEndTime
FROM dbo.Animals FOR SYSTEM_TIME ALL AS a
WHERE a.IsHistoricalPedigreeOnly = 0
  AND (@AnimalId IS NULL OR a.Id = @AnimalId)
ORDER BY a.SysStartTime DESC;";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var cmd = new CommandDefinition(sql, new { AnimalId = animalId }, cancellationToken: ct);
        var rows = await conn.QueryAsync<AnimalTemporalHistoryRowDto>(cmd);
        return rows.ToList();
    }
}
