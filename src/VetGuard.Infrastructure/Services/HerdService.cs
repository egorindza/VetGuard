using FluentValidation;
using Microsoft.EntityFrameworkCore;
using VetGuard.Application.Interfaces;
using VetGuard.Application.Models;
using VetGuard.Domain.Entities;
using VetGuard.Domain.Enums;
using VetGuard.Domain.Exceptions;
using VetGuard.Infrastructure.Persistence;

namespace VetGuard.Infrastructure.Services;

public sealed class HerdService : IHerdService
{
    private readonly VetGuardDbContext _db;
    private readonly IQrCodeService _qrCodeService;
    private readonly IValidator<CreateOrUpdateAnimalCommand> _animalValidator;
    private readonly IValidator<MobileVetInspectionCommand> _inspectionValidator;

    public HerdService(
        VetGuardDbContext db,
        IQrCodeService qrCodeService,
        IValidator<CreateOrUpdateAnimalCommand> animalValidator,
        IValidator<MobileVetInspectionCommand> inspectionValidator)
    {
        _db = db;
        _qrCodeService = qrCodeService;
        _animalValidator = animalValidator;
        _inspectionValidator = inspectionValidator;
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;
        var activeAnimals = await _db.Animals
            .AsNoTracking()
            .Include(a => a.Treatments)
            .ThenInclude(t => t.VetDrug)
            .Include(a => a.EpizooticItems)
            .ThenInclude(i => i.EpizooticAct)
            .Where(a => !a.IsHistoricalPedigreeOnly && a.HealthStatus != HealthStatus.Culled)
            .ToListAsync(ct);

        var rules = await _db.EpizooticRules.AsNoTracking().ToListAsync(ct);

        var timers = new List<ActiveQuarantineTimerDto>();
        foreach (var animal in activeAnimals)
        {
            var milkLock = animal.GetActiveMilkWithdrawal(nowUtc);
            var meatLock = animal.GetActiveMeatWithdrawal(nowUtc);
            var primary = milkLock ?? meatLock;
            if (primary is not null)
            {
                var remMilkHours = milkLock is not null
                    ? Math.Max(0, (milkLock.MilkWithdrawalEndsAtUtc - nowUtc).TotalHours)
                    : 0;
                var remMeatDays = meatLock is not null
                    ? Math.Max(0, (meatLock.MeatWithdrawalEndsAtUtc - nowUtc).TotalDays)
                    : 0;

                timers.Add(new ActiveQuarantineTimerDto(
                    animal.Id,
                    animal.TagNumber,
                    animal.Name,
                    animal.BarnName,
                    primary.DrugNameSnapshot,
                    primary.DrugBatchSeriesSnapshot,
                    primary.AdministeredAtUtc,
                    milkLock?.MilkWithdrawalEndsAtUtc ?? primary.MilkWithdrawalEndsAtUtc,
                    meatLock?.MeatWithdrawalEndsAtUtc ?? primary.MeatWithdrawalEndsAtUtc,
                    milkLock is not null,
                    meatLock is not null,
                    remMilkHours,
                    remMeatDays));
            }
        }

        var overdueTasks = 0;
        foreach (var rule in rules)
        {
            foreach (var animal in activeAnimals.Where(a => a.GetAgeDays(nowUtc) >= rule.MinAgeDays))
            {
                var lastExecution = animal.EpizooticItems
                    .Where(i => i.EpizooticAct != null && i.EpizooticAct.EpizooticRuleId == rule.Id)
                    .Select(i => (DateTime?)i.EpizooticAct!.ExecutedAtUtc)
                    .Max();

                var dueDate = lastExecution.HasValue
                    ? lastExecution.Value.AddDays(rule.IntervalDays)
                    : nowUtc.AddDays(-1);

                if (dueDate <= nowUtc)
                {
                    overdueTasks++;
                }
            }
        }

        var recentBlocks = await _db.QuarantineBlockLogs
            .AsNoTracking()
            .OrderByDescending(x => x.AttemptedAtUtc)
            .Take(8)
            .ToListAsync(ct);

        var recentTreatments = await _db.TreatmentRecords
            .AsNoTracking()
            .Include(t => t.Animal)
            .OrderByDescending(t => t.AdministeredAtUtc)
            .Take(8)
            .ToListAsync(ct);

        var latestMilkDate = await _db.MilkYieldRecords
            .AsNoTracking()
            .MaxAsync(m => (DateTime?)m.MilkingDate, ct);

        var safeMilkLiters = 0m;
        if (latestMilkDate.HasValue)
        {
            var rows = await _db.MilkYieldRecords
                .AsNoTracking()
                .Where(m => m.MilkingDate == latestMilkDate.Value && !m.IsQuarantineDiscarded)
                .ToListAsync(ct);
            safeMilkLiters = rows.Sum(r => r.MorningLiters + r.EveningLiters);
        }

        var lockedAnimalCount = activeAnimals.Count(a => a.HasAnyActiveQuarantineLock(nowUtc));
        var isolatorCount = activeAnimals.Count(a => a.HealthStatus == HealthStatus.Quarantine || a.BarnName == "Карантинный изолятор");
        var milkingCows = activeAnimals.Count(a => a.Sex == AnimalSex.Female && a.GetAgeDays(nowUtc) >= 650);
        var totalBlocksCount = await _db.QuarantineBlockLogs.CountAsync(ct);

        return new DashboardSummaryDto(
            TotalActiveLivestock: activeAnimals.Count,
            MilkingCowsCount: milkingCows,
            ActiveQuarantineLocksCount: lockedAnimalCount,
            QuarantineIsolatorCount: isolatorCount,
            OverdueEpizooticTasksCount: overdueTasks,
            BlockedBatchAttemptsCount: totalBlocksCount,
            TodayTotalSafeMilkLiters: safeMilkLiters,
            ActiveTimers: timers.OrderBy(t => t.MilkWithdrawalEndsAtUtc).ToList(),
            RecentBlockedAttempts: recentBlocks,
            RecentTreatments: recentTreatments);
    }

