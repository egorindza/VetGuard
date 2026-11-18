using Microsoft.AspNetCore.Mvc;
using VetGuard.Application.Interfaces;
using VetGuard.Application.Models;
using VetGuard.Domain.Enums;
using VetGuard.Domain.Exceptions;
using VetGuard.Web.Models;

namespace VetGuard.Web.Controllers;

public sealed class EpizooticsController : Controller
{
    private readonly IEpizooticService _epizooticService;
    private readonly ITreatmentAndDrugService _drugService;
    private readonly IHerdService _herdService;
    private readonly IPdfReportService _pdfService;

    public EpizooticsController(
        IEpizooticService epizooticService,
        ITreatmentAndDrugService drugService,
        IHerdService herdService,
        IPdfReportService pdfService)
    {
        _epizooticService = epizooticService;
        _drugService = drugService;
        _herdService = herdService;
        _pdfService = pdfService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var plans = await _epizooticService.GetEpizooticCalendarPlanAsync(ct);
        var isolator = await _epizooticService.GetQuarantineIsolatorAnimalsAsync(ct);
        var acts = await _epizooticService.GetRecentActsAsync(25, ct);
        var allDrugs = await _drugService.GetDrugsAsync(ct);
        var allActive = await _herdService.GetAllActiveAnimalsForSelectionAsync(ct);

        var biologics = allDrugs
            .Where(d => d.Category == DrugCategory.Vaccine || d.Category == DrugCategory.DiagnosticAllergen || d.Category == DrugCategory.Antiparasitic)
            .ToList();

        return View(new EpizooticsIndexViewModel
        {
            Plans = plans,
            IsolatorAnimals = isolator,
            RecentActs = acts,
            BiologicsCatalog = biologics.Count > 0 ? biologics : allDrugs,
            NonIsolatedAnimals = allActive.Where(a => a.HealthStatus != HealthStatus.Quarantine).ToList()
        });
    }

    /// <summary>
    /// 1-й клик групповой обработки: открывает HTMX-модальное окно с предвыбранным списком животных и серией вакцины.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> PrepareGroupModal(Guid ruleId, CancellationToken ct)
    {
        var plans = await _epizooticService.GetEpizooticCalendarPlanAsync(ct);
        var plan = plans.FirstOrDefault(p => p.RuleId == ruleId);
        if (plan is null)
        {
            return NotFound();
        }

        var allDrugs = await _drugService.GetDrugsAsync(ct);
        var biologics = allDrugs
            .Where(d => d.Category == DrugCategory.Vaccine || d.Category == DrugCategory.DiagnosticAllergen)
            .ToList();

        return PartialView("_GroupActModal", new GroupEpizooticModalViewModel
        {
            Plan = plan,
            Biologics = biologics.Count > 0 ? biologics : allDrugs
        });
    }

    /// <summary>
    /// 2-й клик групповой обработки: списывает серию вакцины (rowversion) и формирует официальный Акт.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ExecuteGroupAct(ExecuteGroupEpizooticActCommand command, CancellationToken ct)
    {
        try
        {
            command.ExecutedByVeterinarian = User.Identity?.Name ?? "Д-р Соколов А.В., к.в.н.";
            var act = await _epizooticService.ExecuteGroupActAsync(command, ct);
            TempData["SuccessMessage"] =
                $"Групповая обработка выполнена! Оформлен Акт № {act.ActNumber} ({act.Items.Count} гол., списано {act.TotalDosesUsed:0.#} доз серии {act.DrugSeriesSnapshot}). PDF-акт готов к печати.";
            TempData["CreatedActId"] = act.Id.ToString();
        }
        catch (DomainRuleException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> IsolateAnimal(Guid animalId, string reason, string isolatorBox, CancellationToken ct)
    {
        try
        {
            await _herdService.PlaceInQuarantineIsolatorAsync(animalId, reason, isolatorBox, ct);
            TempData["SuccessMessage"] = "Животное помещено в карантинный изолятор. Продукция автоматически заблокирована.";
        }
        catch (DomainRuleException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ActPdf(Guid id, CancellationToken ct)
    {
        var act = await _epizooticService.GetActByIdAsync(id, ct);
        if (act is null)
        {
            return NotFound();
        }

        var pdf = _pdfService.GenerateEpizooticActPdf(act);
        return File(pdf, "application/pdf", $"{act.ActNumber}.pdf");
    }
}
