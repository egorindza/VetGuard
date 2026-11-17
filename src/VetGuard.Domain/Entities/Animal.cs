using VetGuard.Domain.Enums;
using VetGuard.Domain.Exceptions;

namespace VetGuard.Domain.Entities;

public class Animal
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Уникальный инвентарный ушной номер бирки (напр. RU-38-IRGAU-0104).
    /// </summary>
    public string TagNumber { get; set; } = string.Empty;

    /// <summary>
    /// Электронный транспондер ISO 11784/11785 (15 цифр) или пустой для архивных предков родословной.
    /// </summary>
    public string? RfidChip { get; set; }

    public string Name { get; set; } = string.Empty;
    public AnimalSpecies Species { get; set; } = AnimalSpecies.Cattle;
    public string Breed { get; set; } = "Голштинская";
    public AnimalSex Sex { get; set; }
    public DateTime BirthDate { get; set; }

    public Guid? SireId { get; set; }
    public Animal? Sire { get; set; }

    public Guid? DamId { get; set; }
    public Animal? Dam { get; set; }

    /// <summary>
    /// Признак архивного предка в племенной книге (не состоит в текущем физическом стаде фермы, но участвует в родословной до 6 колен).
    /// </summary>
    public bool IsHistoricalPedigreeOnly { get; set; }

    public string BarnName { get; set; } = "Учебный корпус №1 (Молочный)";
    public string SectionCode { get; set; } = "Секция А / Стойло 01";
    public string? LineageCode { get; set; }

    public HealthStatus HealthStatus { get; set; } = HealthStatus.Healthy;
    public string? QuarantineReason { get; set; }
    public DateTime? QuarantineEnteredAtUtc { get; set; }
    public string? PreviousBarnBeforeQuarantine { get; set; }

    public decimal CurrentWeightKg { get; set; }
    public decimal? AverageDailyMilkKg { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optimistic Concurrency токен (SQL Server rowversion).
    /// Также таблица Animals версионируется через SQL Server SYSTEM_VERSIONING = ON.
    /// </summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<TreatmentRecord> Treatments { get; set; } = new List<TreatmentRecord>();
    public ICollection<MilkYieldRecord> MilkYields { get; set; } = new List<MilkYieldRecord>();
    public ICollection<WeightRecord> WeightRecords { get; set; } = new List<WeightRecord>();
    public ICollection<VetInspection> Inspections { get; set; } = new List<VetInspection>();
    public ICollection<EpizooticActItem> EpizooticItems { get; set; } = new List<EpizooticActItem>();

    public int GetAgeDays(DateTime asOfUtc) => Math.Max(0, (int)(asOfUtc.Date - BirthDate.Date).TotalDays);

    public string GetFormattedAge(DateTime asOfUtc)
    {
        var days = GetAgeDays(asOfUtc);
        var years = days / 365;
        var months = (days % 365) / 30;
        if (years > 0)
        {
            return $"{years} г. {months} мес.";
        }

        return months > 0 ? $"{months} мес. ({days} дн.)" : $"{days} дн.";
    }

    public void ValidateParentage(Animal? sire, Animal? dam)
    {
        if (sire is not null)
        {
            if (sire.Id == Id)
            {
                throw new DomainRuleException("Животное не может быть собственным отцом.");
            }

            if (sire.Sex != AnimalSex.Male)
            {
                throw new DomainRuleException($"Выбранный отец {sire.Name} [{sire.TagNumber}] должен иметь мужской пол (бык-производитель).");
            }

            if (sire.BirthDate.Date >= BirthDate.Date)
            {
                throw new DomainRuleException($"Отец {sire.Name} не может быть рожден позже или в один день с потомком.");
            }
        }

        if (dam is not null)
        {
            if (dam.Id == Id)
            {
                throw new DomainRuleException("Животное не может быть собственной матерью.");
            }

            if (dam.Sex != AnimalSex.Female)
            {
                throw new DomainRuleException($"Выбранная мать {dam.Name} [{dam.TagNumber}] должна иметь женский пол.");
            }

            if (dam.BirthDate.Date >= BirthDate.Date)
            {
                throw new DomainRuleException($"Мать {dam.Name} не может быть рождена позже или в один день с потомком.");
            }
        }
    }

    public void ChangeHealthStatus(HealthStatus newStatus, string? reason = null)
    {
        HealthStatus = newStatus;
        if (newStatus == HealthStatus.Quarantine)
        {
            QuarantineReason = string.IsNullOrWhiteSpace(reason) ? "Ветсаннадзор / Изоляция" : reason.Trim();
            QuarantineEnteredAtUtc ??= DateTime.UtcNow;
        }
        else if (newStatus == HealthStatus.Healthy)
        {
            QuarantineReason = null;
            QuarantineEnteredAtUtc = null;
        }

        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void PlaceInQuarantineIsolator(string reason, string isolatorSection = "Бокс №1 (Строгая изоляция)")
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainRuleException("При переводе в карантинный изолятор обязательно указание эпизоотической/клинической причины.");
        }

        if (BarnName != "Карантинный изолятор")
        {
            PreviousBarnBeforeQuarantine = $"{BarnName} | {SectionCode}";
        }

        BarnName = "Карантинный изолятор";
        SectionCode = isolatorSection;
        HealthStatus = HealthStatus.Quarantine;
        QuarantineReason = reason.Trim();
        QuarantineEnteredAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ReleaseFromQuarantineIsolator(string? returnBarn = null, string? returnSection = null)
    {
        if (!string.IsNullOrWhiteSpace(returnBarn))
        {
            BarnName = returnBarn;
            SectionCode = string.IsNullOrWhiteSpace(returnSection) ? "Секция А / Стойло 01" : returnSection;
        }
        else if (!string.IsNullOrWhiteSpace(PreviousBarnBeforeQuarantine))
        {
            var parts = PreviousBarnBeforeQuarantine.Split('|', StringSplitOptions.TrimEntries);
            BarnName = parts[0];
            if (parts.Length > 1)
            {
                SectionCode = parts[1];
            }
        }
        else
        {
            BarnName = "Учебный корпус №1 (Молочный)";
            SectionCode = "Секция А / Стойло 05";
        }

        PreviousBarnBeforeQuarantine = null;
        QuarantineReason = null;
        QuarantineEnteredAtUtc = null;
        HealthStatus = HealthStatus.Healthy;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Возвращает активную запись лечения с максимальным остатком карантина по молоку (если есть).
    /// </summary>
    public TreatmentRecord? GetActiveMilkWithdrawal(DateTime checkUtc)
    {
        return Treatments
            .Where(t => t.MilkWithdrawalHoursApplied > 0 && t.MilkWithdrawalEndsAtUtc > checkUtc)
            .OrderByDescending(t => t.MilkWithdrawalEndsAtUtc)
            .FirstOrDefault();
    }

    /// <summary>
    /// Возвращает активную запись лечения с максимальным остатком карантина по мясу/убою (если есть).
    /// </summary>
    public TreatmentRecord? GetActiveMeatWithdrawal(DateTime checkUtc)
    {
        return Treatments
            .Where(t => t.MeatWithdrawalDaysApplied > 0 && t.MeatWithdrawalEndsAtUtc > checkUtc)
            .OrderByDescending(t => t.MeatWithdrawalEndsAtUtc)
            .FirstOrDefault();
    }

    public bool HasAnyActiveQuarantineLock(DateTime checkUtc)
    {
        return HealthStatus == HealthStatus.Quarantine
               || HealthStatus == HealthStatus.Culled
               || GetActiveMilkWithdrawal(checkUtc) is not null
               || GetActiveMeatWithdrawal(checkUtc) is not null;
    }

    /// <summary>
    /// Жесткий доменный инвариант «Карантинный замок»: проверяет допустимость включения продукции животного
    /// в товарную партию молока или отгрузку на убой. При любом нарушении выбрасывает QuarantineLockDomainException.
    /// </summary>
    public void EnsureEligibleForProductBatch(ProductBatchType batchType, DateTime checkUtc)
    {
        if (IsHistoricalPedigreeOnly)
        {
            throw new QuarantineLockDomainException(
                Id, TagNumber, Name, batchType,
                "Архивное племенное животное не состоит в действующем производственном стаде.");
        }

        if (HealthStatus == HealthStatus.Culled)
        {
            throw new QuarantineLockDomainException(
                Id, TagNumber, Name, batchType,
                "Животное выбраковано из производственного стада.");
        }

        if (HealthStatus == HealthStatus.Quarantine)
        {
            throw new QuarantineLockDomainException(
                Id, TagNumber, Name, batchType,
                $"Животное изолировано в карантинном изоляторе ({QuarantineReason ?? "ветсаннадзор"}). Реализация сырья строго запрещена!");
        }

        if (batchType == ProductBatchType.RawMilk)
        {
            if (Sex != AnimalSex.Female)
            {
                throw new QuarantineLockDomainException(
                    Id, TagNumber, Name, batchType,
                    "Самец (бык) не может быть включен в партию товарного молока.");
            }

            var activeMilkLock = GetActiveMilkWithdrawal(checkUtc);
            if (activeMilkLock is not null)
            {
                var remaining = activeMilkLock.MilkWithdrawalEndsAtUtc - checkUtc;
                var drugTitle = activeMilkLock.VetDrug?.Name ?? activeMilkLock.DrugNameSnapshot;
                throw new QuarantineLockDomainException(
                    Id,
                    TagNumber,
                    Name,
                    batchType,
                    $"Не завершен период выведения ветпрепарата «{drugTitle}» из молока",
                    drugName: drugTitle,
                    drugSeries: activeMilkLock.DrugBatchSeriesSnapshot,
                    clearanceUtc: activeMilkLock.MilkWithdrawalEndsAtUtc,
                    remainingDuration: remaining);
            }
        }
        else if (batchType == ProductBatchType.MeatSlaughter)
        {
            var activeMeatLock = GetActiveMeatWithdrawal(checkUtc);
            if (activeMeatLock is not null)
            {
                var remaining = activeMeatLock.MeatWithdrawalEndsAtUtc - checkUtc;
                var drugTitle = activeMeatLock.VetDrug?.Name ?? activeMeatLock.DrugNameSnapshot;
                throw new QuarantineLockDomainException(
                    Id,
                    TagNumber,
                    Name,
                    batchType,
                    $"Не завершен период выведения ветпрепарата «{drugTitle}» из мышечной ткани и субпродуктов",
                    drugName: drugTitle,
                    drugSeries: activeMeatLock.DrugBatchSeriesSnapshot,
                    clearanceUtc: activeMeatLock.MeatWithdrawalEndsAtUtc,
                    remainingDuration: remaining);
            }
        }
    }
}
