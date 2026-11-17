using Dapper;
using FluentValidation;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using VetGuard.Application.Interfaces;
using VetGuard.Application.Models;
using VetGuard.Domain.Entities;
using VetGuard.Domain.Enums;
using VetGuard.Domain.Exceptions;
using VetGuard.Domain.Services;
using VetGuard.Infrastructure.Persistence;

namespace VetGuard.Infrastructure.Services;

public sealed class DapperPedigreeService : IPedigreeService
{
    private readonly VetGuardDbContext _db;
    private readonly string? _connectionString;
    private readonly IValidator<SaveBreedingPlanCommand> _planValidator;

    public DapperPedigreeService(
        VetGuardDbContext db,
        IConfiguration configuration,
        IValidator<SaveBreedingPlanCommand> planValidator)
    {
        _db = db;
        _connectionString = configuration.GetConnectionString("DefaultConnection");
        _planValidator = planValidator;
    }

    /// <summary>
    /// Строит родословную животного до 6 поколений через рекурсивный T-SQL запрос WITH PedigreeTree AS (...) на Dapper.
    /// </summary>
    public async Task<PedigreeTreeViewDto?> GetPedigreeTreeAsync(
        Guid animalId,
        int maxGenerations = 6,
        CancellationToken ct = default)
    {
        var subject = await _db.Animals
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == animalId, ct);

        if (subject is null)
        {
            return null;
        }

        maxGenerations = Math.Clamp(maxGenerations, 1, 6);

        if (_db.Database.IsRelational() && !string.IsNullOrWhiteSpace(_connectionString))
        {
            const string sql = @"
WITH PedigreeTree AS (
    -- Anchor member: родители I поколения (Отец F и Мать M)
    SELECT
        p.Id AS AnimalId,
        child.Id AS ChildId,
        1 AS GenerationDepth,
        CAST(CASE WHEN p.Id = child.SireId THEN 'F' ELSE 'M' END AS varchar(16)) AS PathCode,
        CAST(CASE WHEN p.Id = child.SireId THEN N'Отцовская ветвь' ELSE N'Материнская ветвь' END AS nvarchar(32)) AS LineSide,
        p.TagNumber,
        p.Name,
        p.Breed,
        p.Sex,
        p.BirthDate,
        p.LineageCode,
        p.SireId,
        p.DamId,
        CAST('|' + CAST(child.Id AS varchar(36)) + '|' + CAST(p.Id AS varchar(36)) + '|' AS varchar(600)) AS VisitedIds
    FROM dbo.Animals child
    INNER JOIN dbo.Animals p ON p.Id IN (child.SireId, child.DamId)
    WHERE child.Id = @AnimalId

    UNION ALL

    -- Recursive member: предки II..VI поколений (до глубины @MaxGenerations = 6)
    SELECT
        gp.Id AS AnimalId,
        pt.AnimalId AS ChildId,
        pt.GenerationDepth + 1 AS GenerationDepth,
        CAST(pt.PathCode + CASE WHEN gp.Id = pt.SireId THEN 'F' ELSE 'M' END AS varchar(16)) AS PathCode,
        pt.LineSide,
        gp.TagNumber,
        gp.Name,
        gp.Breed,
        gp.Sex,
        gp.BirthDate,
        gp.LineageCode,
        gp.SireId,
        gp.DamId,
        CAST(pt.VisitedIds + CAST(gp.Id AS varchar(36)) + '|' AS varchar(600)) AS VisitedIds
    FROM PedigreeTree pt
    INNER JOIN dbo.Animals gp ON gp.Id IN (pt.SireId, pt.DamId)
    WHERE pt.GenerationDepth < @MaxGenerations
      AND CHARINDEX('|' + CAST(gp.Id AS varchar(36)) + '|', pt.VisitedIds) = 0
)
SELECT
    AnimalId,
    ChildId,
    GenerationDepth,
    PathCode,
    LineSide,
    TagNumber,
    Name,
    Breed,
    Sex,
    BirthDate,
    LineageCode,
    SireId,
    DamId,
    COUNT(*) OVER (PARTITION BY AnimalId) AS OccurrenceCount
FROM PedigreeTree
ORDER BY GenerationDepth, PathCode;";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            var cmd = new CommandDefinition(sql, new { AnimalId = animalId, MaxGenerations = maxGenerations }, cancellationToken: ct);
            var nodes = (await conn.QueryAsync<PedigreeTreeNodeDto>(cmd)).ToList();

            var repeated = nodes
                .Where(n => n.OccurrenceCount > 1)
                .Select(n => $"{n.Name} [{n.TagNumber}] ({n.OccurrenceCount}x)")
                .Distinct()
                .ToList();

            return new PedigreeTreeViewDto(subject, nodes, repeated);
        }

