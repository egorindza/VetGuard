using Microsoft.EntityFrameworkCore;
using VetGuard.Application.Interfaces;
using VetGuard.Application.Models;
using VetGuard.Application.Validators;
using VetGuard.Domain.Entities;
using VetGuard.Domain.Enums;
using VetGuard.Domain.Exceptions;
using VetGuard.Infrastructure.Persistence;
using VetGuard.Infrastructure.Services;

namespace VetGuard.Tests;

public sealed class QuarantineLockTests
{
    private static Animal CreateTestCow(string tag = "RU-38-TEST-001", string name = "Зорька-Тест")
    {
        return new Animal
        {
            TagNumber = tag,
            Name = name,
            Species = AnimalSpecies.Cattle,
            Breed = "Голштинская",
            Sex = AnimalSex.Female,
            BirthDate = DateTime.UtcNow.AddYears(-3),
            CurrentWeightKg = 580m,
            AverageDailyMilkKg = 28m,
            HealthStatus = HealthStatus.Healthy
        };
    }

    private static VetDrug CreateAntibiotic(int milkHours = 168, int meatDays = 28, decimal stock = 100m)
    {
        return new VetDrug
        {
            Name = "Нитокс 200",
            ActiveSubstance = "Окситетрациклин",
            Category = DrugCategory.Antibiotic,
            Manufacturer = "НИТА-ФАРМ",
            BatchSeries = "NTX-TEST-01",
            ExpirationDate = DateTime.UtcNow.Date.AddMonths(12),
            MilkWithdrawalHours = milkHours,
            MeatWithdrawalDays = meatDays,
            Unit = "мл",
            StockDoses = stock,
            IsPotent = true
        };
    }

    [Fact]
    public void EnsureEligibleForProductBatch_ShouldBlockMilk_WhenAntibioticWithdrawalIsActive()
    {
        var now = new DateTime(2026, 11, 18, 8, 0, 0, DateTimeKind.Utc);
        var cow = CreateTestCow();
        var nitox = CreateAntibiotic(milkHours: 168, meatDays: 28);

        TreatmentRecord.Create(
            cow, nitox, 50m, "внутримышечно",
            "Острый катаральный мастит",
            now.AddHours(-24),
            "Д-р Соколов А.В.");

        var ex = Assert.Throws<QuarantineLockDomainException>(() =>
            cow.EnsureEligibleForProductBatch(ProductBatchType.RawMilk, now));

        Assert.Equal("Нитокс 200", ex.DrugName);
        Assert.Equal("NTX-TEST-01", ex.DrugSeries);
        Assert.Equal(144.0, ex.RemainingHours, precision: 2);
        Assert.Contains("КАРАНТИННЫЙ ЗАМОК", ex.Message);
        Assert.Contains("144 ч", ex.Message);
    }

    [Fact]
    public void EnsureEligibleForProductBatch_ShouldAllowMilk_ButStillBlockMeat_WhenMilkPeriodExpired()
    {
        var now = new DateTime(2026, 11, 18, 8, 0, 0, DateTimeKind.Utc);
        var cow = CreateTestCow();
        var nitox = CreateAntibiotic(milkHours: 96, meatDays: 21);

        // Введено 100 часов назад: молоко (96 ч) уже очистилось, а мясо (21 сут = 504 ч) еще под карантином!
        TreatmentRecord.Create(
            cow, nitox, 45m, "внутримышечно",
            "Эндометрит",
            now.AddHours(-100),
            "Д-р Соколов А.В.");

        // Молоко разрешено
        var exception = Record.Exception(() => cow.EnsureEligibleForProductBatch(ProductBatchType.RawMilk, now));
        Assert.Null(exception);

        // Отгрузка на убой/мясо строго заблокирована
        var meatEx = Assert.Throws<QuarantineLockDomainException>(() =>
            cow.EnsureEligibleForProductBatch(ProductBatchType.MeatSlaughter, now));
        Assert.Equal("Нитокс 200", meatEx.DrugName);
        Assert.True(meatEx.RemainingHours > 350);
    }

    [Fact]
    public void EnsureEligibleForProductBatch_ShouldBlockAnimalInQuarantineIsolator_EvenWithoutDrugs()
    {
        var cow = CreateTestCow("RU-38-ISO-09", "Метелица-Тест");
        cow.PlaceInQuarantineIsolator("Подозрение на инфекционный ринотрахеит");

        var ex = Assert.Throws<QuarantineLockDomainException>(() =>
            cow.EnsureEligibleForProductBatch(ProductBatchType.RawMilk, DateTime.UtcNow));

        Assert.Contains("карантинном изоляторе", ex.Message);
    }

