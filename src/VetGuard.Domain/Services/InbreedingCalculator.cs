using VetGuard.Domain.Enums;

namespace VetGuard.Domain.Services;

public sealed record PedigreeNodeSnapshot(
    Guid Id,
    string TagNumber,
    string Name,
    AnimalSex Sex,
    string Breed,
    Guid? SireId,
    Guid? DamId);

public sealed record CommonAncestorLoopResult(
    Guid AncestorId,
    string AncestorTag,
    string AncestorName,
    string AncestorBreed,
    int DamDistanceGenerations,
    int SireDistanceGenerations,
    double AncestorInbreedingFa,
    double PathContributionCoefficient,
    string ShaporuzhDegree);

public sealed record InbreedingEvaluationResult(
    Guid DamId,
    Guid SireId,
    double CoefficientFraction,
    double CoefficientPercent,
    InbreedingRiskLevel RiskLevel,
    string ShaporuzhSummary,
    IReadOnlyList<CommonAncestorLoopResult> CommonAncestorLoops,
    string ZootechnicalRecommendation);

/// <summary>
/// Чистый доменный вычислитель коэффициента инбридинга Сьюэлла Райта:
/// F_X = SUM [ (1/2)^(n_Sire + n_Dam + 1) * (1 + F_A) ]
/// по всем простым независимым генеалогическим путям до глубины maxGenerations (по умолчанию 6 поколений),
/// а также классификатор степени родства по Шапоружу (римская нотация поколений Мать - Отец).
/// </summary>
public static class InbreedingCalculator
{
    public const int DefaultMaxGenerations = 6;

    public static InbreedingEvaluationResult EvaluatePair(
        Guid damId,
        Guid sireId,
        IReadOnlyDictionary<Guid, PedigreeNodeSnapshot> pedigreeBook,
        int maxGenerations = DefaultMaxGenerations)
    {
        var memoizedFa = new Dictionary<Guid, double>();
        var loops = FindIndependentLoops(damId, sireId, pedigreeBook, maxGenerations, memoizedFa);

        var totalFx = loops.Sum(x => x.PathContributionCoefficient);
        var percent = Math.Round(totalFx * 100.0, 4);
        var riskLevel = ClassifyRisk(percent);

        var shaporuzh = loops.Count == 0
            ? "Аутбридинг (общих предков нет)"
            : string.Join("; ", loops
                .OrderByDescending(l => l.PathContributionCoefficient)
                .Select(l => $"{l.AncestorName}: {l.ShaporuzhDegree}")
                .Distinct());

        var recommendation = BuildRecommendation(riskLevel, percent, loops.Count);

        return new InbreedingEvaluationResult(
            damId,
            sireId,
            totalFx,
            percent,
            riskLevel,
            shaporuzh,
            loops,
            recommendation);
    }

    public static InbreedingRiskLevel ClassifyRisk(double fxPercent)
    {
        if (fxPercent >= 6.25)
        {
            return InbreedingRiskLevel.Critical;
        }

        if (fxPercent > 3.125)
        {
            return InbreedingRiskLevel.High;
        }

        if (fxPercent >= 1.5)
        {
            return InbreedingRiskLevel.Moderate;
        }

        return InbreedingRiskLevel.Safe;
    }

    /// <summary>
    /// Запись степени инбридинга по Шапоружу: материнские ряды предков пишутся слева, отцовские — справа.
    /// Родители = I ряд (n = 0 от родителя, т.е. поколение в родословной потомка = n + 1), деды = II ряд, прадеды = III ряд и т.д.
    /// </summary>
    public static string FormatShaporuzh(int damStepsToAncestor, int sireStepsToAncestor)
    {
        return $"{ToRoman(damStepsToAncestor + 1)}-{ToRoman(sireStepsToAncestor + 1)}";
    }

    public static string BuildRecommendation(InbreedingRiskLevel level, double fxPercent, int commonCount)
    {
        return level switch
        {
            InbreedingRiskLevel.Safe when commonCount == 0 =>
                "Чистый аутбридинг (F = 0%). Идеальное сочетание для сохранения гетерозиса, жизнеспособности телят и молочной продуктивности.",
            InbreedingRiskLevel.Safe =>
                $"Отдаленное родство (F = {fxPercent:0.##}% < 1.5%). Безопасный подбор пары без риска инбредной депрессии.",
            InbreedingRiskLevel.Moderate =>
                $"Умеренный лайнбридинг (F = {fxPercent:0.##}%). Допускается в племенной работе для закрепления выдающейся генетической линии под контролем селекционера.",
            InbreedingRiskLevel.High =>
                $"ОПАСНО: Близкий инбридинг (F = {fxPercent:0.##}%). Высокий риск снижения фертильности, ослабления иммунитета и мертворожденности. Рекомендуется заменить быка.",
            _ =>
                $"КРИТИЧЕСКИЙ ЗАПРЕТ: Тесное кровосмешение (F = {fxPercent:0.##}% >= 6.25%)! Высок риск летальных рецессивных аномалий и уродств приплода. Случка/осеменение блокируется."
        };
    }

