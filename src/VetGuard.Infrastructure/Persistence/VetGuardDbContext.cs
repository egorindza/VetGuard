using Microsoft.EntityFrameworkCore;
using VetGuard.Domain.Entities;

namespace VetGuard.Infrastructure.Persistence;

public class VetGuardDbContext : DbContext
{
    public VetGuardDbContext(DbContextOptions<VetGuardDbContext> options) : base(options)
    {
    }

    public DbSet<Animal> Animals => Set<Animal>();
    public DbSet<VetDrug> VetDrugs => Set<VetDrug>();
    public DbSet<TreatmentRecord> TreatmentRecords => Set<TreatmentRecord>();
    public DbSet<ProductBatch> ProductBatches => Set<ProductBatch>();
    public DbSet<ProductBatchItem> ProductBatchItems => Set<ProductBatchItem>();
    public DbSet<QuarantineBlockLog> QuarantineBlockLogs => Set<QuarantineBlockLog>();
    public DbSet<MilkYieldRecord> MilkYieldRecords => Set<MilkYieldRecord>();
    public DbSet<WeightRecord> WeightRecords => Set<WeightRecord>();
    public DbSet<VetInspection> VetInspections => Set<VetInspection>();
    public DbSet<EpizooticRule> EpizooticRules => Set<EpizooticRule>();
    public DbSet<EpizooticAct> EpizooticActs => Set<EpizooticAct>();
    public DbSet<EpizooticActItem> EpizooticActItems => Set<EpizooticActItem>();
    public DbSet<BreedingPlan> BreedingPlans => Set<BreedingPlan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var isSqlServer = Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true;

        // 1. Animal — Temporal Table (SYSTEM_VERSIONING = ON) + Optimistic Concurrency (rowversion)
        modelBuilder.Entity<Animal>(entity =>
        {
            if (isSqlServer)
            {
                entity.ToTable("Animals", b => b.IsTemporal(t =>
                {
                    t.HasPeriodStart("SysStartTime");
                    t.HasPeriodEnd("SysEndTime");
                    t.UseHistoryTable("AnimalsHistory");
                }));
            }
            else
            {
                entity.ToTable("Animals");
            }

            entity.HasKey(x => x.Id);
            entity.Property(x => x.TagNumber).HasMaxLength(32).IsRequired();
            entity.HasIndex(x => x.TagNumber).IsUnique();

            entity.Property(x => x.RfidChip).HasMaxLength(20);
            if (isSqlServer)
            {
                entity.HasIndex(x => x.RfidChip).IsUnique().HasFilter("[RfidChip] IS NOT NULL");
            }

            entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Breed).HasMaxLength(80).IsRequired();
            entity.Property(x => x.BarnName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.SectionCode).HasMaxLength(80).IsRequired();
            entity.Property(x => x.LineageCode).HasMaxLength(120);
            entity.Property(x => x.QuarantineReason).HasMaxLength(300);
            entity.Property(x => x.PreviousBarnBeforeQuarantine).HasMaxLength(200);
            entity.Property(x => x.Notes).HasMaxLength(800);

            entity.Property(x => x.CurrentWeightKg).HasPrecision(10, 2);
            entity.Property(x => x.AverageDailyMilkKg).HasPrecision(8, 2);

            entity.Property(x => x.RowVersion).IsRowVersion();

            // Внимание: для таблиц с SYSTEM_VERSIONING = ON в MS SQL Server запрещены каскадные удаления (CASCADE / SET NULL)
            entity.HasOne(x => x.Sire)
                .WithMany()
                .HasForeignKey(x => x.SireId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Dam)
                .WithMany()
                .HasForeignKey(x => x.DamId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(x => new { x.SireId, x.DamId });
            entity.HasIndex(x => new { x.IsHistoricalPedigreeOnly, x.HealthStatus });
        });

