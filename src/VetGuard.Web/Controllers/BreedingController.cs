using Microsoft.AspNetCore.Mvc;
using VetGuard.Application.Interfaces;
using VetGuard.Application.Models;
using VetGuard.Domain.Exceptions;
using VetGuard.Web.Models;

namespace VetGuard.Web.Controllers;

public sealed class BreedingController : Controller
{
    private readonly IPedigreeService _pedigreeService;
    private readonly IHerdService _herdService;

    public BreedingController(IPedigreeService pedigreeService, IHerdService herdService)
    {
        _pedigreeService = pedigreeService;
        _herdService = herdService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid? subjectId = null, Guid? damId = null, CancellationToken ct = default)
    {
        var active = await _herdService.GetAllActiveAnimalsForSelectionAsync(ct);
        var females = await _herdService.GetBreedingFemalesAsync(ct);

        var defaultCow = females.FirstOrDefault(f => f.TagNumber == "RU-38-IRGAU-0107") ?? females.FirstOrDefault();
        var chosenSubjectId = subjectId ?? defaultCow?.Id ?? active.FirstOrDefault()?.Id ?? Guid.Empty;
        var chosenDamId = damId ?? defaultCow?.Id ?? Guid.Empty;

        var tree = chosenSubjectId != Guid.Empty
            ? await _pedigreeService.GetPedigreeTreeAsync(chosenSubjectId, 6, ct)
            : null;

        var candidates = chosenDamId != Guid.Empty
            ? await _pedigreeService.EvaluateCandidateSiresForDamAsync(chosenDamId, 6, ct)
            : Array.Empty<CandidateSireEvaluationDto>();

        var plans = await _pedigreeService.GetBreedingPlansAsync(ct);

        return View(new BreedingIndexViewModel
        {
            AllPedigreeSubjects = active,
            BreedingFemales = females,
            SelectedSubjectId = chosenSubjectId,
            SelectedDamId = chosenDamId,
            InitialTree = tree,
            InitialCandidates = candidates,
            SavedPlans = plans
        });
    }

    [HttpGet]
    public async Task<IActionResult> PedigreePartial(Guid subjectId, CancellationToken ct)
    {
        var tree = await _pedigreeService.GetPedigreeTreeAsync(subjectId, 6, ct);
        if (tree is null)
        {
            return NotFound();
        }

        return PartialView("_PedigreeTree", tree);
    }

    [HttpGet]
    public async Task<IActionResult> EvaluatePairsPartial(Guid damId, CancellationToken ct)
    {
        var candidates = await _pedigreeService.EvaluateCandidateSiresForDamAsync(damId, 6, ct);
        ViewBag.DamId = damId;
        return PartialView("_SireEvaluationTable", candidates);
    }

    [HttpPost]
    public async Task<IActionResult> SavePlan(SaveBreedingPlanCommand command, CancellationToken ct)
    {
        try
        {
            command.SelectorName = User.Identity?.Name ?? "Ильина Е.С. (Зоотехник-селекционер)";
            var plan = await _pedigreeService.SaveBreedingPlanAsync(command, ct);
            TempData["SuccessMessage"] =
                $"Племенная пара успешно закреплена в плане осеменения (F = {plan.InbreedingCoefficientPercent:0.##}%, {plan.ShaporuzhNotation}).";
        }
        catch (DomainRuleException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { subjectId = command.DamId, damId = command.DamId });
    }
}
