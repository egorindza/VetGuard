using Microsoft.AspNetCore.Mvc;
using VetGuard.Application.Interfaces;
using VetGuard.Application.Models;
using VetGuard.Domain.Exceptions;
using VetGuard.Web.Models;

namespace VetGuard.Web.Controllers;

public sealed class TreatmentsController : Controller
{
    private readonly ITreatmentAndDrugService _treatmentService;
    private readonly IHerdService _herdService;

    public TreatmentsController(ITreatmentAndDrugService treatmentService, IHerdService herdService)
    {
        _treatmentService = treatmentService;
        _herdService = herdService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid? animalId = null, CancellationToken ct = default)
    {
        var drugs = await _treatmentService.GetDrugsAsync(ct);
        var animals = await _herdService.GetAllActiveAnimalsForSelectionAsync(ct);
        var recent = await _treatmentService.GetRecentTreatmentsAsync(60, ct);

        var firstDrug = drugs.FirstOrDefault();
        var preview = firstDrug is not null
            ? await _treatmentService.PreviewWithdrawalAsync(firstDrug.Id, DateTime.UtcNow, ct)
            : null;

        return View(new TreatmentsIndexViewModel
        {
            RecentTreatments = recent,
            ActiveAnimals = animals,
            AvailableDrugs = drugs,
            InitialDrugPreview = preview,
            PreselectedAnimalId = animalId
        });
    }

    [HttpGet]
    public async Task<IActionResult> WithdrawalPreview(Guid vetDrugId, CancellationToken ct)
    {
        var preview = await _treatmentService.PreviewWithdrawalAsync(vetDrugId, DateTime.UtcNow, ct);
        if (preview is null)
        {
            return Content(string.Empty);
        }

        return PartialView("_WithdrawalPreview", preview);
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterTreatmentCommand command, CancellationToken ct)
    {
        try
        {
            command.VeterinarianName = User.Identity?.Name ?? "Д-р Соколов А.В., к.в.н.";
            command.VeterinarianRole = User.FindFirst("RoleTitle")?.Value ?? "Главный ветврач";

            var rec = await _treatmentService.RegisterTreatmentAsync(command, ct);

            if (rec.MilkWithdrawalHoursApplied > 0 || rec.MeatWithdrawalDaysApplied > 0)
            {
                TempData["SuccessMessage"] =
                    $"Введение препарата «{rec.DrugNameSnapshot}» зарегистрировано в неизменяемом журнале Temporal Audit! " +
                    $"АКТИВИРОВАН КАРАНТИННЫЙ ЗАМОК: молоко заблокировано на {rec.MilkWithdrawalHoursApplied} ч " +
                    $"(до {rec.MilkWithdrawalEndsAtUtc.AddHours(8):dd.MM.yyyy HH:mm} ИКТ), мясо — на {rec.MeatWithdrawalDaysApplied} сут.";
            }
            else
            {
                TempData["SuccessMessage"] =
                    $"Обработка препаратом «{rec.DrugNameSnapshot}» зарегистрирована (срок ожидания по молоку — 0 ч).";
            }
        }
        catch (DomainRuleException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
