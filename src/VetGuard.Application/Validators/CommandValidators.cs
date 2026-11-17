using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using VetGuard.Application.Models;

namespace VetGuard.Application.Validators;

public sealed class CreateOrUpdateAnimalCommandValidator : AbstractValidator<CreateOrUpdateAnimalCommand>
{
    public CreateOrUpdateAnimalCommandValidator()
    {
        RuleFor(x => x.TagNumber)
            .NotEmpty().WithMessage("Укажите уникальный инвентарный номер бирки (напр. RU-38-IRGAU-0125).")
            .MinimumLength(4).WithMessage("Номер бирки должен содержать минимум 4 символа.")
            .MaximumLength(32).WithMessage("Номер бирки не должен превышать 32 символа.");

        RuleFor(x => x.RfidChip)
            .Must(rfid => string.IsNullOrWhiteSpace(rfid) || Regex.IsMatch(rfid.Trim(), @"^\d{10,15}$"))
            .WithMessage("RFID-чип должен состоять из 10–15 цифр (стандарт ISO 11784/11785) либо быть пустым.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Укажите кличку животного.")
            .MaximumLength(80);

        RuleFor(x => x.Breed)
            .NotEmpty().WithMessage("Укажите породу животного.")
            .MaximumLength(80);

        RuleFor(x => x.BirthDate)
            .LessThanOrEqualTo(_ => DateTime.UtcNow.Date)
            .WithMessage("Дата рождения животного не может быть в будущем.");

        RuleFor(x => x.BarnName)
            .NotEmpty().WithMessage("Укажите производственный корпус/ферму.")
            .MaximumLength(100);

        RuleFor(x => x.SectionCode)
            .NotEmpty().WithMessage("Укажите секцию или номер стойла.")
            .MaximumLength(80);

        RuleFor(x => x.CurrentWeightKg)
            .GreaterThan(15m).WithMessage("Живая масса должна быть больше 15 кг.")
            .LessThanOrEqualTo(1600m).WithMessage("Живая масса не может превышать 1600 кг.");

        RuleFor(x => x)
            .Must(x => !x.SireId.HasValue || !x.DamId.HasValue || x.SireId != x.DamId)
            .WithMessage("Отец и мать не могут совпадать.");
    }
}

public sealed class CreateVetDrugCommandValidator : AbstractValidator<CreateVetDrugCommand>
{
    public CreateVetDrugCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Введите торговое наименование ветпрепарата.")
            .MaximumLength(150);

        RuleFor(x => x.ActiveSubstance)
            .NotEmpty().WithMessage("Укажите действующее вещество (МНН).")
            .MaximumLength(150);

        RuleFor(x => x.Manufacturer)
            .NotEmpty().WithMessage("Укажите производителя биопрепарата.")
            .MaximumLength(120);

        RuleFor(x => x.BatchSeries)
            .NotEmpty().WithMessage("Укажите номер заводской серии препарата.")
            .MaximumLength(50);

        RuleFor(x => x.MilkWithdrawalHours)
            .GreaterThanOrEqualTo(0).WithMessage("Срок ожидания по молоку (в часах) не может быть отрицательным.")
            .LessThanOrEqualTo(2400).WithMessage("Срок ожидания по молоку превышает допустимый предел.");

        RuleFor(x => x.MeatWithdrawalDays)
            .GreaterThanOrEqualTo(0).WithMessage("Срок ожидания по мясу (в сутках) не может быть отрицательным.")
            .LessThanOrEqualTo(365).WithMessage("Срок ожидания по мясу не может превышать 365 суток.");

        RuleFor(x => x.StockDoses)
            .GreaterThanOrEqualTo(0).WithMessage("Остаток препарата на складе не может быть отрицательным.");
    }
}

