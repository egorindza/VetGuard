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

public sealed class ProductBatchService : IProductBatchService
{
    private readonly VetGuardDbContext _db;
    private readonly IVetAlertNotifier _notifier;
    private readonly IValidator<CreateProductBatchCommand> _batchValidator;

    public ProductBatchService(
        VetGuardDbContext db,
        IVetAlertNotifier notifier,
        IValidator<CreateProductBatchCommand> batchValidator)
    {
        _db = db;
        _notifier = notifier;
        _batchValidator = batchValidator;
    }

    public async Task<IReadOnlyList<ProductBatch>> GetBatchesAsync(CancellationToken ct = default)
    {
        return await _db.ProductBatches
            .AsNoTracking()
            .Include(b => b.Items)
            .ThenInclude(i => i.Animal)
            .OrderByDescending(b => b.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<ProductBatch?> GetBatchByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.ProductBatches
            .AsNoTracking()
            .Include(b => b.Items)
            .ThenInclude(i => i.Animal)
            .FirstOrDefaultAsync(b => b.Id == id, ct);
    }

    public async Task<IReadOnlyList<QuarantineBlockLog>> GetBlockedLogsAsync(int count = 30, CancellationToken ct = default)
    {
        return await _db.QuarantineBlockLogs
            .AsNoTracking()
            .OrderByDescending(l => l.AttemptedAtUtc)
            .Take(count)
            .ToListAsync(ct);
    }

    public async Task<BatchCreationOutcomeDto> CreateBatchWithQuarantineCheckAsync(
        CreateProductBatchCommand command,
        CancellationToken ct = default)
    {
        var validation = await _batchValidator.ValidateAsync(command, ct);
        if (!validation.IsValid)
        {
            throw new DomainRuleException(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var batchNumber = command.BatchNumber.Trim().ToUpperInvariant();
        if (await _db.ProductBatches.AnyAsync(b => b.BatchNumber == batchNumber, ct))
        {
            batchNumber = $"{batchNumber}-{DateTime.UtcNow:HHmmss}";
        }

        var nowUtc = DateTime.UtcNow;
        var strategy = _db.Database.CreateExecutionStrategy();

        var outcome = await strategy.ExecuteAsync(async () =>
        {
            var isRelational = _db.Database.IsRelational();
            await using var tx = isRelational
                ? await _db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct)
                : null;

            try
            {
                var animalIds = command.Contributions.Select(c => c.AnimalId).Distinct().ToList();
                var animals = await _db.Animals
                    .Include(a => a.Treatments)
                    .ThenInclude(t => t.VetDrug)
                    .Where(a => animalIds.Contains(a.Id))
                    .ToDictionaryAsync(a => a.Id, ct);

                var batch = new ProductBatch
                {
                    BatchNumber = batchNumber,
                    BatchType = command.BatchType,
                    Status = BatchStatus.Draft,
                    CreatedAtUtc = nowUtc,
                    FatPercent = command.BatchType == ProductBatchType.RawMilk ? command.FatPercent : null,
                    ProteinPercent = command.BatchType == ProductBatchType.RawMilk ? command.ProteinPercent : null,
                    SomaticCellsThousands = command.BatchType == ProductBatchType.RawMilk ? command.SomaticCellsThousands : null,
                    Destination = command.Destination.Trim(),
                    CreatedByOperator = string.IsNullOrWhiteSpace(command.OperatorName) ? "Бригадир доильного зала" : command.OperatorName.Trim(),
                    Notes = command.Notes?.Trim()
                };

                foreach (var contribution in command.Contributions)
                {
                    if (!animals.TryGetValue(contribution.AnimalId, out var animal))
                    {
                        throw new DomainRuleException($"Животное с идентификатором {contribution.AnimalId} не найдено.");
                    }

                    // Жесткий карантинный замок: выбросит QuarantineLockDomainException при активном выведении препарата!
                    batch.AddAnimalContribution(animal, contribution.QuantityLitersOrKg, nowUtc);
                }

                _db.ProductBatches.Add(batch);
                await _db.SaveChangesAsync(ct);

                if (tx is not null)
                {
                    await tx.CommitAsync(ct);
                }

                return new BatchCreationOutcomeDto(
                    Succeeded: true,
                    BatchId: batch.Id,
                    BatchNumber: batch.BatchNumber,
                    Message: $"Партия № {batch.BatchNumber} ({batch.TotalVolumeLitersOrKg:0.0} {(batch.BatchType == ProductBatchType.RawMilk ? "л" : "кг")}) успешно сформирована и прошла проверку карантинного замка.");
            }
            catch (QuarantineLockDomainException qEx)
            {
                if (tx is not null)
                {
                    await tx.RollbackAsync(ct);
                }

                _db.ChangeTracker.Clear();

                // Записываем неизменяемый журнал попытки нарушения карантина (отдельно после отката партии!)
                var incident = new QuarantineBlockLog
                {
                    AttemptedAtUtc = nowUtc,
                    AttemptedBatchNumber = batchNumber,
                    BatchType = command.BatchType,
                    AnimalId = qEx.AnimalId,
                    AnimalTag = qEx.AnimalTag,
                    AnimalName = qEx.AnimalName,
                    ActiveDrugName = qEx.DrugName ?? "Карантинный изолятор",
                    ActiveDrugSeries = qEx.DrugSeries,
                    RemainingHours = Math.Round(qEx.RemainingHours, 2),
                    AttemptedByUser = string.IsNullOrWhiteSpace(command.OperatorName) ? "Оператор доильного зала" : command.OperatorName,
                    BlockMessage = qEx.Message
                };

                _db.QuarantineBlockLogs.Add(incident);
                await _db.SaveChangesAsync(ct);

                return new BatchCreationOutcomeDto(
                    Succeeded: false,
                    BatchId: null,
                    BatchNumber: batchNumber,
                    Message: qEx.Message,
                    BlockedAnimalTag: qEx.AnimalTag,
                    BlockedAnimalName: qEx.AnimalName,
                    BlockedDrugName: qEx.DrugName,
                    BlockedDrugSeries: qEx.DrugSeries,
                    RemainingQuarantineHours: qEx.RemainingHours,
                    ClearanceUtc: qEx.ClearanceUtc);
            }
        });

        if (!outcome.Succeeded && !string.IsNullOrWhiteSpace(outcome.BlockedAnimalTag))
        {
            await _notifier.NotifyQuarantineViolationBlockedAsync(
                outcome.BatchNumber,
                outcome.BlockedAnimalTag,
                outcome.BlockedAnimalName ?? string.Empty,
                outcome.BlockedDrugName,
                outcome.RemainingQuarantineHours ?? 0,
                string.IsNullOrWhiteSpace(command.OperatorName) ? "Оператор доильного зала" : command.OperatorName,
                ct);
        }

        return outcome;
    }

    public async Task<ProductBatch> ApproveBatchAsync(ApproveProductBatchCommand command, CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;
        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            var isRelational = _db.Database.IsRelational();
            await using var tx = isRelational
                ? await _db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct)
                : null;

            try
            {
                var batch = await _db.ProductBatches
                    .Include(b => b.Items)
                    .FirstOrDefaultAsync(b => b.Id == command.BatchId, ct)
                    ?? throw new DomainRuleException("Партия продукции не найдена.");

                if (!string.IsNullOrWhiteSpace(command.ExpectedRowVersionBase64))
                {
                    _db.Entry(batch).Property(b => b.RowVersion).OriginalValue = Convert.FromBase64String(command.ExpectedRowVersionBase64);
                }

                var animalIds = batch.Items.Select(i => i.AnimalId).Distinct().ToList();
                var animals = await _db.Animals
                    .Include(a => a.Treatments)
                    .ThenInclude(t => t.VetDrug)
                    .Where(a => animalIds.Contains(a.Id))
                    .ToListAsync(ct);

                batch.ApproveByVeterinarian(animals, command.VeterinarianName, nowUtc);
                await _db.SaveChangesAsync(ct);

                if (tx is not null)
                {
                    await tx.CommitAsync(ct);
                }

                return batch;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (tx is not null)
                {
                    await tx.RollbackAsync(ct);
                }

                throw new ConcurrencyConflictException(
                    "Конфликт оптимистичной блокировки (SQL rowversion): партия была изменена другим пользователем до завершения ветеринарного допуска!",
                    ex);
            }
        });
    }

    public async Task<ProductBatch> MarkShippedAsync(Guid batchId, CancellationToken ct = default)
    {
        var batch = await _db.ProductBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct)
            ?? throw new DomainRuleException("Партия не найдена.");

        batch.MarkShipped();
        await _db.SaveChangesAsync(ct);
        return batch;
    }
}