    public async Task<IReadOnlyList<AnimalListItemDto>> GetAnimalsAsync(AnimalFilterQuery filter, CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;
        var query = _db.Animals
            .AsNoTracking()
            .Include(a => a.Treatments)
            .Where(a => !a.IsHistoricalPedigreeOnly);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            query = query.Where(a =>
                a.TagNumber.Contains(s) ||
                a.Name.Contains(s) ||
                (a.RfidChip != null && a.RfidChip.Contains(s)) ||
                (a.LineageCode != null && a.LineageCode.Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Breed))
        {
            query = query.Where(a => a.Breed == filter.Breed);
        }

        if (filter.Sex.HasValue)
        {
            query = query.Where(a => a.Sex == filter.Sex.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.BarnName))
        {
            query = query.Where(a => a.BarnName == filter.BarnName);
        }

        if (filter.HealthStatus.HasValue)
        {
            query = query.Where(a => a.HealthStatus == filter.HealthStatus.Value);
        }

        var animals = await query
            .OrderBy(a => a.TagNumber)
            .ToListAsync(ct);

        var items = animals.Select(a =>
        {
            var milkLock = a.GetActiveMilkWithdrawal(nowUtc);
            var meatLock = a.GetActiveMeatWithdrawal(nowUtc);
            var remHours = milkLock is not null
                ? Math.Max(0, (milkLock.MilkWithdrawalEndsAtUtc - nowUtc).TotalHours)
                : 0;

            return new AnimalListItemDto(
                a.Id,
                a.TagNumber,
                a.RfidChip,
                a.Name,
                a.Breed,
                a.Sex,
                a.BirthDate,
                a.GetFormattedAge(nowUtc),
                a.BarnName,
                a.SectionCode,
                a.HealthStatus,
                a.CurrentWeightKg,
                a.AverageDailyMilkKg,
                IsMilkQuarantineLocked: milkLock is not null || a.HealthStatus == HealthStatus.Quarantine,
                IsMeatQuarantineLocked: meatLock is not null || a.HealthStatus == HealthStatus.Quarantine,
                ActiveLockDrugName: milkLock?.DrugNameSnapshot ?? meatLock?.DrugNameSnapshot ?? (a.HealthStatus == HealthStatus.Quarantine ? "Изолятор" : null),
                MilkLockEndsAtUtc: milkLock?.MilkWithdrawalEndsAtUtc,
                RemainingMilkLockHours: remHours);
        });

        if (filter.OnlyQuarantineLocked)
        {
            items = items.Where(i => i.IsMilkQuarantineLocked || i.IsMeatQuarantineLocked);
        }

        return items.ToList();
    }

