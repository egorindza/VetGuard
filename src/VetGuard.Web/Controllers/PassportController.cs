using Microsoft.AspNetCore.Mvc;
using VetGuard.Application.Interfaces;
using VetGuard.Application.Models;
using VetGuard.Domain.Exceptions;

namespace VetGuard.Web.Controllers;

public sealed class PassportController : Controller
{
    private readonly IHerdService _herdService;

    public PassportController(IHerdService herdService)
    {
        _herdService = herdService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string tagNumber, CancellationToken ct)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var details = await _herdService.GetAnimalByTagOrIdAsync(tagNumber, baseUrl, ct);
        if (details is null)
        {
            return NotFound($"QR-паспорт животного с биркой «{tagNumber}» не найден в базе ИрГАУ.");
        }

        return View(details);
    }

    [HttpPost]
    public async Task<IActionResult> Inspect(MobileVetInspectionCommand command, string tagNumber, CancellationToken ct)
    {
        try
        {
            command.InspectorName = User.Identity?.Name ?? "Д-р Соколов А.В., к.в.н.";
            await _herdService.RecordVetInspectionAsync(command, ct);
            TempData["SuccessMessage"] = "Мобильный клинический осмотр у стойла сохранен, статус обновлен (зафиксировано в SQL Temporal Audit).";
        }
        catch (DomainRuleException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToRoute("passport", new { tagNumber });
    }
}
