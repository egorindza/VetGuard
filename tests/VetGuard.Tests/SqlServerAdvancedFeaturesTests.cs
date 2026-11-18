using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using VetGuard.Application.Models;
using VetGuard.Application.Validators;
using VetGuard.Domain.Enums;
using VetGuard.Domain.Exceptions;
using VetGuard.Infrastructure.Persistence;
using VetGuard.Infrastructure.Services;

namespace VetGuard.Tests;

public sealed class SqlServerAdvancedFeaturesTests
{
    private const string SqlConnectionString =
        "Server=localhost;Database=IrGau_VetGuardDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

    private static VetGuardDbContext CreateSqlDbContext()
    {
        var options = new DbContextOptionsBuilder<VetGuardDbContext>()
            .UseSqlServer(SqlConnectionString, sql => sql.EnableRetryOnFailure(3))
            .Options;
        return new VetGuardDbContext(options);
    }

    [Fact]
    public async Task SqlServer_TransactionalQuarantineLockWithRetryStrategy_ShouldRollbackAndLogIncident()
    {
        await using var db = CreateSqlDbContext();
        await VetGuardDbSeeder.SeedAsync(db, NullLogger.Instance);

        var zorka = await db.Animals.AsNoTracking().FirstAsync(a => a.TagNumber == "RU-38-IRGAU-0104");
        var baykalka = await db.Animals.AsNoTracking().FirstAsync(a => a.TagNumber == "RU-38-IRGAU-0107");

        var batchService = new ProductBatchService(db, new NoOpNotifier(), new CreateProductBatchCommandValidator());
        var outcome = await batchService.CreateBatchWithQuarantineCheckAsync(new CreateProductBatchCommand
        {
            BatchNumber = $"MILK-SQL-TEST-{Guid.NewGuid():N}"[..24],
            BatchType = ProductBatchType.RawMilk,
            Destination = "АО Янтарь",
            OperatorName = "Тест интеграции SQL Server",
            Contributions = new List<BatchContributionInput>
            {
                new() { AnimalId = baykalka.Id, QuantityLitersOrKg = 31m },
                new() { AnimalId = zorka.Id, QuantityLitersOrKg = 26m }
            }
        });

        Assert.False(outcome.Succeeded);
        Assert.Equal("RU-38-IRGAU-0104", outcome.BlockedAnimalTag);
        Assert.Contains("Нитокс 200", outcome.BlockedDrugName);
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = SqlConnectionString
            })
            .Build();
    }

    [Fact]
    public async Task SqlServer_DapperRecursiveCte_ShouldBuild6GenerationPedigreeAndCalculateAll4WrightRiskTiers()
    {
        await using var db = CreateSqlDbContext();
        await VetGuardDbSeeder.SeedAsync(db, NullLogger.Instance);

        var config = CreateConfiguration();
        var pedigreeService = new DapperPedigreeService(db, config, new SaveBreedingPlanCommandValidator());

        var baykalka = await db.Animals.AsNoTracking().FirstAsync(a => a.TagNumber == "RU-38-IRGAU-0107");

        // 1. Проверка рекурсивного CTE WITH PedigreeTree AS (...) на глубину 6 поколений
        var tree = await pedigreeService.GetPedigreeTreeAsync(baykalka.Id, maxGenerations: 6);
        Assert.NotNull(tree);
        Assert.Equal(6, tree!.Nodes.Max(n => n.GenerationDepth));
        Assert.Contains(tree.Nodes, n => n.GenerationDepth == 6 && n.Name == "Атлант-Основатель");

        // 2. Проверка T-SQL CTE калькулятора инбридинга С. Райта по всем 4 племенным быкам
        var evaluations = await pedigreeService.EvaluateCandidateSiresForDamAsync(baykalka.Id, maxGenerations: 6);
        Assert.True(evaluations.Count >= 4);

        var almaz = evaluations.Single(e => e.SireTag == "RU-38-IRGAU-0011");
        Assert.Equal(0.0, almaz.InbreedingPercent, precision: 3);
        Assert.Equal(InbreedingRiskLevel.Safe, almaz.RiskLevel);

        var yenisey = evaluations.Single(e => e.SireTag == "RU-38-IRGAU-0012");
        Assert.Equal(1.5625, yenisey.InbreedingPercent, precision: 4);
        Assert.Equal(InbreedingRiskLevel.Moderate, yenisey.RiskLevel);

        var grom = evaluations.Single(e => e.SireTag == "RU-38-IRGAU-0013");
        Assert.Equal(4.6875, grom.InbreedingPercent, precision: 4);
        Assert.Equal(InbreedingRiskLevel.High, grom.RiskLevel);

        var buran = evaluations.Single(e => e.SireTag == "RU-38-IRGAU-0014");
        Assert.Equal(12.5, buran.InbreedingPercent, precision: 4);
        Assert.Equal(InbreedingRiskLevel.Critical, buran.RiskLevel);

        // 3. Попытка сохранить кровосмесительную пару (12.5%) блокируется доменным инвариантом
        await Assert.ThrowsAsync<InbreedingHazardException>(() =>
            pedigreeService.SaveBreedingPlanAsync(new SaveBreedingPlanCommand
            {
                DamId = baykalka.Id,
                SireId = buran.SireId,
                SelectorName = "Тест селекционера"
            }));
    }

    [Fact]
    public async Task SqlServer_RowVersionConcurrency_ShouldDetectConcurrentVaccineStockModification()
    {
        await using var db1 = CreateSqlDbContext();
        await using var db2 = CreateSqlDbContext();
        await VetGuardDbSeeder.SeedAsync(db1, NullLogger.Instance);

        var drugInSession1 = await db1.VetDrugs.FirstAsync(d => d.BatchSeries == "VAC-SYA-2026-03");
        var staleRowVersionBase64 = Convert.ToBase64String(drugInSession1.RowVersion);

        // Сессия 1 проводит приход/изменение остатка вакцины (SQL Server обновляет rowversion)
        drugInSession1.ReplenishStock(5m);
        await db1.SaveChangesAsync();

        // Сессия 2 пытается сохранить изменение со старым токеном ExpectedRowVersionBase64 -> ConcurrencyConflictException!
        var service2 = new TreatmentAndDrugService(
            db2,
            new NoOpNotifier(),
            new CreateVetDrugCommandValidator(),
            new RegisterTreatmentCommandValidator());

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            service2.ReplenishStockAsync(new ReplenishDrugStockCommand
            {
                DrugId = drugInSession1.Id,
                AdditionalDoses = 10m,
                ExpectedRowVersionBase64 = staleRowVersionBase64
            }));
    }

    [Fact]
    public async Task SqlServer_TemporalTables_And_PdfQrGeneration_ShouldWorkEndToEnd()
    {
        await using var db = CreateSqlDbContext();
        await VetGuardDbSeeder.SeedAsync(db, NullLogger.Instance);

        var config = CreateConfiguration();
        var auditService = new TemporalAuditService(db, config);

        var treatmentHistory = await auditService.GetTreatmentTemporalHistoryAsync();
        var animalHistory = await auditService.GetAnimalStatusTemporalHistoryAsync();

        Assert.NotEmpty(treatmentHistory);
        Assert.Contains(treatmentHistory, r => !r.IsCurrentVersion); // Архивная ревизия из TreatmentRecordsHistory!
        Assert.NotEmpty(animalHistory);
        Assert.Contains(animalHistory, r => !r.IsCurrentVersion); // Архивный срез из AnimalsHistory!

        var qrService = new QrCodeService();
        var pdfService = new PdfReportService(qrService);
        var herdService = new HerdService(
            db,
            qrService,
            new CreateOrUpdateAnimalCommandValidator(),
            new MobileVetInspectionCommandValidator());

        var zorka = await db.Animals.AsNoTracking().FirstAsync(a => a.TagNumber == "RU-38-IRGAU-0104");
        var details = await herdService.GetAnimalDetailsAsync(zorka.Id, "http://localhost:5104");
        Assert.NotNull(details);

        var stallPdf = pdfService.GenerateStallQrPassportPdf(details!);
        var extractPdf = pdfService.GenerateVeterinaryExtractPdf(details!);

        var sampleBatch = await db.ProductBatches
            .AsNoTracking()
            .Include(b => b.Items).ThenInclude(i => i.Animal)
            .FirstAsync();
        var batchPdf = pdfService.GenerateBatchVetCertificatePdf(sampleBatch);

        var sampleAct = await db.EpizooticActs
            .AsNoTracking()
            .Include(a => a.EpizooticRule)
            .Include(a => a.Items).ThenInclude(i => i.Animal)
            .FirstAsync();
        var actPdf = pdfService.GenerateEpizooticActPdf(sampleAct);

        Assert.True(stallPdf.Length > 1500);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(stallPdf, 0, 5));
        Assert.True(extractPdf.Length > 1500);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(extractPdf, 0, 5));
        Assert.True(batchPdf.Length > 1500);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(batchPdf, 0, 5));
        Assert.True(actPdf.Length > 1500);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(actPdf, 0, 5));
    }

    private sealed class NoOpNotifier : VetGuard.Application.Interfaces.IVetAlertNotifier
    {
        public Task NotifyEpizooticReminderAsync(string message, int overdueCount, int dueSoonCount, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyQuarantineExpiredAsync(string animalTag, string animalName, string drugName, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyQuarantineLockActivatedAsync(string animalTag, string animalName, string drugName, int milkHours, int meatDays, DateTime clearanceUtc, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyQuarantineViolationBlockedAsync(string batchNumber, string animalTag, string animalName, string? drugName, double remainingHours, string attemptedBy, CancellationToken ct = default) => Task.CompletedTask;
    }
}
