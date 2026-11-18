using Microsoft.AspNetCore.Mvc;
using VetGuard.Application.Interfaces;
using VetGuard.Application.Models;
using VetGuard.Domain.Exceptions;

namespace VetGuard.Web.Controllers;

public sealed class DrugsController : Controller
{
    private readonly ITreatmentAndDrugService _drugService;

    public DrugsController(ITreatmentAndDrugService drugService)
    {
        _drugService = drugService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var drugs = await _drugService.GetDrugsAsync(ct);
        return View(drugs);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateVetDrugCommand command, CancellationToken ct)
    {
        try
        {
            var created = await _drugService.CreateDrugAsync(command, ct);
            TempData["SuccessMessage"] = $"Ветпрепарат «{created.Name}» (серия {created.BatchSeries}, молоко: {created.MilkWithdrawalHours} ч, мясо: {created.MeatWithdrawalDays} сут) добавлен в справочник.";
        }
        catch (DomainRuleException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Replenish(ReplenishDrugStockCommand command, CancellationToken ct)
    {
        try
        {
            var updated = await _drugService.ReplenishStockAsync(command, ct);
            TempData["SuccessMessage"] = $"Остаток препарата «{updated.Name}» пополнен. Текущий запас серии {updated.BatchSeries}: {updated.StockDoses:0.##} {updated.Unit}.";
        }
        catch (DomainRuleException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