        // 2. VetDrug — Справочник ветпрепаратов с Optimistic Concurrency (rowversion) при списании серий
        modelBuilder.Entity<VetDrug>(entity =>
        {
            entity.ToTable("VetDrugs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.ActiveSubstance).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Manufacturer).HasMaxLength(120).IsRequired();
            entity.Property(x => x.BatchSeries).HasMaxLength(50).IsRequired();
            entity.Property(x => x.DefaultDosageText).HasMaxLength(150);
            entity.Property(x => x.Unit).HasMaxLength(24).IsRequired();
            entity.Property(x => x.StorageConditions).HasMaxLength(200);
            entity.Property(x => x.StockDoses).HasPrecision(12, 2);
            entity.Property(x => x.RowVersion).IsRowVersion();

            entity.HasIndex(x => new { x.Name, x.BatchSeries });
        });

        // 3. TreatmentRecord — Журнал ЛПМ с Temporal Table (SYSTEM_VERSIONING = ON) для строгого вет-аудита
        modelBuilder.Entity<TreatmentRecord>(entity =>
        {
            if (isSqlServer)
            {
                entity.ToTable("TreatmentRecords", b => b.IsTemporal(t =>
                {
                    t.HasPeriodStart("SysStartTime");
                    t.HasPeriodEnd("SysEndTime");
                    t.UseHistoryTable("TreatmentRecordsHistory");
                }));
            }
            else
            {
                entity.ToTable("TreatmentRecords");
            }

            entity.HasKey(x => x.Id);
            entity.Property(x => x.DrugNameSnapshot).HasMaxLength(150).IsRequired();
            entity.Property(x => x.DrugBatchSeriesSnapshot).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Diagnosis).HasMaxLength(250).IsRequired();
            entity.Property(x => x.DosageAmount).HasPrecision(10, 2);
            entity.Property(x => x.DosageUnit).HasMaxLength(24).IsRequired();
            entity.Property(x => x.AdministrationRoute).HasMaxLength(60).IsRequired();
            entity.Property(x => x.VeterinarianName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.VeterinarianRole).HasMaxLength(80).IsRequired();
            entity.Property(x => x.ClinicalNotes).HasMaxLength(600);

            entity.HasOne(x => x.Animal)
                .WithMany(a => a.Treatments)
                .HasForeignKey(x => x.AnimalId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.VetDrug)
                .WithMany(d => d.Treatments)
                .HasForeignKey(x => x.VetDrugId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(x => new { x.AnimalId, x.MilkWithdrawalEndsAtUtc });
            entity.HasIndex(x => new { x.AnimalId, x.MeatWithdrawalEndsAtUtc });
        });

        // 4. ProductBatch & ProductBatchItem — Партии продукции с Optimistic Concurrency (rowversion)
        modelBuilder.Entity<ProductBatch>(entity =>
        {
            entity.ToTable("ProductBatches");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.BatchNumber).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => x.BatchNumber).IsUnique();

            entity.Property(x => x.TotalVolumeLitersOrKg).HasPrecision(12, 2);
            entity.Property(x => x.FatPercent).HasPrecision(5, 2);
            entity.Property(x => x.ProteinPercent).HasPrecision(5, 2);
            entity.Property(x => x.Destination).HasMaxLength(160).IsRequired();
            entity.Property(x => x.CreatedByOperator).HasMaxLength(120).IsRequired();
            entity.Property(x => x.ApprovedByVeterinarian).HasMaxLength(120);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Property(x => x.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<ProductBatchItem>(entity =>
        {
            entity.ToTable("ProductBatchItems");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.QuantityLitersOrKg).HasPrecision(10, 2);

            entity.HasOne(x => x.ProductBatch)
                .WithMany(b => b.Items)
                .HasForeignKey(x => x.ProductBatchId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Animal)
                .WithMany()
                .HasForeignKey(x => x.AnimalId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<QuarantineBlockLog>(entity =>
        {
            entity.ToTable("QuarantineBlockLogs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AttemptedBatchNumber).HasMaxLength(50).IsRequired();
            entity.Property(x => x.AnimalTag).HasMaxLength(32).IsRequired();
            entity.Property(x => x.AnimalName).HasMaxLength(80).IsRequired();
            entity.Property(x => x.ActiveDrugName).HasMaxLength(150);
            entity.Property(x => x.ActiveDrugSeries).HasMaxLength(50);
            entity.Property(x => x.AttemptedByUser).HasMaxLength(120).IsRequired();
            entity.Property(x => x.BlockMessage).HasMaxLength(600).IsRequired();
            entity.HasIndex(x => x.AttemptedAtUtc);
        });

        // 5. Продуктивность и осмотры
        modelBuilder.Entity<MilkYieldRecord>(entity =>
        {
            entity.ToTable("MilkYieldRecords");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MorningLiters).HasPrecision(8, 2);
            entity.Property(x => x.EveningLiters).HasPrecision(8, 2);
            entity.Property(x => x.FatPercent).HasPrecision(5, 2);
            entity.Property(x => x.ProteinPercent).HasPrecision(5, 2);
            entity.Property(x => x.RecordedBy).HasMaxLength(120);
            entity.Ignore(x => x.TotalLiters);

            entity.HasOne(x => x.Animal)
                .WithMany(a => a.MilkYields)
                .HasForeignKey(x => x.AnimalId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(x => new { x.AnimalId, x.MilkingDate });
        });

        modelBuilder.Entity<WeightRecord>(entity =>
        {
            entity.ToTable("WeightRecords");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.WeightKg).HasPrecision(10, 2);
            entity.Property(x => x.MeasuredBy).HasMaxLength(120);

            entity.HasOne(x => x.Animal)
                .WithMany(a => a.WeightRecords)
                .HasForeignKey(x => x.AnimalId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(x => new { x.AnimalId, x.MeasuredAtUtc });
        });

        modelBuilder.Entity<VetInspection>(entity =>
        {
            entity.ToTable("VetInspections");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.BodyTemperatureC).HasPrecision(4, 1);
            entity.Property(x => x.MucousMembranes).HasMaxLength(150);
            entity.Property(x => x.UdderCondition).HasMaxLength(200);
            entity.Property(x => x.Summary).HasMaxLength(500);
            entity.Property(x => x.InspectorName).HasMaxLength(120);

            entity.HasOne(x => x.Animal)
                .WithMany(a => a.Inspections)
                .HasForeignKey(x => x.AnimalId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // 6. Противоэпизоотические мероприятия и племенной план
        modelBuilder.Entity<EpizooticRule>(entity =>
        {
            entity.ToTable("EpizooticRules");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(180).IsRequired();
            entity.Property(x => x.RegulatoryBasis).HasMaxLength(220);
            entity.Property(x => x.StandardDosePerAnimal).HasPrecision(8, 2);

            entity.HasOne(x => x.DefaultVetDrug)
                .WithMany()
                .HasForeignKey(x => x.DefaultVetDrugId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<EpizooticAct>(entity =>
        {
            entity.ToTable("EpizooticActs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ActNumber).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => x.ActNumber).IsUnique();
            entity.Property(x => x.DrugNameSnapshot).HasMaxLength(150);
            entity.Property(x => x.DrugSeriesSnapshot).HasMaxLength(50);
            entity.Property(x => x.TargetBarn).HasMaxLength(120);
            entity.Property(x => x.DosePerAnimal).HasPrecision(8, 2);
            entity.Property(x => x.TotalDosesUsed).HasPrecision(12, 2);
            entity.Property(x => x.ExecutedByVeterinarian).HasMaxLength(120);
            entity.Property(x => x.WitnessZootechnician).HasMaxLength(120);
            entity.Property(x => x.Notes).HasMaxLength(500);

            entity.HasOne(x => x.EpizooticRule)
                .WithMany(r => r.Acts)
                .HasForeignKey(x => x.EpizooticRuleId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.VetDrug)
                .WithMany()
                .HasForeignKey(x => x.VetDrugId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<EpizooticActItem>(entity =>
        {
            entity.ToTable("EpizooticActItems");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ResultText).HasMaxLength(200);

            entity.HasOne(x => x.EpizooticAct)
                .WithMany(a => a.Items)
                .HasForeignKey(x => x.EpizooticActId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Animal)
                .WithMany(an => an.EpizooticItems)
                .HasForeignKey(x => x.AnimalId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<BreedingPlan>(entity =>
        {
            entity.ToTable("BreedingPlans");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.InbreedingCoefficientPercent).HasPrecision(8, 4);
            entity.Property(x => x.ShaporuzhNotation).HasMaxLength(120);
            entity.Property(x => x.CommonAncestorsSummary).HasMaxLength(400);
            entity.Property(x => x.CreatedBySelector).HasMaxLength(120);
            entity.Property(x => x.Notes).HasMaxLength(400);

            entity.HasOne(x => x.Dam)
                .WithMany()
                .HasForeignKey(x => x.DamId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Sire)
                .WithMany()
                .HasForeignKey(x => x.SireId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
