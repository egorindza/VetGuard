using VetGuard.Application.Models;
using VetGuard.Domain.Entities;

namespace VetGuard.Application.Interfaces;

public interface IVetAlertNotifier
{
    Task NotifyQuarantineLockActivatedAsync(
        string animalTag,
        string animalName,
        string drugName,
        int milkHours,
        int meatDays,
        DateTime clearanceUtc,
        CancellationToken ct = default);

    Task NotifyQuarantineViolationBlockedAsync(
        string batchNumber,
        string animalTag,
        string animalName,
        string? drugName,
        double remainingHours,
        string attemptedBy,
        CancellationToken ct = default);

    Task NotifyQuarantineExpiredAsync(
        string animalTag,
        string animalName,
        string drugName,
        CancellationToken ct = default);

    Task NotifyEpizooticReminderAsync(
        string message,
        int overdueCount,
        int dueSoonCount,
        CancellationToken ct = default);
}

public interface IHerdService
{
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AnimalListItemDto>> GetAnimalsAsync(AnimalFilterQuery filter, CancellationToken ct = default);
    Task<IReadOnlyList<Animal>> GetBreedingFemalesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Animal>> GetBreedingBullsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Animal>> GetAllActiveAnimalsForSelectionAsync(CancellationToken ct = default);
    Task<AnimalDetailsDto?> GetAnimalDetailsAsync(Guid id, string baseHostUrl, CancellationToken ct = default);
    Task<AnimalDetailsDto?> GetAnimalByTagOrIdAsync(string tagOrId, string baseHostUrl, CancellationToken ct = default);
    Task<Animal> CreateOrUpdateAnimalAsync(CreateOrUpdateAnimalCommand command, CancellationToken ct = default);
    Task LogMilkYieldAsync(LogMilkYieldCommand command, CancellationToken ct = default);
    Task LogWeightAsync(LogWeightCommand command, CancellationToken ct = default);
    Task RecordVetInspectionAsync(MobileVetInspectionCommand command, CancellationToken ct = default);
    Task PlaceInQuarantineIsolatorAsync(Guid animalId, string reason, string isolatorBox, CancellationToken ct = default);
    Task ReleaseFromQuarantineIsolatorAsync(Guid animalId, CancellationToken ct = default);
}

public interface ITreatmentAndDrugService
{
    Task<IReadOnlyList<VetDrug>> GetDrugsAsync(CancellationToken ct = default);
    Task<VetDrug> CreateDrugAsync(CreateVetDrugCommand command, CancellationToken ct = default);
    Task<VetDrug> ReplenishStockAsync(ReplenishDrugStockCommand command, CancellationToken ct = default);
    Task<WithdrawalPreviewDto?> PreviewWithdrawalAsync(Guid drugId, DateTime? administeredAtUtc, CancellationToken ct = default);
    Task<IReadOnlyList<TreatmentRecord>> GetRecentTreatmentsAsync(int count = 50, CancellationToken ct = default);
    Task<TreatmentRecord> RegisterTreatmentAsync(RegisterTreatmentCommand command, CancellationToken ct = default);
    Task<int> ProcessExpiredWithdrawalsAsync(CancellationToken ct = default);
}

public interface IProductBatchService
{
    Task<IReadOnlyList<ProductBatch>> GetBatchesAsync(CancellationToken ct = default);
    Task<ProductBatch?> GetBatchByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<QuarantineBlockLog>> GetBlockedLogsAsync(int count = 30, CancellationToken ct = default);
    Task<BatchCreationOutcomeDto> CreateBatchWithQuarantineCheckAsync(CreateProductBatchCommand command, CancellationToken ct = default);
    Task<ProductBatch> ApproveBatchAsync(ApproveProductBatchCommand command, CancellationToken ct = default);
    Task<ProductBatch> MarkShippedAsync(Guid batchId, CancellationToken ct = default);
}

public interface IPedigreeService
{
    /// <summary>
    /// Выполняет рекурсивный T-SQL запрос WITH PedigreeTree AS (...) через Dapper на глубину до 6 поколений.
    /// </summary>
    Task<PedigreeTreeViewDto?> GetPedigreeTreeAsync(Guid animalId, int maxGenerations = 6, CancellationToken ct = default);

    /// <summary>
    /// Выполняет пакетный рекурсивный T-SQL запрос WITH PedigreeTree AS (...) через Dapper,
    /// находит общих предков до 6 колен между выбранной маткой и всеми быками-производителями,
    /// рассчитывая коэффициент инбридинга С. Райта (%) и степень по Шапоружу на уровне СУБД.
    /// </summary>
    Task<IReadOnlyList<CandidateSireEvaluationDto>> EvaluateCandidateSiresForDamAsync(
        Guid damId,
        int maxGenerations = 6,
        CancellationToken ct = default);

    Task<IReadOnlyList<BreedingPlan>> GetBreedingPlansAsync(CancellationToken ct = default);
    Task<BreedingPlan> SaveBreedingPlanAsync(SaveBreedingPlanCommand command, CancellationToken ct = default);
}

public interface IEpizooticService
{
    Task<IReadOnlyList<EpizooticRulePlanDto>> GetEpizooticCalendarPlanAsync(CancellationToken ct = default);
    Task<IReadOnlyList<EpizooticAct>> GetRecentActsAsync(int count = 25, CancellationToken ct = default);
    Task<EpizooticAct?> GetActByIdAsync(Guid actId, CancellationToken ct = default);
    Task<IReadOnlyList<Animal>> GetQuarantineIsolatorAnimalsAsync(CancellationToken ct = default);
    Task<EpizooticAct> ExecuteGroupActAsync(ExecuteGroupEpizooticActCommand command, CancellationToken ct = default);
}

public interface ITemporalAuditService
{
    Task<IReadOnlyList<TreatmentTemporalHistoryRowDto>> GetTreatmentTemporalHistoryAsync(Guid? animalId = null, CancellationToken ct = default);
    Task<IReadOnlyList<AnimalTemporalHistoryRowDto>> GetAnimalStatusTemporalHistoryAsync(Guid? animalId = null, CancellationToken ct = default);
}

public interface IQrCodeService
{
    string GenerateSvgQrCode(string payload);
    byte[] GeneratePngQrCode(string payload, int pixelsPerModule = 10);
}

public interface IPdfReportService
{
    byte[] GenerateStallQrPassportPdf(AnimalDetailsDto details);
    byte[] GenerateVeterinaryExtractPdf(AnimalDetailsDto details);
    byte[] GenerateBatchVetCertificatePdf(ProductBatch batch);
    byte[] GenerateEpizooticActPdf(EpizooticAct act);
}
