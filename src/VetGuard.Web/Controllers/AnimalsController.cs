using Microsoft.AspNetCore.Mvc;
using VetGuard.Application.Interfaces;
using VetGuard.Application.Models;
using VetGuard.Domain.Exceptions;
using VetGuard.Web.Models;

namespace VetGuard.Web.Controllers;

public sealed class AnimalsController : Controller
{
    private readonly IHerdService _herdService;
    private readonly IPdfReportService _pdfService;

    public AnimalsController(IHerdService herdService, IPdfReportService pdfService)
    {
        _herdService = herdService;
        _pdfService = pdfService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] AnimalFilterQuery filter, CancellationToken ct)
    {
        var all = await _herdService.GetAnimalsAsync(new AnimalFilterQuery(), ct);
        var filtered = await _herdService.GetAnimalsAsync(filter, ct);

        var vm = new AnimalsIndexViewModel
        {
            Filter = filter,
            Animals = filtered,
            AvailableBarns = all.Select(a => a.BarnName).Distinct().OrderBy(x => x).ToList(),
            AvailableBreeds = all.Select(a => a.Breed).Distinct().OrderBy(x => x).ToList()
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Table([FromQuery] AnimalFilterQuery filter, CancellationToken ct)
    {
        var filtered = await _herdService.GetAnimalsAsync(filter, ct);
        return PartialView("_AnimalsTable", filtered);
    }

    [HttpGet]
    public async Task<IActionResult> CreateModal(CancellationToken ct)
    {
        var sires = await _herdService.GetBreedingBullsAsync(ct);
        var dams = await _herdService.GetBreedingFemalesAsync(ct);

        return PartialView("_AnimalFormModal", new AnimalEditModalViewModel
        {
            Command = new CreateOrUpdateAnimalCommand
            {
                TagNumber = $"RU-38-IRGAU-{Random.Shared.Next(201, 899)}",
                RfidChip = $"643098100038{Random.Shared.Next(200, 999)}",
                BirthDate = DateTime.UtcNow.Date.AddYears(-2)
            },
            PotentialSires = sires,
            PotentialDams = dams
        });
    }

    [HttpGet]
    public async Task<IActionResult> EditModal(Guid id, CancellationToken ct)
    {
        var details = await _herdService.GetAnimalDetailsAsync(id, GetBaseUrl(), ct);
        if (details is null)
        {
            return NotFound();
        }

        var a = details.Animal;
        var sires = await _herdService.GetBreedingBullsAsync(ct);
        var dams = await _herdService.GetBreedingFemalesAsync(ct);

        return PartialView("_AnimalFormModal", new AnimalEditModalViewModel
        {
            Command = new CreateOrUpdateAnimalCommand
            {
                Id = a.Id,
                TagNumber = a.TagNumber,
                RfidChip = a.RfidChip,
                Name = a.Name,
                Species = a.Species,
                Breed = a.Breed,
                Sex = a.Sex,
                BirthDate = a.BirthDate,
                SireId = a.SireId,
                DamId = a.DamId,
                BarnName = a.BarnName,
                SectionCode = a.SectionCode,
                LineageCode = a.LineageCode,
                CurrentWeightKg = a.CurrentWeightKg,
                AverageDailyMilkKg = a.AverageDailyMilkKg,
                Notes = a.Notes
            },
            PotentialSires = sires.Where(s => s.Id != a.Id).ToList(),
            PotentialDams = dams.Where(d => d.Id != a.Id).ToList()
        });
    }

    [HttpPost]
    public async Task<IActionResult> Save(CreateOrUpdateAnimalCommand command, CancellationToken ct)
    {
        try
        {
            var saved = await _herdService.CreateOrUpdateAnimalAsync(command, ct);
            TempData["SuccessMessage"] = $"Учетная карточка животного {saved.Name} [{saved.TagNumber}] сохранена.";
            return RedirectToAction(nameof(Details), new { id = saved.Id });
        }
        catch (DomainRuleException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return command.Id.HasValue
                ? RedirectToAction(nameof(Details), new { id = command.Id.Value })
                : RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var details = await _herdService.GetAnimalDetailsAsync(id, GetBaseUrl(), ct);
        if (details is null)
        {
            return NotFound();
        }

        return View(details);
    }

    [HttpPost]
    public async Task<IActionResult> LogMilk(LogMilkYieldCommand command, CancellationToken ct)
    {
        try
        {
            command.RecordedBy = User.Identity?.Name ?? "Петров М.И. (Оператор доильного зала)";
            await _herdService.LogMilkYieldAsync(command, ct);
            TempData["SuccessMessage"] = "Контрольный надой сохранен и график удоев обновлен.";
        }
        catch (DomainRuleException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = command.AnimalId });
    }

    [HttpPost]
    public async Task<IActionResult> LogWeight(LogWeightCommand command, CancellationToken ct)
    {
        try
        {
            command.MeasuredBy = User.Identity?.Name ?? "Ильина Е.С. (Зоотехник-селекционер)";
            await _herdService.LogWeightAsync(command, ct);
            TempData["SuccessMessage"] = "Контрольное взвешивание и среднесуточный привес зафиксированы.";
        }
        catch (DomainRuleException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = command.AnimalId });
    }

    [HttpPost]
    public async Task<IActionResult> Isolate(Guid animalId, string reason, string isolatorBox, CancellationToken ct)
    {
        try
        {
            await _herdService.PlaceInQuarantineIsolatorAsync(animalId, reason, isolatorBox, ct);
            TempData["SuccessMessage"] = "Животное переведено в карантинный изолятор. Активирован запрет реализации продукции.";
        }
        catch (DomainRuleException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = animalId });
    }

    [HttpPost]
    public async Task<IActionResult> Release(Guid animalId, string? returnToEpizootics = null, CancellationToken ct = default)
    {
        try
        {
            await _herdService.ReleaseFromQuarantineIsolatorAsync(animalId, ct);
            TempData["SuccessMessage"] = "Карантинный режим снят. Животное возвращено в производственный корпус.";
        }
        catch (DomainRuleException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        if (!string.IsNullOrWhiteSpace(returnToEpizootics))
        {
            return RedirectToAction("Index", "Epizootics");
        }

        return RedirectToAction(nameof(Details), new { id = animalId });
    }

    [HttpGet]
    public async Task<IActionResult> StallCardPdf(Guid id, CancellationToken ct)
    {
        var details = await _herdService.GetAnimalDetailsAsync(id, GetBaseUrl(), ct);
        if (details is null)
        {
            return NotFound();
        }

        var pdf = _pdfService.GenerateStallQrPassportPdf(details);
        return File(pdf, "application/pdf", $"QR_Passport_{details.Animal.TagNumber}.pdf");
    }

    [HttpGet]
    public async Task<IActionResult> VetExtractPdf(Guid id, CancellationToken ct)
    {
        var details = await _herdService.GetAnimalDetailsAsync(id, GetBaseUrl(), ct);
        if (details is null)
        {
            return NotFound();
        }

        var pdf = _pdfService.GenerateVeterinaryExtractPdf(details);
        return File(pdf, "application/pdf", $"Vet_Extract_{details.Animal.TagNumber}.pdf");
    }

    private string GetBaseUrl()
        => $"{Request.Scheme}://{Request.Host}";
}