    public async Task<IReadOnlyList<Animal>> GetBreedingFemalesAsync(CancellationToken ct = default)
    {
        return await _db.Animals
            .AsNoTracking()
            .Where(a => !a.IsHistoricalPedigreeOnly && a.Sex == AnimalSex.Female && a.HealthStatus != HealthStatus.Culled)
            .OrderBy(a => a.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Animal>> GetBreedingBullsAsync(CancellationToken ct = default)
    {
        return await _db.Animals
            .AsNoTracking()
            .Where(a => !a.IsHistoricalPedigreeOnly && a.Sex == AnimalSex.Male && a.HealthStatus != HealthStatus.Culled)
            .OrderBy(a => a.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Animal>> GetAllActiveAnimalsForSelectionAsync(CancellationToken ct = default)
    {
        return await _db.Animals
            .AsNoTracking()
            .Include(a => a.Treatments)
            .Where(a => !a.IsHistoricalPedigreeOnly && a.HealthStatus != HealthStatus.Culled)
            .OrderBy(a => a.TagNumber)
            .ToListAsync(ct);
    }

    public async Task<AnimalDetailsDto?> GetAnimalDetailsAsync(Guid id, string baseHostUrl, CancellationToken ct = default)
    {
        var animal = await _db.Animals
            .AsNoTracking()
            .Include(a => a.Sire)
            .Include(a => a.Dam)
            .Include(a => a.Treatments).ThenInclude(t => t.VetDrug)
            .Include(a => a.MilkYields)
            .Include(a => a.WeightRecords)
            .Include(a => a.Inspections)
            .Include(a => a.EpizooticItems).ThenInclude(ei => ei.EpizooticAct).ThenInclude(act => act!.EpizooticRule)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (animal is null)
        {
            return null;
        }

        return BuildAnimalDetailsDto(animal, baseHostUrl);
    }

    public async Task<AnimalDetailsDto?> GetAnimalByTagOrIdAsync(string tagOrId, string baseHostUrl, CancellationToken ct = default)
    {
        Guid.TryParse(tagOrId, out var parsedId);
        var normalized = tagOrId.Trim();

        var animal = await _db.Animals
            .AsNoTracking()
            .Include(a => a.Sire)
            .Include(a => a.Dam)
            .Include(a => a.Treatments).ThenInclude(t => t.VetDrug)
            .Include(a => a.MilkYields)
            .Include(a => a.WeightRecords)
            .Include(a => a.Inspections)
            .Include(a => a.EpizooticItems).ThenInclude(ei => ei.EpizooticAct).ThenInclude(act => act!.EpizooticRule)
            .FirstOrDefaultAsync(a =>
                (parsedId != Guid.Empty && a.Id == parsedId) ||
                a.TagNumber == normalized ||
                a.RfidChip == normalized, ct);

        return animal is null ? null : BuildAnimalDetailsDto(animal, baseHostUrl);
    }

    private AnimalDetailsDto BuildAnimalDetailsDto(Animal animal, string baseHostUrl)
    {
        var nowUtc = DateTime.UtcNow;
        var host = string.IsNullOrWhiteSpace(baseHostUrl) ? "http://localhost:5104" : baseHostUrl.TrimEnd('/');
        var passportUrl = $"{host}/Passport/{Uri.EscapeDataString(animal.TagNumber)}";
        var qrSvg = _qrCodeService.GenerateSvgQrCode(passportUrl);

        return new AnimalDetailsDto(
            Animal: animal,
            ActiveMilkLock: animal.GetActiveMilkWithdrawal(nowUtc),
            ActiveMeatLock: animal.GetActiveMeatWithdrawal(nowUtc),
            Treatments: animal.Treatments.OrderByDescending(t => t.AdministeredAtUtc).ToList(),
            MilkYields: animal.MilkYields.OrderBy(m => m.MilkingDate).ToList(),
            WeightRecords: animal.WeightRecords.OrderBy(w => w.MeasuredAtUtc).ToList(),
            Inspections: animal.Inspections.OrderByDescending(i => i.InspectedAtUtc).ToList(),
            EpizooticHistory: animal.EpizooticItems
                .OrderByDescending(e => e.EpizooticAct != null ? e.EpizooticAct.ExecutedAtUtc : DateTime.MinValue)
                .ToList(),
            QrCodeSvg: qrSvg,
            PassportUrl: passportUrl);
    }

    public async Task<Animal> CreateOrUpdateAnimalAsync(CreateOrUpdateAnimalCommand command, CancellationToken ct = default)
    {
        var validation = await _animalValidator.ValidateAsync(command, ct);
        if (!validation.IsValid)
        {
            throw new DomainRuleException(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var normalizedTag = command.TagNumber.Trim().ToUpperInvariant();
        var duplicateTag = await _db.Animals
            .AnyAsync(a => a.TagNumber == normalizedTag && a.Id != command.Id, ct);
        if (duplicateTag)
        {
            throw new DomainRuleException($"Животное с инвентарной биркой {normalizedTag} уже зарегистрировано в реестре ИрГАУ.");
        }

        var normalizedRfid = string.IsNullOrWhiteSpace(command.RfidChip) ? null : command.RfidChip.Trim();
        if (normalizedRfid is not null)
        {
            var duplicateRfid = await _db.Animals
                .AnyAsync(a => a.RfidChip == normalizedRfid && a.Id != command.Id, ct);
            if (duplicateRfid)
            {
                throw new DomainRuleException($"Транспондер RFID {normalizedRfid} уже закреплен за другим животным.");
            }
        }

        var sire = command.SireId.HasValue ? await _db.Animals.FindAsync(new object[] { command.SireId.Value }, ct) : null;
        var dam = command.DamId.HasValue ? await _db.Animals.FindAsync(new object[] { command.DamId.Value }, ct) : null;

        Animal animal;
        if (command.Id.HasValue)
        {
            animal = await _db.Animals.FirstOrDefaultAsync(a => a.Id == command.Id.Value, ct)
                ?? throw new DomainRuleException("Карточка животного не найдена.");
        }
        else
        {
            animal = new Animal { CreatedAtUtc = DateTime.UtcNow };
            _db.Animals.Add(animal);
        }

        animal.TagNumber = normalizedTag;
        animal.RfidChip = normalizedRfid;
        animal.Name = command.Name.Trim();
        animal.Species = command.Species;
        animal.Breed = command.Breed.Trim();
        animal.Sex = command.Sex;
        animal.BirthDate = command.BirthDate.Date;
        animal.SireId = command.SireId;
        animal.DamId = command.DamId;
        animal.BarnName = command.BarnName.Trim();
        animal.SectionCode = command.SectionCode.Trim();
        animal.LineageCode = string.IsNullOrWhiteSpace(command.LineageCode) ? null : command.LineageCode.Trim();
        animal.CurrentWeightKg = command.CurrentWeightKg;
        animal.AverageDailyMilkKg = command.Sex == AnimalSex.Female ? command.AverageDailyMilkKg : null;
        animal.Notes = command.Notes?.Trim();
        animal.UpdatedAtUtc = DateTime.UtcNow;

        animal.ValidateParentage(sire, dam);

        if (!command.Id.HasValue)
        {
            _db.WeightRecords.Add(new WeightRecord
            {
                AnimalId = animal.Id,
                MeasuredAtUtc = DateTime.UtcNow,
                WeightKg = animal.CurrentWeightKg,
                DailyGainGrams = 780,
                MeasuredBy = "Первичная бонитировка"
            });
        }

        await _db.SaveChangesAsync(ct);
        return animal;
    }

    public async Task LogMilkYieldAsync(LogMilkYieldCommand command, CancellationToken ct = default)
    {
        if (command.MorningLiters < 0 || command.EveningLiters < 0 || (command.MorningLiters + command.EveningLiters) <= 0)
        {
            throw new DomainRuleException("Суточный надой должен быть больше 0 литров.");
        }

        var animal = await _db.Animals
            .Include(a => a.Treatments)
            .Include(a => a.MilkYields)
            .FirstOrDefaultAsync(a => a.Id == command.AnimalId, ct)
            ?? throw new DomainRuleException("Животное не найдено.");

        if (animal.Sex != AnimalSex.Female)
        {
            throw new DomainRuleException("Контрольная дойка регистрируется только для коров.");
        }

        var nowUtc = DateTime.UtcNow;
        var isQuarantineDiscarded = animal.GetActiveMilkWithdrawal(nowUtc) is not null || animal.HealthStatus == HealthStatus.Quarantine;

        var record = new MilkYieldRecord
        {
            AnimalId = animal.Id,
            MilkingDate = command.MilkingDate.Date,
            MorningLiters = command.MorningLiters,
            EveningLiters = command.EveningLiters,
            FatPercent = command.FatPercent,
            ProteinPercent = command.ProteinPercent,
            IsQuarantineDiscarded = isQuarantineDiscarded,
            RecordedBy = string.IsNullOrWhiteSpace(command.RecordedBy) ? "Оператор доильного зала" : command.RecordedBy
        };

        _db.MilkYieldRecords.Add(record);

        var recent = animal.MilkYields
            .OrderByDescending(m => m.MilkingDate)
            .Take(6)
            .Select(m => m.MorningLiters + m.EveningLiters)
            .Append(command.MorningLiters + command.EveningLiters)
            .ToList();

        animal.AverageDailyMilkKg = Math.Round(recent.Average(), 2);
        animal.UpdatedAtUtc = nowUtc;

        await _db.SaveChangesAsync(ct);
    }

    public async Task LogWeightAsync(LogWeightCommand command, CancellationToken ct = default)
    {
        if (command.WeightKg <= 15m)
        {
            throw new DomainRuleException("Масса животного должна превышать 15 кг.");
        }

        var animal = await _db.Animals
            .Include(a => a.WeightRecords)
            .FirstOrDefaultAsync(a => a.Id == command.AnimalId, ct)
            ?? throw new DomainRuleException("Животное не найдено.");

        var previous = animal.WeightRecords
            .OrderByDescending(w => w.MeasuredAtUtc)
            .FirstOrDefault();

        var gainGrams = 0;
        if (previous is not null)
        {
            var days = Math.Max(1.0, (command.MeasuredAtUtc - previous.MeasuredAtUtc).TotalDays);
            gainGrams = (int)Math.Round(((double)(command.WeightKg - previous.WeightKg) * 1000.0) / days);
        }

        _db.WeightRecords.Add(new WeightRecord
        {
            AnimalId = animal.Id,
            MeasuredAtUtc = command.MeasuredAtUtc,
            WeightKg = command.WeightKg,
            DailyGainGrams = gainGrams,
            MeasuredBy = string.IsNullOrWhiteSpace(command.MeasuredBy) ? "Зоотехник-селекционер" : command.MeasuredBy
        });

        animal.CurrentWeightKg = command.WeightKg;
        animal.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task RecordVetInspectionAsync(MobileVetInspectionCommand command, CancellationToken ct = default)
    {
        var validation = await _inspectionValidator.ValidateAsync(command, ct);
        if (!validation.IsValid)
        {
            throw new DomainRuleException(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var animal = await _db.Animals.FirstOrDefaultAsync(a => a.Id == command.AnimalId, ct)
            ?? throw new DomainRuleException("Животное не найдено.");

        var statusToApply = command.MoveToQuarantineIsolator ? HealthStatus.Quarantine : command.NewHealthStatus;

        if (command.MoveToQuarantineIsolator)
        {
            var reason = string.IsNullOrWhiteSpace(command.QuarantineReason)
                ? $"Клинический осмотр по QR (t={command.BodyTemperatureC:0.0}°C): {command.Summary}"
                : command.QuarantineReason;
            animal.PlaceInQuarantineIsolator(reason);
        }
        else if (animal.HealthStatus == HealthStatus.Quarantine && statusToApply == HealthStatus.Healthy)
        {
            animal.ReleaseFromQuarantineIsolator();
        }
        else
        {
            animal.ChangeHealthStatus(statusToApply, command.QuarantineReason ?? command.Summary);
        }

        _db.VetInspections.Add(new VetInspection
        {
            AnimalId = animal.Id,
            InspectedAtUtc = DateTime.UtcNow,
            BodyTemperatureC = command.BodyTemperatureC,
            HeartRateBpm = command.HeartRateBpm,
            RuminationPer2Min = command.RuminationPer2Min,
            MucousMembranes = command.MucousMembranes,
            UdderCondition = command.UdderCondition,
            ResultHealthStatus = statusToApply,
            Summary = command.Summary.Trim(),
            InspectorName = string.IsNullOrWhiteSpace(command.InspectorName) ? "Дежурный ветврач" : command.InspectorName.Trim(),
            ScannedViaQrPassport = true
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task PlaceInQuarantineIsolatorAsync(Guid animalId, string reason, string isolatorBox, CancellationToken ct = default)
    {
        var animal = await _db.Animals.FirstOrDefaultAsync(a => a.Id == animalId, ct)
            ?? throw new DomainRuleException("Животное не найдено.");

        animal.PlaceInQuarantineIsolator(reason, string.IsNullOrWhiteSpace(isolatorBox) ? "Бокс №1 (Инфекционный фильтр)" : isolatorBox);
        await _db.SaveChangesAsync(ct);
    }

    public async Task ReleaseFromQuarantineIsolatorAsync(Guid animalId, CancellationToken ct = default)
    {
        var animal = await _db.Animals.FirstOrDefaultAsync(a => a.Id == animalId, ct)
            ?? throw new DomainRuleException("Животное не найдено.");

        animal.ReleaseFromQuarantineIsolator();
        await _db.SaveChangesAsync(ct);
    }
}
