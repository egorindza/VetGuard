using VetGuard.Domain.Enums;

namespace VetGuard.Domain.Exceptions;

public class DomainRuleException : Exception
{
    public DomainRuleException(string message) : base(message)
    {
    }

    public DomainRuleException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Строгое доменное исключение «Карантинный замок» — выбрасывается при любой попытке
/// включить животное с активным периодом выведения препарата (или находящееся в изоляторе)
/// в товарную партию молока или отгрузки на убой.
/// </summary>
public sealed class QuarantineLockDomainException : DomainRuleException
{
    public Guid AnimalId { get; }
    public string AnimalTag { get; }
    public string AnimalName { get; }
    public ProductBatchType BatchType { get; }
    public string LockReason { get; }
    public string? DrugName { get; }
    public string? DrugSeries { get; }
    public DateTime? ClearanceUtc { get; }
    public TimeSpan? RemainingDuration { get; }

    public double RemainingHours => RemainingDuration?.TotalHours ?? 0;

    public QuarantineLockDomainException(
        Guid animalId,
        string animalTag,
        string animalName,
        ProductBatchType batchType,
        string lockReason,
        string? drugName = null,
        string? drugSeries = null,
        DateTime? clearanceUtc = null,
        TimeSpan? remainingDuration = null)
        : base(BuildMessage(animalTag, animalName, batchType, lockReason, drugName, drugSeries, clearanceUtc, remainingDuration))
    {
        AnimalId = animalId;
        AnimalTag = animalTag;
        AnimalName = animalName;
        BatchType = batchType;
        LockReason = lockReason;
        DrugName = drugName;
        DrugSeries = drugSeries;
        ClearanceUtc = clearanceUtc;
        RemainingDuration = remainingDuration;
    }

    private static string BuildMessage(
        string animalTag,
        string animalName,
        ProductBatchType batchType,
        string lockReason,
        string? drugName,
        string? drugSeries,
        DateTime? clearanceUtc,
        TimeSpan? remainingDuration)
    {
        var batchName = batchType == ProductBatchType.RawMilk ? "товарное молоко" : "партию на убой/отгрузку";
        if (!string.IsNullOrWhiteSpace(drugName) && remainingDuration.HasValue)
        {
            var totalHours = (int)Math.Floor(remainingDuration.Value.TotalHours);
            var minutes = remainingDuration.Value.Minutes;
            if (totalHours == 0 && minutes == 0 && remainingDuration.Value.TotalSeconds > 0)
            {
                minutes = 1;
            }

            var seriesInfo = string.IsNullOrWhiteSpace(drugSeries) ? string.Empty : $" (серия {drugSeries})";
            var clearanceText = clearanceUtc.HasValue
                ? $", полное выведение: {clearanceUtc.Value.AddHours(8):dd.MM.yyyy HH:mm} (ИКТ)"
                : string.Empty;

            return $"КАРАНТИННЫЙ ЗАМОК! Запрещено включение животного {animalName} [{animalTag}] в {batchName}. " +
                   $"Активный препарат: «{drugName}»{seriesInfo}. " +
                   $"Остаток карантина: {totalHours} ч {minutes} мин{clearanceText}.";
        }

        return $"КАРАНТИННЫЙ ЗАМОК! Запрещено включение животного {animalName} [{animalTag}] в {batchName}: {lockReason}";
    }
}

public sealed class InsufficientDrugStockException : DomainRuleException
{
    public string DrugName { get; }
    public string BatchSeries { get; }
    public decimal AvailableDoses { get; }
    public decimal RequestedDoses { get; }

    public InsufficientDrugStockException(string drugName, string batchSeries, decimal availableDoses, decimal requestedDoses)
        : base($"Недостаточный остаток препарата «{drugName}» (серия {batchSeries}): доступно {availableDoses:0.##}, запрошено {requestedDoses:0.##}.")
    {
        DrugName = drugName;
        BatchSeries = batchSeries;
        AvailableDoses = availableDoses;
        RequestedDoses = requestedDoses;
    }
}

public sealed class ConcurrencyConflictException : DomainRuleException
{
    public ConcurrencyConflictException(string message, Exception? innerException = null)
        : base(message, innerException ?? new Exception(message))
    {
    }
}

public sealed class InbreedingHazardException : DomainRuleException
{
    public double InbreedingCoefficientPercent { get; }

    public InbreedingHazardException(double coefficientPercent, string message) : base(message)
    {
        InbreedingCoefficientPercent = coefficientPercent;
    }
}
