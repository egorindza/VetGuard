using System.Data;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using VetGuard.Application.Interfaces;
using VetGuard.Application.Models;
using VetGuard.Domain.Entities;
using VetGuard.Domain.Enums;
using VetGuard.Domain.Exceptions;
using VetGuard.Infrastructure.Persistence;

namespace VetGuard.Infrastructure.Services;

public sealed class TreatmentAndDrugService : ITreatmentAndDrugService
{
    private readonly VetGuardDbContext _db;
    private readonly IVetAlertNotifier _notifier;
    private readonly IValidator<CreateVetDrugCommand> _drugValidator;
    private readonly IValidator<RegisterTreatmentCommand> _treatmentValidator;

    public TreatmentAndDrugService(
        VetGuardDbContext db,
        IVetAlertNotifier notifier,
        IValidator<CreateVetDrugCommand> drugValidator,
        IValidator<RegisterTreatmentCommand> treatmentValidator)
    {
        _db = db;
        _notifier = notifier;
        _drugValidator = drugValidator;
        _treatmentValidator = treatmentValidator;
    }

    public async Task<IReadOnlyList<VetDrug>> GetDrugsAsync(CancellationToken ct = default)
    {
        return await _db.VetDrugs
            .AsNoTracking()
            .OrderBy(d => d.Category)
            .ThenBy(d => d.Name)
            .ToListAsync(ct);
    }