public sealed class RegisterTreatmentCommandValidator : AbstractValidator<RegisterTreatmentCommand>
{
    public RegisterTreatmentCommandValidator()
    {
        RuleFor(x => x.AnimalId)
            .NotEmpty().WithMessage("Выберите животное для проведения ветеринарной обработки.");

        RuleFor(x => x.VetDrugId)
            .NotEmpty().WithMessage("Выберите ветпрепарат из справочника.");

        RuleFor(x => x.DosageAmount)
            .GreaterThan(0).WithMessage("Дозировка препарата должна быть больше нуля.")
            .LessThanOrEqualTo(500).WithMessage("Разовая дозировка превышает безопасный порог (500 ед.).");

        RuleFor(x => x.Diagnosis)
            .NotEmpty().WithMessage("Обязательно укажите клинический диагноз или цель введения препарата.")
            .MinimumLength(3).WithMessage("Диагноз должен содержать не менее 3 символов.")
            .MaximumLength(250);

        RuleFor(x => x.AdministrationRoute)
            .NotEmpty().WithMessage("Укажите путь введения препарата (в/м, п/к, интрацистернально и т.д.).");
    }
}

public sealed class CreateProductBatchCommandValidator : AbstractValidator<CreateProductBatchCommand>
{
    public CreateProductBatchCommandValidator()
    {
        RuleFor(x => x.BatchNumber)
            .NotEmpty().WithMessage("Укажите учетный номер товарной партии.")
            .MaximumLength(50);

        RuleFor(x => x.Destination)
            .NotEmpty().WithMessage("Укажите получателя (молзавод / переработчик).")
            .MaximumLength(160);

        RuleFor(x => x.Contributions)
            .NotEmpty().WithMessage("Выберите хотя бы одно животное для формирования товарной партии.");

        RuleForEach(x => x.Contributions).ChildRules(item =>
        {
            item.RuleFor(c => c.AnimalId).NotEmpty();
            item.RuleFor(c => c.QuantityLitersOrKg)
                .GreaterThan(0).WithMessage("Объем/масса от каждого животного должна быть больше нуля.");
        });
    }
}

public sealed class ExecuteGroupEpizooticActCommandValidator : AbstractValidator<ExecuteGroupEpizooticActCommand>
{
    public ExecuteGroupEpizooticActCommandValidator()
    {
        RuleFor(x => x.RuleId).NotEmpty().WithMessage("Выберите мероприятие противоэпизоотического плана.");
        RuleFor(x => x.VetDrugId).NotEmpty().WithMessage("Выберите вакцину или диагностический аллерген со склада.");
        RuleFor(x => x.AnimalIds)
            .NotEmpty().WithMessage("Отметьте хотя бы одно животное для групповой обработки.");
        RuleFor(x => x.DosePerAnimal)
            .GreaterThan(0).WithMessage("Расход биопрепарата на 1 голову должен быть больше нуля.");
    }
}

public sealed class MobileVetInspectionCommandValidator : AbstractValidator<MobileVetInspectionCommand>
{
    public MobileVetInspectionCommandValidator()
    {
        RuleFor(x => x.AnimalId).NotEmpty();
        RuleFor(x => x.BodyTemperatureC)
            .InclusiveBetween(34.0m, 43.0m)
            .WithMessage("Температура тела КРС должна находиться в физиологически измеримом диапазоне 34.0–43.0 °C.");
        RuleFor(x => x.HeartRateBpm)
            .InclusiveBetween(20, 180)
            .WithMessage("Частота пульса должна быть в диапазоне 20–180 уд./мин.");
        RuleFor(x => x.Summary)
            .NotEmpty().WithMessage("Кратко зафиксируйте клиническое заключение осмотра.")
            .MaximumLength(500);
    }
}

public sealed class SaveBreedingPlanCommandValidator : AbstractValidator<SaveBreedingPlanCommand>
{
    public SaveBreedingPlanCommandValidator()
    {
        RuleFor(x => x.DamId).NotEmpty().WithMessage("Выберите матку (корову/тёлку).");
        RuleFor(x => x.SireId).NotEmpty().WithMessage("Выберите быка-производителя.");
        RuleFor(x => x)
            .Must(x => x.DamId != x.SireId)
            .WithMessage("Некорректное сочетание родительской пары.");
    }
}

public static class ApplicationDependencyInjection
{
    public static IServiceCollection AddVetGuardApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<CreateOrUpdateAnimalCommandValidator>();
        return services;
    }
}
