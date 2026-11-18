using VetGuard.Application.Models;
using VetGuard.Domain.Entities;

namespace VetGuard.Web.Models;

public sealed class AnimalsIndexViewModel
{
    public AnimalFilterQuery Filter { get; set; } = new();
    public IReadOnlyList<AnimalListItemDto> Animals { get; set; } = Array.Empty<AnimalListItemDto>();
    public IReadOnlyList<string> AvailableBarns { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> AvailableBreeds { get; set; } = Array.Empty<string>();
}

public sealed class AnimalEditModalViewModel
{
    public CreateOrUpdateAnimalCommand Command { get; set; } = new();
    public IReadOnlyList<Animal> PotentialSires { get; set; } = Array.Empty<Animal>();
    public IReadOnlyList<Animal> PotentialDams { get; set; } = Array.Empty<Animal>();
}

public sealed class TreatmentsIndexViewModel
{
    public IReadOnlyList<TreatmentRecord> RecentTreatments { get; set; } = Array.Empty<TreatmentRecord>();
    public IReadOnlyList<Animal> ActiveAnimals { get; set; } = Array.Empty<Animal>();
    public IReadOnlyList<VetDrug> AvailableDrugs { get; set; } = Array.Empty<VetDrug>();
    public WithdrawalPreviewDto? InitialDrugPreview { get; set; }
    public Guid? PreselectedAnimalId { get; set; }
}

public sealed class BatchesIndexViewModel
{
    public IReadOnlyList<ProductBatch> Batches { get; set; } = Array.Empty<ProductBatch>();
    public IReadOnlyList<QuarantineBlockLog> BlockedLogs { get; set; } = Array.Empty<QuarantineBlockLog>();
    public IReadOnlyList<AnimalListItemDto> HerdAnimals { get; set; } = Array.Empty<AnimalListItemDto>();
    public BatchCreationOutcomeDto? LastOutcome { get; set; }
    public string SuggestedBatchNumber { get; set; } = string.Empty;
}

public sealed class BreedingIndexViewModel
{
    public IReadOnlyList<Animal> AllPedigreeSubjects { get; set; } = Array.Empty<Animal>();
    public IReadOnlyList<Animal> BreedingFemales { get; set; } = Array.Empty<Animal>();
    public Guid SelectedSubjectId { get; set; }
    public Guid SelectedDamId { get; set; }
    public PedigreeTreeViewDto? InitialTree { get; set; }
    public IReadOnlyList<CandidateSireEvaluationDto> InitialCandidates { get; set; } = Array.Empty<CandidateSireEvaluationDto>();
    public IReadOnlyList<BreedingPlan> SavedPlans { get; set; } = Array.Empty<BreedingPlan>();
}

public sealed class EpizooticsIndexViewModel
{
    public IReadOnlyList<EpizooticRulePlanDto> Plans { get; set; } = Array.Empty<EpizooticRulePlanDto>();
    public IReadOnlyList<Animal> IsolatorAnimals { get; set; } = Array.Empty<Animal>();
    public IReadOnlyList<EpizooticAct> RecentActs { get; set; } = Array.Empty<EpizooticAct>();
    public IReadOnlyList<VetDrug> BiologicsCatalog { get; set; } = Array.Empty<VetDrug>();
    public IReadOnlyList<Animal> NonIsolatedAnimals { get; set; } = Array.Empty<Animal>();
}

public sealed class GroupEpizooticModalViewModel
{
    public EpizooticRulePlanDto Plan { get; set; } = null!;
    public IReadOnlyList<VetDrug> Biologics { get; set; } = Array.Empty<VetDrug>();
}

public sealed class TemporalAuditViewModel
{
    public IReadOnlyList<TreatmentTemporalHistoryRowDto> TreatmentHistory { get; set; } = Array.Empty<TreatmentTemporalHistoryRowDto>();
    public IReadOnlyList<AnimalTemporalHistoryRowDto> AnimalStatusHistory { get; set; } = Array.Empty<AnimalTemporalHistoryRowDto>();
    public Guid? FilteredAnimalId { get; set; }
    public IReadOnlyList<Animal> ActiveAnimals { get; set; } = Array.Empty<Animal>();
}