    public async Task<VetDrug> CreateDrugAsync(CreateVetDrugCommand command, CancellationToken ct = default)
    {
        var validation = await _drugValidator.ValidateAsync(command, ct);
        if (!validation.IsValid)
        {
            throw new DomainRuleException(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var drug = new VetDrug
        {
            Name = command.Name.Trim(),
            ActiveSubstance = command.ActiveSubstance.Trim(),
            Category = command.Category,
            Manufacturer = command.Manufacturer.Trim(),
            BatchSeries = command.BatchSeries.Trim(),
            ExpirationDate = command.ExpirationDate.Date,
            MilkWithdrawalHours = command.MilkWithdrawalHours,
            MeatWithdrawalDays = command.MeatWithdrawalDays,
            DefaultDosageText = command.DefaultDosageText.Trim(),
            Unit = command.Unit.Trim(),
            StockDoses = command.StockDoses,
            IsPotent = command.IsPotent,
            StorageConditions = command.StorageConditions.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.VetDrugs.Add(drug);
        await _db.SaveChangesAsync(ct);
        return drug;
    }

    public async Task<VetDrug> ReplenishStockAsync(ReplenishDrugStockCommand command, CancellationToken ct = default)
    {
        var drug = await _db.VetDrugs.FirstOrDefaultAsync(d => d.Id == command.DrugId, ct)
            ?? throw new DomainRuleException("Ветпрепарат не найден в справочнике.");

        if (!string.IsNullOrWhiteSpace(command.ExpectedRowVersionBase64))
        {
            _db.Entry(drug).Property(d => d.RowVersion).OriginalValue = Convert.FromBase64String(command.ExpectedRowVersionBase64);
        }

        drug.ReplenishStock(command.AdditionalDoses, command.NewBatchSeries, command.NewExpirationDate);

        try
        {
            await _db.SaveChangesAsync(ct);
            return drug;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(
                $"Конфликт конкурентного доступа (rowversion): остаток препарата «{drug.Name}» (серия {drug.BatchSeries}) был параллельно изменен другим сотрудником. Обновите страницу.",
                ex);
        }
    }

    public async Task<WithdrawalPreviewDto?> PreviewWithdrawalAsync(Guid drugId, DateTime? administeredAtUtc, CancellationToken ct = default)
    {
        var drug = await _db.VetDrugs.AsNoTracking().FirstOrDefaultAsync(d => d.Id == drugId, ct);
        if (drug is null)
        {
            return null;
        }

        var baseTime = administeredAtUtc ?? DateTime.UtcNow;
        return new WithdrawalPreviewDto(
            drug.Id,
            drug.Name,
            drug.ActiveSubstance,
            drug.BatchSeries,
            drug.MilkWithdrawalHours,
            drug.MeatWithdrawalDays,
            drug.CalculateMilkWithdrawalEndUtc(baseTime),
            drug.CalculateMeatWithdrawalEndUtc(baseTime),
            drug.StockDoses,
            drug.Unit,
            drug.DefaultDosageText,
            drug.IsPotent);
    }

    public async Task<IReadOnlyList<TreatmentRecord>> GetRecentTreatmentsAsync(int count = 50, CancellationToken ct = default)
    {
        return await _db.TreatmentRecords
            .AsNoTracking()
            .Include(t => t.Animal)
            .Include(t => t.VetDrug)
            .OrderByDescending(t => t.AdministeredAtUtc)
            .Take(count)
            .ToListAsync(ct);
    }

    public async Task<TreatmentRecord> RegisterTreatmentAsync(RegisterTreatmentCommand command, CancellationToken ct = default)
    {
        var validation = await _treatmentValidator.ValidateAsync(command, ct);
        if (!validation.IsValid)
        {
            throw new DomainRuleException(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        var (record, animalTag, animalName, drugName, milkHours, meatDays) = await strategy.ExecuteAsync(async () =>
        {
            var isRelational = _db.Database.IsRelational();
            await using var tx = isRelational
                ? await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct)
                : null;

            try
            {
                var animal = await _db.Animals
                    .Include(a => a.Treatments)
                    .FirstOrDefaultAsync(a => a.Id == command.AnimalId, ct)
                    ?? throw new DomainRuleException("Карточка животного не найдена.");

                var drug = await _db.VetDrugs
                    .FirstOrDefaultAsync(d => d.Id == command.VetDrugId, ct)
                    ?? throw new DomainRuleException("Ветпрепарат не найден.");

                var administeredUtc = command.AdministeredAtUtc ?? DateTime.UtcNow;

                var created = TreatmentRecord.Create(
                    animal,
                    drug,
                    command.DosageAmount,
                    command.AdministrationRoute,
                    command.Diagnosis,
                    administeredUtc,
                    command.VeterinarianName,
                    command.VeterinarianRole,
                    command.ClinicalNotes);

                _db.TreatmentRecords.Add(created);
                await _db.SaveChangesAsync(ct);

                if (tx is not null)
                {
                    await tx.CommitAsync(ct);
                }

                return (created, animal.TagNumber, animal.Name, drug.Name, drug.MilkWithdrawalHours, drug.MeatWithdrawalDays);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (tx is not null)
                {
                    await tx.RollbackAsync(ct);
                }

                throw new ConcurrencyConflictException(
                    "Конфликт конкурентного списания ветпрепарата (rowversion): остаток серии или статус животного был параллельно изменен.",
                    ex);
            }
        });

        if (milkHours > 0 || meatDays > 0)
        {
            var primaryClearance = milkHours > 0
                ? record.MilkWithdrawalEndsAtUtc
                : record.MeatWithdrawalEndsAtUtc;

            await _notifier.NotifyQuarantineLockActivatedAsync(
                animalTag,
                animalName,
                drugName,
                milkHours,
                meatDays,
                primaryClearance,
                ct);
        }

        return record;
    }

    public async Task<int> ProcessExpiredWithdrawalsAsync(CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;
        var justCleared = await _db.TreatmentRecords
            .Include(t => t.Animal)
            .ThenInclude(a => a!.Treatments)
            .Where(t => !t.IsClearedNotificationSent
                        && t.MilkWithdrawalEndsAtUtc <= nowUtc
                        && t.MeatWithdrawalEndsAtUtc <= nowUtc)
            .ToListAsync(ct);

        if (justCleared.Count == 0)
        {
            return 0;
        }

        foreach (var record in justCleared)
        {
            record.IsClearedNotificationSent = true;
            if (record.Animal is not null
                && record.Animal.HealthStatus == HealthStatus.UnderTreatment
                && record.Animal.GetActiveMilkWithdrawal(nowUtc) is null
                && record.Animal.GetActiveMeatWithdrawal(nowUtc) is null)
            {
                record.Animal.ChangeHealthStatus(HealthStatus.Healthy);
            }
        }

        await _db.SaveChangesAsync(ct);

        foreach (var record in justCleared)
        {
            if (record.Animal is not null)
            {
                await _notifier.NotifyQuarantineExpiredAsync(
                    record.Animal.TagNumber,
                    record.Animal.Name,
                    record.DrugNameSnapshot,
                    ct);
            }
        }

        return justCleared.Count;
    }
}
