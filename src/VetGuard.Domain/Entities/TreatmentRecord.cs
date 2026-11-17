using VetGuard.Domain.Enums;
using VetGuard.Domain.Exceptions;

namespace VetGuard.Domain.Entities;

/// <summary>
/// Запись журнала лечебно-профилактических обработок.
/// В MS SQL Server таблица TreatmentRecords сконфигурирована как Temporal Table (SYSTEM_VERSIONING = ON)
/// для неизменяемого государственного ветеринарного аудита введения сильнодействующих препаратов и антибиотиков.
/// </summary>
public class TreatmentRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AnimalId { get; set; }
    public Animal? Animal { get; set; }

    public Guid VetDrugId { get; set; }
    public VetDrug? VetDrug { get; set; }

    public string DrugNameSnapshot { get; set; } = string.Empty;
    public string DrugBatchSeriesSnapshot { get; set; } = string.Empty;
    public bool IsPotentDrugSnapshot { get; set; }

    public string Diagnosis { get; set; } = string.Empty;
    public decimal DosageAmount { get; set; }
    public string DosageUnit { get; set; } = "мл";
    public string AdministrationRoute { get; set; } = "внутримышечно";

    public DateTime AdministeredAtUtc { get; set; }
    public int MilkWithdrawalHoursApplied { get; set; }
    public int MeatWithdrawalDaysApplied { get; set; }
    public DateTime MilkWithdrawalEndsAtUtc { get; set; }
    public DateTime MeatWithdrawalEndsAtUtc { get; set; }

    public string VeterinarianName { get; set; } = string.Empty;
    public string VeterinarianRole { get; set; } = "Главный ветврач";
    public string? ClinicalNotes { get; set; }

    public bool IsClearedNotificationSent { get; set; }

    public bool IsMilkWithdrawalActive(DateTime asOfUtc)
        => MilkWithdrawalHoursApplied > 0 && MilkWithdrawalEndsAtUtc > asOfUtc;

    public bool IsMeatWithdrawalActive(DateTime asOfUtc)
        => MeatWithdrawalDaysApplied > 0 && MeatWithdrawalEndsAtUtc > asOfUtc;

    public static TreatmentRecord Create(
        Animal animal,
        VetDrug drug,
        decimal dosageAmount,
        string administrationRoute,
        string diagnosis,
        DateTime administeredAtUtc,
        string veterinarianName,
        string veterinarianRole = "Главный ветврач",
        string? clinicalNotes = null)
    {
        if (animal.IsHistoricalPedigreeOnly)
        {
            throw new DomainRuleException("Нельзя назначить ветпрепарат архивному предку племенной книги.");
        }

        if (string.IsNullOrWhiteSpace(diagnosis))
        {
            throw new DomainRuleException("Диагноз или показание к применению ветпрепарата обязательны.");
        }

        drug.EnsureValidForAdministration(administeredAtUtc);
        drug.DeductStock(dosageAmount);

        var milkEndUtc = drug.CalculateMilkWithdrawalEndUtc(administeredAtUtc);
        var meatEndUtc = drug.CalculateMeatWithdrawalEndUtc(administeredAtUtc);

        if ((drug.MilkWithdrawalHours > 0 || drug.MeatWithdrawalDays > 0 || drug.Category == DrugCategory.Antibiotic)
            && animal.HealthStatus == HealthStatus.Healthy)
        {
            animal.ChangeHealthStatus(HealthStatus.UnderTreatment, diagnosis);
        }

        var record = new TreatmentRecord
        {
            AnimalId = animal.Id,
            Animal = animal,
            VetDrugId = drug.Id,
            VetDrug = drug,
            DrugNameSnapshot = drug.Name,
            DrugBatchSeriesSnapshot = drug.BatchSeries,
            IsPotentDrugSnapshot = drug.IsPotent,
            Diagnosis = diagnosis.Trim(),
            DosageAmount = dosageAmount,
            DosageUnit = drug.Unit,
            AdministrationRoute = string.IsNullOrWhiteSpace(administrationRoute) ? "внутримышечно" : administrationRoute.Trim(),
            AdministeredAtUtc = administeredAtUtc,
            MilkWithdrawalHoursApplied = drug.MilkWithdrawalHours,
            MeatWithdrawalDaysApplied = drug.MeatWithdrawalDays,
            MilkWithdrawalEndsAtUtc = milkEndUtc,
            MeatWithdrawalEndsAtUtc = meatEndUtc,
            VeterinarianName = string.IsNullOrWhiteSpace(veterinarianName) ? "Дежурный ветврач" : veterinarianName.Trim(),
            VeterinarianRole = veterinarianRole,
            ClinicalNotes = clinicalNotes?.Trim(),
            IsClearedNotificationSent = drug.MilkWithdrawalHours == 0 && drug.MeatWithdrawalDays == 0
        };

        animal.Treatments.Add(record);
        return record;
    }
}
