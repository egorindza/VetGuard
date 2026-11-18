using Microsoft.AspNetCore.Mvc;
using VetGuard.Application.Interfaces;
using VetGuard.Web.Models;

namespace VetGuard.Web.Controllers;

public sealed class AuditController : Controller
{
    private readonly ITemporalAuditService _auditService;
    private readonly IHerdService _herdService;

    public AuditController(ITemporalAuditService auditService, IHerdService herdService)
    {
        _auditService = auditService;
        _herdService = herdService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid? animalId = null, CancellationToken ct = default)
    {
        var treatments = await _auditService.GetTreatmentTemporalHistoryAsync(animalId, ct);
        var animals = await _auditService.GetAnimalStatusTemporalHistoryAsync(animalId, ct);
        var active = await _herdService.GetAllActiveAnimalsForSelectionAsync(ct);

        return View(new TemporalAuditViewModel
        {
            TreatmentHistory = treatments,
            AnimalStatusHistory = animals,
            FilteredAnimalId = animalId,
            ActiveAnimals = active
        });
    }
}