        // Резервный расчет в памяти (для InMemory тестов без SQL Server)
        return await BuildPedigreeInMemoryFallbackAsync(subject, maxGenerations, ct);
    }

    /// <summary>
    /// Рассчитывает коэффициент инбридинга С. Райта (%) и степень по Шапоружу для выбранной матки
    /// против всех быков-производителей стада прямо на уровне MS SQL Server через рекурсивный CTE WITH PedigreeTree AS (...).
    /// </summary>
    public async Task<IReadOnlyList<CandidateSireEvaluationDto>> EvaluateCandidateSiresForDamAsync(
        Guid damId,
        int maxGenerations = 6,
        CancellationToken ct = default)
    {
        maxGenerations = Math.Clamp(maxGenerations, 1, 6);

        if (_db.Database.IsRelational() && !string.IsNullOrWhiteSpace(_connectionString))
        {
            const string sql = @"
WITH PedigreeTree AS (
    -- Anchor: выбранная матка (Dam) и все действующие быки-производители (Depth = 0)
    SELECT
        root.Id AS RootParentId,
        root.Id AS AncestorId,
        0 AS Depth,
        CAST('' AS varchar(600)) AS PriorNodes,
        CAST('|' + CAST(root.Id AS varchar(36)) + '|' AS varchar(600)) AS VisitedNodes
    FROM dbo.Animals root
    WHERE root.Id = @DamId
       OR (root.Sex = 2 AND root.IsHistoricalPedigreeOnly = 0 AND root.HealthStatus <> 5)

    UNION ALL

    -- Recursive: подъем по ветвям отца и матери на глубину до 6 поколений
    SELECT
        pt.RootParentId,
        anc.Id AS AncestorId,
        pt.Depth + 1 AS Depth,
        CAST(pt.PriorNodes + CASE WHEN pt.PriorNodes = '' THEN '|' ELSE '' END + CAST(pt.AncestorId AS varchar(36)) + '|' AS varchar(600)) AS PriorNodes,
        CAST(pt.VisitedNodes + CAST(anc.Id AS varchar(36)) + '|' AS varchar(600)) AS VisitedNodes
    FROM PedigreeTree pt
    INNER JOIN dbo.Animals cur ON cur.Id = pt.AncestorId
    INNER JOIN dbo.Animals anc ON anc.Id IN (cur.SireId, cur.DamId)
    WHERE pt.Depth < @MaxGenerations
      AND CHARINDEX('|' + CAST(anc.Id AS varchar(36)) + '|', pt.VisitedNodes) = 0
),
DamPaths AS (
    SELECT AncestorId, Depth AS DamDepth, PriorNodes AS DamPriorNodes
    FROM PedigreeTree
    WHERE RootParentId = @DamId
),
SirePaths AS (
    SELECT RootParentId AS SireId, AncestorId, Depth AS SireDepth, PriorNodes AS SirePriorNodes
    FROM PedigreeTree
    WHERE RootParentId <> @DamId
),
IndependentWrightLoops AS (
    -- Независимые простые пути С. Райта через общего предка A без повторного пересечения промежуточных узлов:
    -- вклад пути = (1/2)^(DamDepth + SireDepth + 1)
    SELECT
        sp.SireId,
        dp.AncestorId,
        dp.DamDepth,
        sp.SireDepth,
        POWER(CAST(0.5 AS float), dp.DamDepth + sp.SireDepth + 1) AS PathContribution
    FROM DamPaths dp
    INNER JOIN SirePaths sp ON sp.AncestorId = dp.AncestorId
    WHERE CHARINDEX('|' + CAST(dp.AncestorId AS varchar(36)) + '|', sp.SirePriorNodes) = 0
      AND CHARINDEX('|' + CAST(sp.AncestorId AS varchar(36)) + '|', dp.DamPriorNodes) = 0
      AND NOT EXISTS (
          SELECT 1
          FROM STRING_SPLIT(dp.DamPriorNodes, '|') dNode
          INNER JOIN STRING_SPLIT(sp.SirePriorNodes, '|') sNode ON dNode.value = sNode.value
          WHERE dNode.value <> ''
      )
)
SELECT
    sire.Id AS SireId,
    sire.TagNumber AS SireTag,
    sire.Name AS SireName,
    sire.Breed AS SireBreed,
    sire.LineageCode AS SireLineage,
    anc.Id AS AncestorId,
    anc.TagNumber AS AncestorTag,
    anc.Name AS AncestorName,
    anc.Breed AS AncestorBreed,
    iwl.DamDepth,
    iwl.SireDepth,
    iwl.PathContribution
FROM dbo.Animals sire
LEFT JOIN IndependentWrightLoops iwl ON iwl.SireId = sire.Id
LEFT JOIN dbo.Animals anc ON anc.Id = iwl.AncestorId
WHERE sire.Sex = 2
  AND sire.IsHistoricalPedigreeOnly = 0
  AND sire.HealthStatus <> 5
ORDER BY sire.Name, iwl.PathContribution DESC;";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            var cmd = new CommandDefinition(sql, new { DamId = damId, MaxGenerations = maxGenerations }, cancellationToken: ct);
            var rows = (await conn.QueryAsync<SqlSireLoopRow>(cmd)).ToList();

            var evaluations = rows
                .GroupBy(r => new { r.SireId, r.SireTag, r.SireName, r.SireBreed, r.SireLineage })
                .Select(group =>
                {
                    var ancestorItems = group
                        .Where(r => r.AncestorId.HasValue && r.PathContribution.HasValue)
                        .Select(r => new CandidateCommonAncestorDto(
                            r.AncestorId!.Value,
                            r.AncestorTag ?? string.Empty,
                            r.AncestorName ?? string.Empty,
                            r.AncestorBreed ?? string.Empty,
                            r.DamDepth!.Value,
                            r.SireDepth!.Value,
                            InbreedingCalculator.FormatShaporuzh(r.DamDepth.Value, r.SireDepth.Value),
                            Math.Round(r.PathContribution!.Value * 100.0, 4)))
                        .OrderByDescending(x => x.ContributionPercent)
                        .ToList();

                    var fxPercent = Math.Round(ancestorItems.Sum(a => a.ContributionPercent), 4);
                    var risk = InbreedingCalculator.ClassifyRisk(fxPercent);
                    var shaporuzh = ancestorItems.Count == 0
                        ? "Аутбридинг (общих предков нет)"
                        : string.Join("; ", ancestorItems.Select(a => $"{a.AncestorName}: {a.ShaporuzhDegree}").Distinct());

                    var rec = InbreedingCalculator.BuildRecommendation(risk, fxPercent, ancestorItems.Count);

                    return new CandidateSireEvaluationDto(
                        group.Key.SireId,
                        group.Key.SireTag,
                        group.Key.SireName,
                        group.Key.SireBreed,
                        group.Key.SireLineage,
                        fxPercent,
                        risk,
                        shaporuzh,
                        ancestorItems,
                        rec);
                })
                .OrderBy(e => e.InbreedingPercent)
                .ThenBy(e => e.SireName)
                .ToList();

            return evaluations;
        }

        // Резервный расчет через доменный InbreedingCalculator (для InMemory тестов)
        var allAnimals = await _db.Animals.AsNoTracking().ToListAsync(ct);
        var book = allAnimals.ToDictionary(
            a => a.Id,
            a => new PedigreeNodeSnapshot(a.Id, a.TagNumber, a.Name, a.Sex, a.Breed, a.SireId, a.DamId));

        var bulls = allAnimals
            .Where(a => a.Sex == AnimalSex.Male && !a.IsHistoricalPedigreeOnly && a.HealthStatus != HealthStatus.Culled)
            .ToList();

        return bulls
            .Select(bull =>
            {
                var eval = InbreedingCalculator.EvaluatePair(damId, bull.Id, book, maxGenerations);
                var ancestors = eval.CommonAncestorLoops.Select(l => new CandidateCommonAncestorDto(
                    l.AncestorId,
                    l.AncestorTag,
                    l.AncestorName,
                    l.AncestorBreed,
                    l.DamDistanceGenerations,
                    l.SireDistanceGenerations,
                    l.ShaporuzhDegree,
                    Math.Round(l.PathContributionCoefficient * 100.0, 4))).ToList();

                return new CandidateSireEvaluationDto(
                    bull.Id,
                    bull.TagNumber,
                    bull.Name,
                    bull.Breed,
                    bull.LineageCode,
                    eval.CoefficientPercent,
                    eval.RiskLevel,
                    eval.ShaporuzhSummary,
                    ancestors,
                    eval.ZootechnicalRecommendation);
            })
            .OrderBy(e => e.InbreedingPercent)
            .ToList();
    }

    public async Task<IReadOnlyList<BreedingPlan>> GetBreedingPlansAsync(CancellationToken ct = default)
    {
        return await _db.BreedingPlans
            .AsNoTracking()
            .Include(p => p.Dam)
            .Include(p => p.Sire)
            .OrderByDescending(p => p.PlannedAtUtc)
            .Take(25)
            .ToListAsync(ct);
    }

    public async Task<BreedingPlan> SaveBreedingPlanAsync(SaveBreedingPlanCommand command, CancellationToken ct = default)
    {
        var validation = await _planValidator.ValidateAsync(command, ct);
        if (!validation.IsValid)
        {
            throw new DomainRuleException(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var dam = await _db.Animals.AsNoTracking().FirstOrDefaultAsync(a => a.Id == command.DamId, ct)
            ?? throw new DomainRuleException("Матка не найдена.");
        var sire = await _db.Animals.AsNoTracking().FirstOrDefaultAsync(a => a.Id == command.SireId, ct)
            ?? throw new DomainRuleException("Бык-производитель не найден.");

        if (dam.Sex != AnimalSex.Female || sire.Sex != AnimalSex.Male)
        {
            throw new DomainRuleException("Для племенного подбора требуются самка (матка) и самец (бык-производитель).");
        }

        var candidates = await EvaluateCandidateSiresForDamAsync(command.DamId, 6, ct);
        var match = candidates.FirstOrDefault(c => c.SireId == command.SireId);

        double fxPercent;
        InbreedingRiskLevel risk;
        string shaporuzh;
        string ancestorsSummary;

        if (match is not null)
        {
            fxPercent = match.InbreedingPercent;
            risk = match.RiskLevel;
            shaporuzh = match.ShaporuzhSummary;
            ancestorsSummary = match.CommonAncestors.Count == 0
                ? "Общих предков до VI колена не выявлено"
                : string.Join(", ", match.CommonAncestors.Select(a => $"{a.AncestorName} [{a.AncestorTag}] ({a.ShaporuzhDegree}, +{a.ContributionPercent:0.##}%)"));
        }
        else
        {
            var all = await _db.Animals.AsNoTracking().ToDictionaryAsync(
                a => a.Id,
                a => new PedigreeNodeSnapshot(a.Id, a.TagNumber, a.Name, a.Sex, a.Breed, a.SireId, a.DamId),
                ct);
            var dom = InbreedingCalculator.EvaluatePair(command.DamId, command.SireId, all, 6);
            fxPercent = dom.CoefficientPercent;
            risk = dom.RiskLevel;
            shaporuzh = dom.ShaporuzhSummary;
            ancestorsSummary = dom.CommonAncestorLoops.Count == 0
                ? "Общих предков до VI колена не выявлено"
                : string.Join(", ", dom.CommonAncestorLoops.Select(a => $"{a.AncestorName} ({a.ShaporuzhDegree})"));
        }

        if (risk == InbreedingRiskLevel.Critical)
        {
            throw new InbreedingHazardException(
                fxPercent,
                $"АНТИ-ИНБРИДИНГ БЛОКИРОВКА! Закрепление пары {dam.Name} × {sire.Name} запрещено: коэффициент инбридинга Райта составляет {fxPercent:0.##}% ({shaporuzh}), что превышает критический порог кровосмешения 6.25%!");
        }

        var status = risk == InbreedingRiskLevel.Safe
            ? BreedingPlanStatus.Recommended
            : BreedingPlanStatus.ApprovedWithCaution;

        var plan = new BreedingPlan
        {
            DamId = dam.Id,
            SireId = sire.Id,
            InbreedingCoefficientPercent = (decimal)fxPercent,
            ShaporuzhNotation = shaporuzh.Length > 120 ? shaporuzh[..117] + "..." : shaporuzh,
            RiskLevel = risk,
            Status = status,
            CommonAncestorsSummary = ancestorsSummary.Length > 400 ? ancestorsSummary[..397] + "..." : ancestorsSummary,
            PlannedAtUtc = DateTime.UtcNow,
            CreatedBySelector = string.IsNullOrWhiteSpace(command.SelectorName) ? "Зоотехник-селекционер" : command.SelectorName.Trim(),
            Notes = command.Notes?.Trim()
        };

        _db.BreedingPlans.Add(plan);
        await _db.SaveChangesAsync(ct);
        return plan;
    }

    private async Task<PedigreeTreeViewDto> BuildPedigreeInMemoryFallbackAsync(
        Animal subject,
        int maxGenerations,
        CancellationToken ct)
    {
        var all = await _db.Animals.AsNoTracking().ToDictionaryAsync(a => a.Id, ct);
        var nodes = new List<PedigreeTreeNodeDto>();

        void Traverse(Animal child, int depth, string pathCode, string lineSide, HashSet<Guid> visited)
        {
            if (depth > maxGenerations)
            {
                return;
            }

            foreach (var (parentId, codeSuffix) in new[] { (child.SireId, "F"), (child.DamId, "M") })
            {
                if (!parentId.HasValue || !all.TryGetValue(parentId.Value, out var parent) || visited.Contains(parent.Id))
                {
                    continue;
                }

                var side = depth == 1 ? (codeSuffix == "F" ? "Отцовская ветвь" : "Материнская ветвь") : lineSide;
                var code = pathCode + codeSuffix;
                nodes.Add(new PedigreeTreeNodeDto
                {
                    AnimalId = parent.Id,
                    ChildId = child.Id,
                    GenerationDepth = depth,
                    PathCode = code,
                    LineSide = side,
                    TagNumber = parent.TagNumber,
                    Name = parent.Name,
                    Breed = parent.Breed,
                    Sex = parent.Sex,
                    BirthDate = parent.BirthDate,
                    LineageCode = parent.LineageCode,
                    SireId = parent.SireId,
                    DamId = parent.DamId
                });

                var nextVisited = new HashSet<Guid>(visited) { parent.Id };
                Traverse(parent, depth + 1, code, side, nextVisited);
            }
        }

        Traverse(subject, 1, string.Empty, string.Empty, new HashSet<Guid> { subject.Id });
        var counts = nodes.GroupBy(n => n.AnimalId).ToDictionary(g => g.Key, g => g.Count());
        foreach (var n in nodes)
        {
            n.OccurrenceCount = counts[n.AnimalId];
        }

        var repeated = nodes
            .Where(n => n.OccurrenceCount > 1)
            .Select(n => $"{n.Name} [{n.TagNumber}] ({n.OccurrenceCount}x)")
            .Distinct()
            .ToList();

        return new PedigreeTreeViewDto(subject, nodes.OrderBy(n => n.GenerationDepth).ThenBy(n => n.PathCode).ToList(), repeated);
    }

    private sealed class SqlSireLoopRow
    {
        public Guid SireId { get; set; }
        public string SireTag { get; set; } = string.Empty;
        public string SireName { get; set; } = string.Empty;
        public string SireBreed { get; set; } = string.Empty;
        public string? SireLineage { get; set; }
        public Guid? AncestorId { get; set; }
        public string? AncestorTag { get; set; }
        public string? AncestorName { get; set; }
        public string? AncestorBreed { get; set; }
        public int? DamDepth { get; set; }
        public int? SireDepth { get; set; }
        public double? PathContribution { get; set; }
    }
}
