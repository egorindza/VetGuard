using VetGuard.Domain.Enums;

namespace VetGuard.Domain.Entities;

public class EpizooticRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public EpizooticDiseaseType DiseaseType { get; set; }
    public EpizooticEventType EventType { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Минимальный возраст животного в сутках для включения в обязательный план обработок/исследований.
    /// </summary>
    public int MinAgeDays { get; set; }

    /// <summary>
    /// Периодичность ревакцинации или повторного исследования (сут), напр. 180 или 365 сут.
    /// </summary>
    public int IntervalDays { get; set; }

    public Guid? DefaultVetDrugId { get; set; }
    public VetDrug? DefaultVetDrug { get; set; }

    public decimal StandardDosePerAnimal { get; set; } = 1.0m;
    public string RegulatoryBasis { get; set; } = string.Empty;
    public bool IsMandatory { get; set; } = true;

    public ICollection<EpizooticAct> Acts { get; set; } = new List<EpizooticAct>();
}

public class EpizooticAct
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ActNumber { get; set; } = string.Empty;

    public Guid EpizooticRuleId { get; set; }
    public EpizooticRule? EpizooticRule { get; set; }

    public Guid? VetDrugId { get; set; }
    public VetDrug? VetDrug { get; set; }
    public string DrugNameSnapshot { get; set; } = string.Empty;
    public string DrugSeriesSnapshot { get; set; } = string.Empty;

    public DateTime ExecutedAtUtc { get; set; } = DateTime.UtcNow;
    public string TargetBarn { get; set; } = string.Empty;
    public decimal DosePerAnimal { get; set; }
    public decimal TotalDosesUsed { get; set; }
    public string ExecutedByVeterinarian { get; set; } = string.Empty;
    public string WitnessZootechnician { get; set; } = "Ильина Е.С. (Зоотехник-селекционер)";
    public string? Notes { get; set; }

    public ICollection<EpizooticActItem> Items { get; set; } = new List<EpizooticActItem>();
}

public class EpizooticActItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EpizooticActId { get; set; }
    public EpizooticAct? EpizooticAct { get; set; }

    public Guid AnimalId { get; set; }
    public Animal? Animal { get; set; }

    public string ResultText { get; set; } = "Отрицательно / Привито без осложнений";
    public DateTime NextDueAtUtc { get; set; }
}

public class BreedingPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DamId { get; set; }
    public Animal? Dam { get; set; }

    public Guid SireId { get; set; }
    public Animal? Sire { get; set; }

    /// <summary>
    /// Коэффициент инбридинга потомства по С. Райту в процентах (F_X * 100).
    /// </summary>
    public decimal InbreedingCoefficientPercent { get; set; }

    /// <summary>
    /// Степень инбридинга по Шапоружу (напр. «Аутбридинг», «IV-III», «III-III», «II-II»).
    /// </summary>
    public string ShaporuzhNotation { get; set; } = "Аутбридинг";

    public InbreedingRiskLevel RiskLevel { get; set; }
    public BreedingPlanStatus Status { get; set; }
    public string? CommonAncestorsSummary { get; set; }

    public DateTime PlannedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBySelector { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