    private static List<CommonAncestorLoopResult> FindIndependentLoops(
        Guid damId,
        Guid sireId,
        IReadOnlyDictionary<Guid, PedigreeNodeSnapshot> pedigreeBook,
        int maxGenerations,
        Dictionary<Guid, double> memoizedFa)
    {
        var damPaths = BuildSimplePaths(damId, pedigreeBook, maxGenerations);
        var sirePaths = BuildSimplePaths(sireId, pedigreeBook, maxGenerations);

        var sireByEndpoint = sirePaths.ToLookup(p => p.EndpointId);
        var results = new List<CommonAncestorLoopResult>();

        foreach (var dPath in damPaths)
        {
            foreach (var sPath in sireByEndpoint[dPath.EndpointId])
            {
                // Путь Dam -> ... -> A <- ... <- Sire является простым независимым циклом Райта
                // тогда и только тогда, когда множества промежуточных узлов строго до A не пересекаются
                // и сам A не лежит среди промежуточных узлов другой ветви.
                if (dPath.PriorNodes.Contains(sPath.EndpointId) || sPath.PriorNodes.Contains(dPath.EndpointId))
                {
                    continue;
                }

                if (dPath.PriorNodes.Overlaps(sPath.PriorNodes))
                {
                    continue;
                }

                if (!pedigreeBook.TryGetValue(dPath.EndpointId, out var ancestor))
                {
                    continue;
                }

                var fa = ComputeIndividualInbreeding(ancestor.Id, pedigreeBook, maxGenerations, memoizedFa);
                var exponent = sPath.Depth + dPath.Depth + 1;
                var contribution = Math.Pow(0.5, exponent) * (1.0 + fa);

                results.Add(new CommonAncestorLoopResult(
                    ancestor.Id,
                    ancestor.TagNumber,
                    ancestor.Name,
                    ancestor.Breed,
                    dPath.Depth,
                    sPath.Depth,
                    fa,
                    contribution,
                    FormatShaporuzh(dPath.Depth, sPath.Depth)));
            }
        }

        return results
            .OrderByDescending(r => r.PathContributionCoefficient)
            .ThenBy(r => r.DamDistanceGenerations + r.SireDistanceGenerations)
            .ToList();
    }

    private static double ComputeIndividualInbreeding(
        Guid animalId,
        IReadOnlyDictionary<Guid, PedigreeNodeSnapshot> pedigreeBook,
        int maxGenerations,
        Dictionary<Guid, double> memoizedFa)
    {
        if (memoizedFa.TryGetValue(animalId, out var cached))
        {
            return cached;
        }

        // Защита от некорректных циклов данных
        memoizedFa[animalId] = 0.0;

        if (!pedigreeBook.TryGetValue(animalId, out var node) || !node.DamId.HasValue || !node.SireId.HasValue)
        {
            return 0.0;
        }

        var parentLoops = FindIndependentLoops(node.DamId.Value, node.SireId.Value, pedigreeBook, Math.Max(1, maxGenerations - 1), memoizedFa);
        var fx = parentLoops.Sum(l => l.PathContributionCoefficient);
        memoizedFa[animalId] = fx;
        return fx;
    }

    private sealed record AncestorPath(Guid EndpointId, int Depth, HashSet<Guid> PriorNodes);

    private static List<AncestorPath> BuildSimplePaths(
        Guid startId,
        IReadOnlyDictionary<Guid, PedigreeNodeSnapshot> pedigreeBook,
        int maxDepth)
    {
        var allPaths = new List<AncestorPath>();
        var queue = new Queue<AncestorPath>();
        queue.Enqueue(new AncestorPath(startId, 0, new HashSet<Guid>()));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            allPaths.Add(current);

            if (current.Depth >= maxDepth || !pedigreeBook.TryGetValue(current.EndpointId, out var node))
            {
                continue;
            }

            foreach (var parentId in new[] { node.SireId, node.DamId })
            {
                if (!parentId.HasValue)
                {
                    continue;
                }

                var pid = parentId.Value;
                if (pid == current.EndpointId || current.PriorNodes.Contains(pid))
                {
                    continue;
                }

                var nextPrior = new HashSet<Guid>(current.PriorNodes) { current.EndpointId };
                queue.Enqueue(new AncestorPath(pid, current.Depth + 1, nextPrior));
            }
        }

        return allPaths;
    }

    public static string ToRoman(int number) => number switch
    {
        1 => "I",
        2 => "II",
        3 => "III",
        4 => "IV",
        5 => "V",
        6 => "VI",
        7 => "VII",
        8 => "VIII",
        _ => number.ToString()
    };
}
