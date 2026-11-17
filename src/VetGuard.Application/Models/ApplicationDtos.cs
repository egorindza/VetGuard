using VetGuard.Domain.Entities;
using VetGuard.Domain.Enums;

namespace VetGuard.Application.Models;

public sealed record DashboardSummaryDto(
    int TotalActiveLivestock,
    int MilkingCowsCount,
    int ActiveQuarantineLocksCount,
    int QuarantineIsolatorCount,
    int OverdueEpizooticTasksCount,
    int BlockedBatchAttemptsCount,
    decimal TodayTotalSafeMilkLiters,
    IReadOnlyList<ActiveQuarantineTimerDto> ActiveTimers,
    IReadOnlyList<QuarantineBlockLog> RecentBlockedAttempts,
    IReadOnlyList<TreatmentRecord> RecentTreatments);

public sealed record ActiveQuarantineTimerDto(
    Guid AnimalId,
    string TagNumber,
    string AnimalName,
    string BarnName,
    string DrugName,
    string DrugSeries,
    DateTime AdministeredAtUtc,
    DateTime MilkWithdrawalEndsAtUtc,
    DateTime MeatWithdrawalEndsAtUtc,
    bool IsMilkLocked,
    bool IsMeatLocked,
    double RemainingMilkHours,
    double RemainingMeatDays);

public sealed class AnimalFilterQuery
{
    public string? Search { get; set; }
    public string? Breed { get; set; }
    public AnimalSex? Sex { get; set; }
    public string? BarnName { get; set; }
    public HealthStatus? HealthStatus { get; set; }
    public bool OnlyQuarantineLocked { get; set; }
}

public sealed record AnimalListItemDto(
    Guid Id,
    string TagNumber,
    string? RfidChip,
    string Name,
    string Breed,
    AnimalSex Sex,
    DateTime BirthDate,
    string FormattedAge,
    string BarnName,
    string SectionCode,
    HealthStatus HealthStatus,
    decimal CurrentWeightKg,
    decimal? AverageDailyMilkKg,
    bool IsMilkQuarantineLocked,
    bool IsMeatQuarantineLocked,
    string? ActiveLockDrugName,
    DateTime? MilkLockEndsAtUtc,
    double RemainingMilkLockHours);

public sealed record AnimalDetailsDto(
    Animal Animal,
    TreatmentRecord? ActiveMilkLock,
    TreatmentRecord? ActiveMeatLock,
    IReadOnlyList<TreatmentRecord> Treatments,
    IReadOnlyList<MilkYieldRecord> MilkYields,
    IReadOnlyList<WeightRecord> WeightRecords,
    IReadOnlyList<VetInspection> Inspections,
    IReadOnlyList<EpizooticActItem> EpizooticHistory,
    string QrCodeSvg,
    string PassportUrl);

public sealed class CreateOrUpdateAnimalCommand
{
    public Guid? Id { get; set; }
    public string TagNumber { get; set; } = string.Empty;
    public string? RfidChip { get; set; }
    public string Name { get; set; } = string.Empty;
    public AnimalSpecies Species { get; set; } = AnimalSpecies.Cattle;
    public string Breed { get; set; } = "Голштинская";
    public AnimalSex Sex { get; set; } = AnimalSex.Female;
    public DateTime BirthDate { get; set; } = DateTime.UtcNow.Date.AddYears(-3);
    public Guid? SireId { get; set; }
    public Guid? DamId { get; set; }
    public string BarnName { get; set; } = "Учебный корпус №1 (Молочный)";
    public string SectionCode { get; set; } = "Секция А / Стойло 01";
    public string? LineageCode { get; set; }
    public decimal CurrentWeightKg { get; set; } = 540m;
    public decimal? AverageDailyMilkKg { get; set; } = 26.5m;
    public string? Notes { get; set; }
}

public sealed class LogMilkYieldCommand
{
    public Guid AnimalId { get; set; }
    public DateTime MilkingDate { get; set; } = DateTime.UtcNow.Date;
    public decimal MorningLiters { get; set; }
    public decimal EveningLiters { get; set; }
    public decimal FatPercent { get; set; } = 3.9m;
    public decimal ProteinPercent { get; set; } = 3.3m;
    public string RecordedBy { get; set; } = string.Empty;
}

public sealed class LogWeightCommand
{
    public Guid AnimalId { get; set; }
    public DateTime MeasuredAtUtc { get; set; } = DateTime.UtcNow;
    public decimal WeightKg { get; set; }
    public string MeasuredBy { get; set; } = string.Empty;
}

