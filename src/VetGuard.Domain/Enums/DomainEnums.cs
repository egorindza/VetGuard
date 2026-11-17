namespace VetGuard.Domain.Enums;

public enum AnimalSpecies
{
    Cattle = 1,
    Sheep = 2,
    Horse = 3,
    Swine = 4
}

public enum AnimalSex
{
    Female = 1,
    Male = 2
}

public enum HealthStatus
{
    Healthy = 1,
    UnderTreatment = 2,
    Quarantine = 3,
    Observation = 4,
    Culled = 5
}

public enum DrugCategory
{
    Antibiotic = 1,
    Antiparasitic = 2,
    Hormonal = 3,
    NSAID = 4,
    Vaccine = 5,
    DiagnosticAllergen = 6,
    Vitamin = 7
}

public enum ProductBatchType
{
    RawMilk = 1,
    MeatSlaughter = 2
}

public enum BatchStatus
{
    Draft = 1,
    Approved = 2,
    BlockedByQuarantine = 3,
    Shipped = 4
}

public enum EpizooticDiseaseType
{
    Anthrax = 1,
    Brucellosis = 2,
    Tuberculosis = 3,
    Leukosis = 4,
    FootAndMouth = 5,
    Rabies = 6,
    Leptospirosis = 7
}

public enum EpizooticEventType
{
    Vaccination = 1,
    DiagnosticTest = 2
}

public enum InbreedingRiskLevel
{
    Safe = 1,
    Moderate = 2,
    High = 3,
    Critical = 4
}

public enum BreedingPlanStatus
{
    Recommended = 1,
    ApprovedWithCaution = 2,
    Inseminated = 3,
    RejectedByInbreeding = 4
}

public static class DomainEnumExtensions
{
    public static string ToDisplayName(this AnimalSpecies species) => species switch
    {
        AnimalSpecies.Cattle => "КРС (Крупный рогатый скот)",
        AnimalSpecies.Sheep => "МРС (Овцы / Козы)",
        AnimalSpecies.Horse => "Лошади",
        AnimalSpecies.Swine => "Свиньи",
        _ => species.ToString()
    };

    public static string ToDisplayName(this AnimalSex sex) => sex switch
    {
        AnimalSex.Female => "Самка (Корова / Тёлка)",
        AnimalSex.Male => "Самец (Бык-производитель / Бычок)",
        _ => sex.ToString()
    };

    public static string ToShortSex(this AnimalSex sex) => sex == AnimalSex.Female ? "♀ Самка" : "♂ Самец";

    public static string ToDisplayName(this HealthStatus status) => status switch
    {
        HealthStatus.Healthy => "Клинически здоров",
        HealthStatus.UnderTreatment => "На вет-лечении",
        HealthStatus.Quarantine => "Карантинный изолятор",
        HealthStatus.Observation => "Под наблюдением",
        HealthStatus.Culled => "Выбракован",
        _ => status.ToString()
    };

    public static string ToDisplayName(this DrugCategory category) => category switch
    {
        DrugCategory.Antibiotic => "Антибиотик",
        DrugCategory.Antiparasitic => "Противопаразитарный",
        DrugCategory.Hormonal => "Гормональный",
        DrugCategory.NSAID => "НПВС (Противовоспалительный)",
        DrugCategory.Vaccine => "Вакцина",
        DrugCategory.DiagnosticAllergen => "Диагностикум / Аллерген",
        DrugCategory.Vitamin => "Витаминно-минеральный",
        _ => category.ToString()
    };

    public static string ToDisplayName(this ProductBatchType type) => type switch
    {
        ProductBatchType.RawMilk => "Товарное молоко (сдача на молзавод)",
        ProductBatchType.MeatSlaughter => "Отгрузка на убой / Мясосырьё",
        _ => type.ToString()
    };

    public static string ToDisplayName(this BatchStatus status) => status switch
    {
        BatchStatus.Draft => "Формируется",
        BatchStatus.Approved => "Допущена ветврачом",
        BatchStatus.BlockedByQuarantine => "Заблокирована карантином",
        BatchStatus.Shipped => "Отгружена",
        _ => status.ToString()
    };

    public static string ToDisplayName(this EpizooticDiseaseType disease) => disease switch
    {
        EpizooticDiseaseType.Anthrax => "Сибирская язва",
        EpizooticDiseaseType.Brucellosis => "Бруцеллёз КРС",
        EpizooticDiseaseType.Tuberculosis => "Туберкулёз (туберкулинизация)",
        EpizooticDiseaseType.Leukosis => "Лейкоз КРС",
        EpizooticDiseaseType.FootAndMouth => "Ящур",
        EpizooticDiseaseType.Rabies => "Бешенство",
        EpizooticDiseaseType.Leptospirosis => "Лептоспироз",
        _ => disease.ToString()
    };

    public static string ToDisplayName(this EpizooticEventType eventType) => eventType switch
    {
        EpizooticEventType.Vaccination => "Профилактическая вакцинация",
        EpizooticEventType.DiagnosticTest => "Диагностическое исследование",
        _ => eventType.ToString()
    };

    public static string ToDisplayName(this InbreedingRiskLevel level) => level switch
    {
        InbreedingRiskLevel.Safe => "Безопасно (Аутбридинг)",
        InbreedingRiskLevel.Moderate => "Умеренный кросс / допустимый лайнбридинг",
        InbreedingRiskLevel.High => "Опасно (Близкий инбридинг)",
        InbreedingRiskLevel.Critical => "Критично (Кровосмешение — ЗАПРЕТ)",
        _ => level.ToString()
    };
}