    [Fact]
    public void ProductBatchApprove_ShouldBlockApproval_IfAntibioticAdministeredAfterDraftCreation()
    {
        var draftTime = new DateTime(2026, 11, 18, 6, 0, 0, DateTimeKind.Utc);
        var cow = CreateTestCow();
        var batch = new ProductBatch
        {
            BatchNumber = "MILK-RACE-01",
            BatchType = ProductBatchType.RawMilk,
            CreatedByOperator = "Петров М.И."
        };

        // В 06:00 корова здорова и добавлена в черновик партии
        batch.AddAnimalContribution(cow, 30m, draftTime);

        // В 06:30 ветврач вводит антибиотик
        var amox = CreateAntibiotic(milkHours: 96, meatDays: 21);
        TreatmentRecord.Create(cow, amox, 40m, "в/м", "Мастит", draftTime.AddMinutes(30), "Д-р Соколов А.В.");

        // В 07:00 попытка подписать ветеринарный допуск партии обязана заблокироваться!
        Assert.Throws<QuarantineLockDomainException>(() =>
            batch.ApproveByVeterinarian(new[] { cow }, "Д-р Соколов А.В.", draftTime.AddHours(1)));
    }

    [Fact]
    public void TreatmentCreate_ShouldThrow_WhenDrugIsExpiredOrInsufficientStock()
    {
        var cow = CreateTestCow();
        var expiredDrug = CreateAntibiotic(stock: 50m);
        expiredDrug.ExpirationDate = DateTime.UtcNow.Date.AddDays(-1);

        Assert.Throws<DomainRuleException>(() =>
            TreatmentRecord.Create(cow, expiredDrug, 10m, "в/м", "Мастит", DateTime.UtcNow, "Ветврач"));

        var lowStockDrug = CreateAntibiotic(stock: 15m);
        Assert.Throws<InsufficientDrugStockException>(() =>
            TreatmentRecord.Create(cow, lowStockDrug, 25m, "в/м", "Мастит", DateTime.UtcNow, "Ветврач"));
    }

    [Fact]
    public async Task CreateBatchWithQuarantineCheck_ShouldRollbackBatchAndWriteIncidentLog_WhenQuarantinedCowIncluded()
    {
        var options = new DbContextOptionsBuilder<VetGuardDbContext>()
            .UseInMemoryDatabase($"QuarantineBatchTest_{Guid.NewGuid()}")
            .Options;

        await using var db = new VetGuardDbContext(options);
        var healthyCow = CreateTestCow("RU-38-OK-01", "Байкалка");
        var lockedCow = CreateTestCow("RU-38-LOCK-02", "Зорька");
        var nitox = CreateAntibiotic(milkHours: 168, meatDays: 28);

        var tr = TreatmentRecord.Create(lockedCow, nitox, 50m, "в/м", "Мастит", DateTime.UtcNow.AddHours(-10), "Ветврач");
        db.Animals.AddRange(healthyCow, lockedCow);
        db.VetDrugs.Add(nitox);
        db.TreatmentRecords.Add(tr);
        await db.SaveChangesAsync();

        var notifier = new FakeAlertNotifier();
        var service = new ProductBatchService(db, notifier, new CreateProductBatchCommandValidator());

        var outcome = await service.CreateBatchWithQuarantineCheckAsync(new CreateProductBatchCommand
        {
            BatchNumber = "MILK-BLOCK-TEST",
            BatchType = ProductBatchType.RawMilk,
            Destination = "Молзавод",
            OperatorName = "Петров М.И.",
            Contributions = new List<BatchContributionInput>
            {
                new() { AnimalId = healthyCow.Id, QuantityLitersOrKg = 31m },
                new() { AnimalId = lockedCow.Id, QuantityLitersOrKg = 25m }
            }
        });

        Assert.False(outcome.Succeeded);
        Assert.Equal("RU-38-LOCK-02", outcome.BlockedAnimalTag);
        Assert.Equal("Нитокс 200", outcome.BlockedDrugName);
        Assert.True(outcome.RemainingQuarantineHours > 150);

        // Партия НЕ сохранена, но инцидент зафиксирован в QuarantineBlockLogs и отправлен в SignalR!
        Assert.Equal(0, await db.ProductBatches.CountAsync());
        Assert.Equal(1, await db.QuarantineBlockLogs.CountAsync());
        Assert.Equal(1, notifier.BlockedAlertsCount);
    }

    private sealed class FakeAlertNotifier : IVetAlertNotifier
    {
        public int BlockedAlertsCount { get; private set; }

        public Task NotifyQuarantineLockActivatedAsync(string animalTag, string animalName, string drugName, int milkHours, int meatDays, DateTime clearanceUtc, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task NotifyQuarantineViolationBlockedAsync(string batchNumber, string animalTag, string animalName, string? drugName, double remainingHours, string attemptedBy, CancellationToken ct = default)
        {
            BlockedAlertsCount++;
            return Task.CompletedTask;
        }

        public Task NotifyQuarantineExpiredAsync(string animalTag, string animalName, string drugName, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task NotifyEpizooticReminderAsync(string message, int overdueCount, int dueSoonCount, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