public sealed class MobileVetInspectionCommand
{
    public Guid AnimalId { get; set; }
    public decimal BodyTemperatureC { get; set; } = 38.6m;
    public int HeartRateBpm { get; set; } = 72;
    public int RuminationPer2Min { get; set; } = 3;
    public string MucousMembranes { get; set; } = "Бледно-розовые, влажные";
    public string UdderCondition { get; set; } = "Мягкое, безболезненное, секрет в норме";
    public HealthStatus NewHealthStatus { get; set; } = HealthStatus.Healthy;
    public bool MoveToQuarantineIsolator { get; set; }
    public string? QuarantineReason { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string InspectorName { get; set; } = string.Empty;
}

public sealed class CreateVetDrugCommand
{
    public string Name { get; set; } = string.Empty;
    public string ActiveSubstance { get; set; } = string.Empty;
    public DrugCategory Category { get; set; } = DrugCategory.Antibiotic;
    public string Manufacturer { get; set; } = string.Empty;
    public string BatchSeries { get; set; } = string.Empty;
    public DateTime ExpirationDate { get; set; } = DateTime.UtcNow.Date.AddYears(1);
    public int MilkWithdrawalHours { get; set; }
    public int MeatWithdrawalDays { get; set; }
    public string DefaultDosageText { get; set; } = "1 мл на 10 кг живой массы";
    public string Unit { get; set; } = "мл";
    public decimal StockDoses { get; set; } = 100m;
    public bool IsPotent { get; set; }
    public string StorageConditions { get; set; } = "От +5 °C до +25 °C в сухом темном месте";
}

public sealed class ReplenishDrugStockCommand
{
    public Guid DrugId { get; set; }
    public decimal AdditionalDoses { get; set; }
    public string? NewBatchSeries { get; set; }
    public DateTime? NewExpirationDate { get; set; }
    public string? ExpectedRowVersionBase64 { get; set; }
}

public sealed class RegisterTreatmentCommand
{
    public Guid AnimalId { get; set; }
    public Guid VetDrugId { get; set; }
    public decimal DosageAmount { get; set; } = 20m;
    public string AdministrationRoute { get; set; } = "внутримышечно";
    public string Diagnosis { get; set; } = string.Empty;
    public DateTime? AdministeredAtUtc { get; set; }
    public string VeterinarianName { get; set; } = string.Empty;
    public string VeterinarianRole { get; set; } = "Главный ветврач";
    public string? ClinicalNotes { get; set; }
}

public sealed record WithdrawalPreviewDto(
    Guid DrugId,
    string DrugName,
    string ActiveSubstance,
    string BatchSeries,
    int MilkWithdrawalHours,
    int MeatWithdrawalDays,
    DateTime MilkClearanceUtc,
    DateTime MeatClearanceUtc,
    decimal AvailableStock,
    string Unit,
    string DefaultDosageText,
    bool IsPotent);

public sealed class BatchContributionInput
{
    public Guid AnimalId { get; set; }
    public decimal QuantityLitersOrKg { get; set; }
}

public sealed class CreateProductBatchCommand
{
    public string BatchNumber { get; set; } = string.Empty;
    public ProductBatchType BatchType { get; set; } = ProductBatchType.RawMilk;
    public string Destination { get; set; } = "АО «Янтарь» (Иркутский молочный завод)";
    public decimal? FatPercent { get; set; } = 3.95m;
    public decimal? ProteinPercent { get; set; } = 3.32m;
    public int? SomaticCellsThousands { get; set; } = 145;
    public string OperatorName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<BatchContributionInput> Contributions { get; set; } = new();
}

public sealed record BatchCreationOutcomeDto(
    bool Succeeded,
    Guid? BatchId,
    string BatchNumber,
    string Message,
    string? BlockedAnimalTag = null,
    string? BlockedAnimalName = null,
    string? BlockedDrugName = null,
    string? BlockedDrugSeries = null,
    double? RemainingQuarantineHours = null,
    DateTime? ClearanceUtc = null);

public sealed class ApproveProductBatchCommand
{
    public Guid BatchId { get; set; }
    public string? ExpectedRowVersionBase64 { get; set; }
    public string VeterinarianName { get; set; } = string.Empty;
}

public sealed class PedigreeTreeNodeDto
{
    public Guid AnimalId { get; set; }
    public Guid? ChildId { get; set; }
    public int GenerationDepth { get; set; }
    public string PathCode { get; set; } = string.Empty;
    public string LineSide { get; set; } = string.Empty;
    public string TagNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Breed { get; set; } = string.Empty;
    public AnimalSex Sex { get; set; }
    public DateTime BirthDate { get; set; }
    public string? LineageCode { get; set; }
    public Guid? SireId { get; set; }
    public Guid? DamId { get; set; }
    public int OccurrenceCount { get; set; }
}

public sealed record PedigreeTreeViewDto(
    Animal Subject,
    IReadOnlyList<PedigreeTreeNodeDto> Nodes,
    IReadOnlyList<string> RepeatedAncestorNames);

public sealed record CandidateCommonAncestorDto(
    Guid AncestorId,
    string AncestorTag,
    string AncestorName,
    string AncestorBreed,
    int DamGenerationDepth,
    int SireGenerationDepth,
    string ShaporuzhDegree,
    double ContributionPercent);

public sealed record CandidateSireEvaluationDto(
    Guid SireId,
    string SireTag,
    string SireName,
    string SireBreed,
    string? SireLineage,
    double InbreedingPercent,
    InbreedingRiskLevel RiskLevel,
    string ShaporuzhSummary,
    IReadOnlyList<CandidateCommonAncestorDto> CommonAncestors,
    string Recommendation);

public sealed class SaveBreedingPlanCommand
{
    public Guid DamId { get; set; }
    public Guid SireId { get; set; }
    public string SelectorName { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public enum EpizooticComplianceState
{
    Overdue = 1,
    DueSoon = 2,
    Compliant = 3
}

public sealed record EpizooticDueAnimalDto(
    Guid AnimalId,
    string TagNumber,
    string Name,
    string Breed,
    string BarnName,
    int AgeDays,
    DateTime? LastExecutedAtUtc,
    DateTime DueAtUtc,
    int DaysUntilDue,
    EpizooticComplianceState State);

public sealed record EpizooticRulePlanDto(
    Guid RuleId,
    string Title,
    EpizooticDiseaseType DiseaseType,
    EpizooticEventType EventType,
    int MinAgeDays,
    int IntervalDays,
    string RegulatoryBasis,
    Guid? DefaultDrugId,
    string? DefaultDrugName,
    string? DefaultDrugSeries,
    decimal AvailableStock,
    decimal StandardDosePerAnimal,
    IReadOnlyList<EpizooticDueAnimalDto> ActionRequiredAnimals,
    int CompliantAnimalsCount);

public sealed class ExecuteGroupEpizooticActCommand
{
    public Guid RuleId { get; set; }
    public Guid VetDrugId { get; set; }
    public List<Guid> AnimalIds { get; set; } = new();
    public string TargetBarn { get; set; } = "Все корпуса по списку";
    public decimal DosePerAnimal { get; set; } = 1.0m;
    public string ExecutedByVeterinarian { get; set; } = string.Empty;
    public string WitnessZootechnician { get; set; } = "Ильина Е.С. (Зоотехник-селекционер)";
    public string? Notes { get; set; }
}

public sealed class TreatmentTemporalHistoryRowDto
{
    public Guid Id { get; set; }
    public Guid AnimalId { get; set; }
    public string AnimalTag { get; set; } = string.Empty;
    public string AnimalName { get; set; } = string.Empty;
    public string DrugNameSnapshot { get; set; } = string.Empty;
    public string DrugBatchSeriesSnapshot { get; set; } = string.Empty;
    public bool IsPotentDrugSnapshot { get; set; }
    public decimal DosageAmount { get; set; }
    public string DosageUnit { get; set; } = string.Empty;
    public string Diagnosis { get; set; } = string.Empty;
    public string VeterinarianName { get; set; } = string.Empty;
    public DateTime AdministeredAtUtc { get; set; }
    public DateTime MilkWithdrawalEndsAtUtc { get; set; }
    public DateTime SysStartTime { get; set; }
    public DateTime SysEndTime { get; set; }
    public bool IsCurrentVersion => SysEndTime.Year >= 9999;
}

public sealed class AnimalTemporalHistoryRowDto
{
    public Guid Id { get; set; }
    public string TagNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public HealthStatus HealthStatus { get; set; }
    public string BarnName { get; set; } = string.Empty;
    public string SectionCode { get; set; } = string.Empty;
    public string? QuarantineReason { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime SysStartTime { get; set; }
    public DateTime SysEndTime { get; set; }
    public bool IsCurrentVersion => SysEndTime.Year >= 9999;
}
