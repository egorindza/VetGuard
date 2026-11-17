using VetGuard.Domain.Enums;
using VetGuard.Domain.Exceptions;

namespace VetGuard.Domain.Entities;

/// <summary>
/// Товарная партия животноводческой продукции (сборный танк молока или партия скота на отгрузку/убой).
/// Защищена Optimistic Concurrency (SQL Server rowversion) и строгими транзакционными проверками карантина.
/// </summary>
public class ProductBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BatchNumber { get; set; } = string.Empty;
    public ProductBatchType BatchType { get; set; }
    public BatchStatus Status { get; set; } = BatchStatus.Draft;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAtUtc { get; set; }

    public decimal TotalVolumeLitersOrKg { get; set; }
    public decimal? FatPercent { get; set; }
    public decimal? ProteinPercent { get; set; }
    public int? SomaticCellsThousands { get; set; }

    public string Destination { get; set; } = "АО «Янтарь» (Иркутский молочный завод)";
    public string CreatedByOperator { get; set; } = string.Empty;
    public string? ApprovedByVeterinarian { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// SQL Server rowversion для предотвращения гонок при одновременном изменении состава партии и её допуске ветврачом.
    /// </summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<ProductBatchItem> Items { get; set; } = new List<ProductBatchItem>();

    public void AddAnimalContribution(Animal animal, decimal quantityLitersOrKg, DateTime checkUtc)
    {
        if (Status == BatchStatus.Approved || Status == BatchStatus.Shipped)
        {
            throw new DomainRuleException($"Партия № {BatchNumber} уже допущена/отгружена и закрыта для редактирования.");
        }

        if (quantityLitersOrKg <= 0)
        {
            throw new DomainRuleException("Объем/масса продукции от животного должна быть больше нуля.");
        }

        // Жесткий доменный карантинный замок!
        animal.EnsureEligibleForProductBatch(BatchType, checkUtc);

        if (Items.Any(i => i.AnimalId == animal.Id))
        {
            throw new DomainRuleException($"Животное {animal.Name} [{animal.TagNumber}] уже включено в данную партию.");
        }

        Items.Add(new ProductBatchItem
        {
            ProductBatchId = Id,
            AnimalId = animal.Id,
            Animal = animal,
            QuantityLitersOrKg = quantityLitersOrKg,
            RecordedAtUtc = checkUtc
        });

        TotalVolumeLitersOrKg = Items.Sum(x => x.QuantityLitersOrKg);
    }

    public void ApproveByVeterinarian(IEnumerable<Animal> animalsInBatch, string vetName, DateTime approvalUtc)
    {
        if (Status == BatchStatus.Approved || Status == BatchStatus.Shipped)
        {
            throw new DomainRuleException($"Партия № {BatchNumber} уже прошла ветеринарно-санитарную экспертизу.");
        }

        var animalList = animalsInBatch.ToList();
        if (animalList.Count == 0 || Items.Count == 0)
        {
            throw new DomainRuleException("Невозможно допустить пустую партию без животных/продукции.");
        }

        // Повторная строгая проверка каждого животного в момент подписания ВСД (на случай введения антибиотика после создания черновика!)
        foreach (var animal in animalList)
        {
            animal.EnsureEligibleForProductBatch(BatchType, approvalUtc);
        }

        Status = BatchStatus.Approved;
        ApprovedByVeterinarian = string.IsNullOrWhiteSpace(vetName) ? "Главный ветврач" : vetName.Trim();
        ApprovedAtUtc = approvalUtc;
    }

    public void MarkShipped()
    {
        if (Status != BatchStatus.Approved)
        {
            throw new DomainRuleException("Отгрузка возможна только после официального допуска партии ветеринарным врачом!");
        }

        Status = BatchStatus.Shipped;
    }
}

public class ProductBatchItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductBatchId { get; set; }
    public ProductBatch? ProductBatch { get; set; }

    public Guid AnimalId { get; set; }
    public Animal? Animal { get; set; }

    public decimal QuantityLitersOrKg { get; set; }
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Журнал инцидентов срабатывания «Карантинного замка» (попытки сдачи запрещенного молока или мяса).
/// Фиксируется в отдельной транзакции аудита даже при откате транзакции товарной партии.
/// </summary>
public class QuarantineBlockLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime AttemptedAtUtc { get; set; } = DateTime.UtcNow;
    public string AttemptedBatchNumber { get; set; } = string.Empty;
    public ProductBatchType BatchType { get; set; }
    public Guid AnimalId { get; set; }
    public string AnimalTag { get; set; } = string.Empty;
    public string AnimalName { get; set; } = string.Empty;
    public string? ActiveDrugName { get; set; }
    public string? ActiveDrugSeries { get; set; }
    public double RemainingHours { get; set; }
    public string AttemptedByUser { get; set; } = string.Empty;
    public string BlockMessage { get; set; } = string.Empty;
}
