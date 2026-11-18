using VetGuard.Domain.Enums;
using VetGuard.Domain.Services;

namespace VetGuard.Tests;

public sealed class InbreedingAndPedigreeTests
{
    private static PedigreeNodeSnapshot Node(Guid id, string name, AnimalSex sex, Guid? sireId = null, Guid? damId = null)
        => new(id, $"TAG-{name}", name, sex, "Голштинская", sireId, damId);

    [Fact]
    public void EvaluatePair_UnrelatedParents_ShouldReturnZeroOutbreeding()
    {
        var sireFather = Guid.NewGuid();
        var damFather = Guid.NewGuid();
        var sire = Guid.NewGuid();
        var dam = Guid.NewGuid();

        var book = new Dictionary<Guid, PedigreeNodeSnapshot>
        {
            [sireFather] = Node(sireFather, "Норд", AnimalSex.Male),
            [damFather] = Node(damFather, "Цезарь", AnimalSex.Male),
            [sire] = Node(sire, "Алмаз", AnimalSex.Male, sireId: sireFather),
            [dam] = Node(dam, "Байкалка", AnimalSex.Female, sireId: damFather)
        };

        var result = InbreedingCalculator.EvaluatePair(dam, sire, book);

        Assert.Equal(0.0, result.CoefficientPercent);
        Assert.Equal(InbreedingRiskLevel.Safe, result.RiskLevel);
        Assert.Empty(result.CommonAncestorLoops);
    }

    [Fact]
    public void EvaluatePair_SireWithOwnDaughter_ShouldReturn25PercentCritical()
    {
        var sire = Guid.NewGuid();
        var daughter = Guid.NewGuid();

        var book = new Dictionary<Guid, PedigreeNodeSnapshot>
        {
            [sire] = Node(sire, "Олимп", AnimalSex.Male),
            [daughter] = Node(daughter, "ДочьОлимпа", AnimalSex.Female, sireId: sire)
        };

        var result = InbreedingCalculator.EvaluatePair(daughter, sire, book);

        Assert.Equal(25.0, result.CoefficientPercent, precision: 3);
        Assert.Equal(InbreedingRiskLevel.Critical, result.RiskLevel);
        Assert.Contains("II-I", result.ShaporuzhSummary);
    }

    [Fact]
    public void EvaluatePair_HalfSiblingsWithGrandparents_ShouldReturnExact12Point5PercentWithoutDoubleCounting()
    {
        // Дед и бабка по общему отцу НЕ должны удваивать вклад через того же общего отца!
        var grandSire = Guid.NewGuid();
        var grandDam = Guid.NewGuid();
        var sharedFather = Guid.NewGuid();
        var motherA = Guid.NewGuid();
        var motherB = Guid.NewGuid();
        var halfBrother = Guid.NewGuid();
        var halfSister = Guid.NewGuid();

        var book = new Dictionary<Guid, PedigreeNodeSnapshot>
        {
            [grandSire] = Node(grandSire, "Султан", AnimalSex.Male),
            [grandDam] = Node(grandDam, "Мальва", AnimalSex.Female),
            [sharedFather] = Node(sharedFather, "Цезарь", AnimalSex.Male, grandSire, grandDam),
            [motherA] = Node(motherA, "Двина", AnimalSex.Female),
            [motherB] = Node(motherB, "Снежана", AnimalSex.Female),
            [halfBrother] = Node(halfBrother, "Буран", AnimalSex.Male, sharedFather, motherB),
            [halfSister] = Node(halfSister, "Байкалка", AnimalSex.Female, sharedFather, motherA)
        };

        var result = InbreedingCalculator.EvaluatePair(halfSister, halfBrother, book);

        Assert.Equal(12.5, result.CoefficientPercent, precision: 4);
        Assert.Single(result.CommonAncestorLoops);
        Assert.Equal("II-II", result.CommonAncestorLoops[0].ShaporuzhDegree);
        Assert.Equal(InbreedingRiskLevel.Critical, result.RiskLevel);
    }

    [Fact]
    public void EvaluatePair_FullSiblings_ShouldReturn25PercentViaTwoParents()
    {
        var father = Guid.NewGuid();
        var mother = Guid.NewGuid();
        var brother = Guid.NewGuid();
        var sister = Guid.NewGuid();

        var book = new Dictionary<Guid, PedigreeNodeSnapshot>
        {
            [father] = Node(father, "Отец", AnimalSex.Male),
            [mother] = Node(mother, "Мать", AnimalSex.Female),
            [brother] = Node(brother, "Брат", AnimalSex.Male, father, mother),
            [sister] = Node(sister, "Сестра", AnimalSex.Female, father, mother)
        };

        var result = InbreedingCalculator.EvaluatePair(sister, brother, book);

        Assert.Equal(25.0, result.CoefficientPercent, precision: 4);
        Assert.Equal(2, result.CommonAncestorLoops.Count);
        Assert.Equal(InbreedingRiskLevel.Critical, result.RiskLevel);
    }

    [Fact]
    public void EvaluatePair_ModerateLinebreeding_IV_III_ShouldReturn1Point5625PercentModerate()
    {
        // Общий предок Султан: у матки в III колене предков (DamDepth = 3, т.е. IV ряд родословной потомка),
        // у быка во II колене предков (SireDepth = 2, т.е. III ряд родословной потомка) -> IV-III, F = (1/2)^6 = 1.5625%
        var sultan = Guid.NewGuid();
        var olimp = Guid.NewGuid();
        var cezar = Guid.NewGuid();
        var dam = Guid.NewGuid();

        var kedr = Guid.NewGuid();
        var sire = Guid.NewGuid();

        var book = new Dictionary<Guid, PedigreeNodeSnapshot>
        {
            [sultan] = Node(sultan, "Султан", AnimalSex.Male),
            [olimp] = Node(olimp, "Олимп", AnimalSex.Male, sireId: sultan),
            [cezar] = Node(cezar, "Цезарь", AnimalSex.Male, sireId: olimp),
            [dam] = Node(dam, "Байкалка", AnimalSex.Female, sireId: cezar),
            [kedr] = Node(kedr, "Кедр", AnimalSex.Male, sireId: sultan),
            [sire] = Node(sire, "Енисей", AnimalSex.Male, sireId: kedr)
        };

        var result = InbreedingCalculator.EvaluatePair(dam, sire, book, maxGenerations: 6);

        Assert.Equal(1.5625, result.CoefficientPercent, precision: 4);
        Assert.Equal(InbreedingRiskLevel.Moderate, result.RiskLevel);
        Assert.Contains("IV-III", result.ShaporuzhSummary);
    }
}
