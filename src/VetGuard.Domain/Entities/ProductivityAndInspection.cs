using VetGuard.Domain.Enums;

namespace VetGuard.Domain.Entities;

public class MilkYieldRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AnimalId { get; set; }
    public Animal? Animal { get; set; }

    public DateTime MilkingDate { get; set; }
    public decimal MorningLiters { get; set; }
    public decimal EveningLiters { get; set; }
    public decimal TotalLiters => MorningLiters + EveningLiters;
    public decimal FatPercent { get; set; } = 3.9m;
    public decimal ProteinPercent { get; set; } = 3.3m;

    /// <summary>
    /// Если у коровы действовал карантинный замок, молоко было надоено в отдельный санитарный бачок и утилизировано.
    /// </summary>
    public bool IsQuarantineDiscarded { get; set; }
    public string RecordedBy { get; set; } = string.Empty;
}

public class WeightRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AnimalId { get; set; }
    public Animal? Animal { get; set; }

    public DateTime MeasuredAtUtc { get; set; }
    public decimal WeightKg { get; set; }
    public int DailyGainGrams { get; set; }
    public string MeasuredBy { get; set; } = string.Empty;
}

public class VetInspection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AnimalId { get; set; }
    public Animal? Animal { get; set; }

    public DateTime InspectedAtUtc { get; set; } = DateTime.UtcNow;
    public decimal BodyTemperatureC { get; set; } = 38.5m;
    public int HeartRateBpm { get; set; } = 72;
    public int RuminationPer2Min { get; set; } = 3;
    public string MucousMembranes { get; set; } = "Бледно-розовые, влажные";
    public string UdderCondition { get; set; } = "Без уплотнений, безболезненное";
    public HealthStatus ResultHealthStatus { get; set; } = HealthStatus.Healthy;
    public string Summary { get; set; } = string.Empty;
    public string InspectorName { get; set; } = string.Empty;
    public bool ScannedViaQrPassport { get; set; } = true;
}
