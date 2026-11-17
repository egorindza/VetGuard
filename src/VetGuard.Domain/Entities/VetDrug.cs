using VetGuard.Domain.Enums;
using VetGuard.Domain.Exceptions;

namespace VetGuard.Domain.Entities;

public class VetDrug
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string ActiveSubstance { get; set; } = string.Empty;
    public DrugCategory Category { get; set; }
    public string Manufacturer { get; set; } = string.Empty;
    public string BatchSeries { get; set; } = string.Empty;
    public DateTime ExpirationDate { get; set; }

    /// <summary>
    /// Период ожидания (карантин) по молоку в точных часах.
    /// </summary>
    public int MilkWithdrawalHours { get; set; }

    /// <summary>
    /// Период ожидания (карантин) по мясу / убою в полных сутках.
    /// </summary>
    public int MeatWithdrawalDays { get; set; }

    public string DefaultDosageText { get; set; } = string.Empty;
    public string Unit { get; set; } = "мл";
    public decimal StockDoses { get; set; }

    /// <summary>
    /// Признак сильнодействующего или строго подотчетного ветеринарного препарата.
    /// </summary>
    public bool IsPotent { get; set; }

    public string StorageConditions { get; set; } = "От +2 °C до +8 °C в защищенном от света месте";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optimistic Concurrency токен (SQL Server rowversion) для защиты от гонок при списании серий препаратов/вакцин.
    /// </summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<TreatmentRecord> Treatments { get; set; } = new List<TreatmentRecord>();

    public bool IsExpired(DateTime asOfUtc) => ExpirationDate.Date < asOfUtc.Date;

    public void EnsureValidForAdministration(DateTime administeredAtUtc)
    {
        if (MilkWithdrawalHours < 0 || MeatWithdrawalDays < 0)
        {
            throw new DomainRuleException("Сроки ожидания по молоку и мясу не могут быть отрицательными.");
        }

        if (IsExpired(administeredAtUtc))
        {
            throw new DomainRuleException(
                $"Введение запрещено! Истек срок годности ветпрепарата «{Name}» (серия {BatchSeries}, годен до {ExpirationDate:dd.MM.yyyy}).");
        }
    }

    public void DeductStock(decimal dosesUsed)
    {
        if (dosesUsed <= 0)
        {
            throw new DomainRuleException("Количество списываемого ветпрепарата должно быть строго больше нуля.");
        }

        if (StockDoses < dosesUsed)
        {
            throw new InsufficientDrugStockException(Name, BatchSeries, StockDoses, dosesUsed);
        }

        StockDoses -= dosesUsed;
    }

    public void ReplenishStock(decimal additionalDoses, string? newSeries = null, DateTime? newExpiration = null)
    {
        if (additionalDoses <= 0)
        {
            throw new DomainRuleException("Количество приходуемого препарата должно быть больше нуля.");
        }

        StockDoses += additionalDoses;
        if (!string.IsNullOrWhiteSpace(newSeries))
        {
            BatchSeries = newSeries.Trim();
        }

        if (newExpiration.HasValue)
        {
            ExpirationDate = newExpiration.Value.Date;
        }
    }

    public DateTime CalculateMilkWithdrawalEndUtc(DateTime administeredAtUtc)
        => administeredAtUtc.AddHours(Math.Max(0, MilkWithdrawalHours));

    public DateTime CalculateMeatWithdrawalEndUtc(DateTime administeredAtUtc)
        => administeredAtUtc.AddDays(Math.Max(0, MeatWithdrawalDays));
}
