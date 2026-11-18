using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VetGuard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialVetGuardSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Animals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TagNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RfidChip = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Species = table.Column<int>(type: "int", nullable: false),
                    Breed = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Sex = table.Column<int>(type: "int", nullable: false),
                    BirthDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SireId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsHistoricalPedigreeOnly = table.Column<bool>(type: "bit", nullable: false),
                    BarnName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SectionCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    LineageCode = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    HealthStatus = table.Column<int>(type: "int", nullable: false),
                    QuarantineReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    QuarantineEnteredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PreviousBarnBeforeQuarantine = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CurrentWeightKg = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    AverageDailyMilkKg = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SysEndTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    SysStartTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Animals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Animals_Animals_DamId",
                        column: x => x.DamId,
                        principalTable: "Animals",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Animals_Animals_SireId",
                        column: x => x.SireId,
                        principalTable: "Animals",
                        principalColumn: "Id");
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "AnimalsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", null)
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "SysEndTime")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "SysStartTime");

            migrationBuilder.CreateTable(
                name: "ProductBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BatchType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalVolumeLitersOrKg = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    FatPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    ProteinPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    SomaticCellsThousands = table.Column<int>(type: "int", nullable: true),
                    Destination = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    CreatedByOperator = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ApprovedByVeterinarian = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuarantineBlockLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AttemptedBatchNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BatchType = table.Column<int>(type: "int", nullable: false),
                    AnimalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnimalTag = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AnimalName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ActiveDrugName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ActiveDrugSeries = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RemainingHours = table.Column<double>(type: "float", nullable: false),
                    AttemptedByUser = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    BlockMessage = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuarantineBlockLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VetDrugs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ActiveSubstance = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Manufacturer = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    BatchSeries = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MilkWithdrawalHours = table.Column<int>(type: "int", nullable: false),
                    MeatWithdrawalDays = table.Column<int>(type: "int", nullable: false),
                    DefaultDosageText = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    StockDoses = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    IsPotent = table.Column<bool>(type: "bit", nullable: false),
                    StorageConditions = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VetDrugs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BreedingPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SireId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InbreedingCoefficientPercent = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    ShaporuzhNotation = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    RiskLevel = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CommonAncestorsSummary = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    PlannedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBySelector = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BreedingPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BreedingPlans_Animals_DamId",
                        column: x => x.DamId,
                        principalTable: "Animals",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BreedingPlans_Animals_SireId",
                        column: x => x.SireId,
                        principalTable: "Animals",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MilkYieldRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnimalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MilkingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MorningLiters = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    EveningLiters = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    FatPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    ProteinPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    IsQuarantineDiscarded = table.Column<bool>(type: "bit", nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MilkYieldRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MilkYieldRecords_Animals_AnimalId",
                        column: x => x.AnimalId,
                        principalTable: "Animals",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VetInspections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnimalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BodyTemperatureC = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: false),
                    HeartRateBpm = table.Column<int>(type: "int", nullable: false),
                    RuminationPer2Min = table.Column<int>(type: "int", nullable: false),
                    MucousMembranes = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    UdderCondition = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ResultHealthStatus = table.Column<int>(type: "int", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    InspectorName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ScannedViaQrPassport = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VetInspections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VetInspections_Animals_AnimalId",
                        column: x => x.AnimalId,
                        principalTable: "Animals",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WeightRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnimalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeasuredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WeightKg = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    DailyGainGrams = table.Column<int>(type: "int", nullable: false),
                    MeasuredBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeightRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeightRecords_Animals_AnimalId",
                        column: x => x.AnimalId,
                        principalTable: "Animals",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductBatchItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnimalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuantityLitersOrKg = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductBatchItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductBatchItems_Animals_AnimalId",
                        column: x => x.AnimalId,
                        principalTable: "Animals",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductBatchItems_ProductBatches_ProductBatchId",
                        column: x => x.ProductBatchId,
                        principalTable: "ProductBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EpizooticRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiseaseType = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    MinAgeDays = table.Column<int>(type: "int", nullable: false),
                    IntervalDays = table.Column<int>(type: "int", nullable: false),
                    DefaultVetDrugId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StandardDosePerAnimal = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    RegulatoryBasis = table.Column<string>(type: "nvarchar(220)", maxLength: 220, nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EpizooticRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EpizooticRules_VetDrugs_DefaultVetDrugId",
                        column: x => x.DefaultVetDrugId,
                        principalTable: "VetDrugs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TreatmentRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnimalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VetDrugId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DrugNameSnapshot = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DrugBatchSeriesSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsPotentDrugSnapshot = table.Column<bool>(type: "bit", nullable: false),
                    Diagnosis = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    DosageAmount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    DosageUnit = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    AdministrationRoute = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    AdministeredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MilkWithdrawalHoursApplied = table.Column<int>(type: "int", nullable: false),
                    MeatWithdrawalDaysApplied = table.Column<int>(type: "int", nullable: false),
                    MilkWithdrawalEndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MeatWithdrawalEndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VeterinarianName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    VeterinarianRole = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ClinicalNotes = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    IsClearedNotificationSent = table.Column<bool>(type: "bit", nullable: false),
                    SysEndTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    SysStartTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreatmentRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TreatmentRecords_Animals_AnimalId",
                        column: x => x.AnimalId,
                        principalTable: "Animals",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TreatmentRecords_VetDrugs_VetDrugId",
                        column: x => x.VetDrugId,
                        principalTable: "VetDrugs",
                        principalColumn: "Id");
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "TreatmentRecordsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", null)
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "SysEndTime")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "SysStartTime");

            migrationBuilder.CreateTable(
                name: "EpizooticActs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EpizooticRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VetDrugId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DrugNameSnapshot = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DrugSeriesSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExecutedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TargetBarn = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DosePerAnimal = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    TotalDosesUsed = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    ExecutedByVeterinarian = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WitnessZootechnician = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EpizooticActs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EpizooticActs_EpizooticRules_EpizooticRuleId",
                        column: x => x.EpizooticRuleId,
                        principalTable: "EpizooticRules",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EpizooticActs_VetDrugs_VetDrugId",
                        column: x => x.VetDrugId,
                        principalTable: "VetDrugs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EpizooticActItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EpizooticActId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnimalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResultText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NextDueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EpizooticActItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EpizooticActItems_Animals_AnimalId",
                        column: x => x.AnimalId,
                        principalTable: "Animals",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EpizooticActItems_EpizooticActs_EpizooticActId",
                        column: x => x.EpizooticActId,
                        principalTable: "EpizooticActs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Animals_DamId",
                table: "Animals",
                column: "DamId");

            migrationBuilder.CreateIndex(
                name: "IX_Animals_IsHistoricalPedigreeOnly_HealthStatus",
                table: "Animals",
                columns: new[] { "IsHistoricalPedigreeOnly", "HealthStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_Animals_RfidChip",
                table: "Animals",
                column: "RfidChip",
                unique: true,
                filter: "[RfidChip] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Animals_SireId_DamId",
                table: "Animals",
                columns: new[] { "SireId", "DamId" });

            migrationBuilder.CreateIndex(
                name: "IX_Animals_TagNumber",
                table: "Animals",
                column: "TagNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BreedingPlans_DamId",
                table: "BreedingPlans",
                column: "DamId");

            migrationBuilder.CreateIndex(
                name: "IX_BreedingPlans_SireId",
                table: "BreedingPlans",
                column: "SireId");

            migrationBuilder.CreateIndex(
                name: "IX_EpizooticActItems_AnimalId",
                table: "EpizooticActItems",
                column: "AnimalId");

            migrationBuilder.CreateIndex(
                name: "IX_EpizooticActItems_EpizooticActId",
                table: "EpizooticActItems",
                column: "EpizooticActId");

            migrationBuilder.CreateIndex(
                name: "IX_EpizooticActs_ActNumber",
                table: "EpizooticActs",
                column: "ActNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EpizooticActs_EpizooticRuleId",
                table: "EpizooticActs",
                column: "EpizooticRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_EpizooticActs_VetDrugId",
                table: "EpizooticActs",
                column: "VetDrugId");

            migrationBuilder.CreateIndex(
                name: "IX_EpizooticRules_DefaultVetDrugId",
                table: "EpizooticRules",
                column: "DefaultVetDrugId");

            migrationBuilder.CreateIndex(
                name: "IX_MilkYieldRecords_AnimalId_MilkingDate",
                table: "MilkYieldRecords",
                columns: new[] { "AnimalId", "MilkingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_BatchNumber",
                table: "ProductBatches",
                column: "BatchNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatchItems_AnimalId",
                table: "ProductBatchItems",
                column: "AnimalId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatchItems_ProductBatchId",
                table: "ProductBatchItems",
                column: "ProductBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_QuarantineBlockLogs_AttemptedAtUtc",
                table: "QuarantineBlockLogs",
                column: "AttemptedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentRecords_AnimalId_MeatWithdrawalEndsAtUtc",
                table: "TreatmentRecords",
                columns: new[] { "AnimalId", "MeatWithdrawalEndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentRecords_AnimalId_MilkWithdrawalEndsAtUtc",
                table: "TreatmentRecords",
                columns: new[] { "AnimalId", "MilkWithdrawalEndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentRecords_VetDrugId",
                table: "TreatmentRecords",
                column: "VetDrugId");

            migrationBuilder.CreateIndex(
                name: "IX_VetDrugs_Name_BatchSeries",
                table: "VetDrugs",
                columns: new[] { "Name", "BatchSeries" });

            migrationBuilder.CreateIndex(
                name: "IX_VetInspections_AnimalId",
                table: "VetInspections",
                column: "AnimalId");

            migrationBuilder.CreateIndex(
                name: "IX_WeightRecords_AnimalId_MeasuredAtUtc",
                table: "WeightRecords",
                columns: new[] { "AnimalId", "MeasuredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BreedingPlans");

            migrationBuilder.DropTable(
                name: "EpizooticActItems");

            migrationBuilder.DropTable(
                name: "MilkYieldRecords");

            migrationBuilder.DropTable(
                name: "ProductBatchItems");

            migrationBuilder.DropTable(
                name: "QuarantineBlockLogs");

            migrationBuilder.DropTable(
                name: "TreatmentRecords")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "TreatmentRecordsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", null)
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "SysEndTime")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "SysStartTime");

            migrationBuilder.DropTable(
                name: "VetInspections");

            migrationBuilder.DropTable(
                name: "WeightRecords");

            migrationBuilder.DropTable(
                name: "EpizooticActs");

            migrationBuilder.DropTable(
                name: "ProductBatches");

            migrationBuilder.DropTable(
                name: "Animals")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "AnimalsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", null)
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "SysEndTime")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "SysStartTime");

            migrationBuilder.DropTable(
                name: "EpizooticRules");

            migrationBuilder.DropTable(
                name: "VetDrugs");
        }
    }
}
