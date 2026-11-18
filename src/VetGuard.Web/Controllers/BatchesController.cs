using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using VetGuard.Application.Interfaces;
using VetGuard.Application.Models;
using VetGuard.Domain.Enums;
using VetGuard.Domain.Exceptions;
using VetGuard.Web.Models;

namespace VetGuard.Web.Controllers;

public sealed class BatchesController : Controller
{
    private readonly IProductBatchService _batchService;
    private readonly IHerdService _herdService;
    private readonly IPdfReportService _pdfService;

    public BatchesController(
        IProductBatchService batchService,
        IHerdService herdService,
        IPdfReportService pdfService)
    {
        _batchService = batchService;
        _herdService = herdService;
        _pdfService = pdfService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        BatchCreationOutcomeDto? outcome = null;
        if (TempData["BatchOutcomeJson"] is string json && !string.IsNullOrWhiteSpace(json))
        {
            outcome = JsonSerializer.Deserialize<BatchCreationOutcomeDto>(json);
        }

        var vm = await BuildIndexViewModelAsync(outcome, ct);
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        string batchNumber,
        ProductBatchType batchType,
        string destination,
        decimal? fatPercent,
        decimal? proteinPercent,
        int? somaticCellsThousands,
        string? notes,
        List<Guid> selectedAnimalIds,
        Dictionary<Guid, decimal>? quantities,
        CancellationToken ct)
    {
        try
        {
            var contributions = new List<BatchContributionInput>();
            foreach (var animalId in selectedAnimalIds.Distinct())
            {
                var qty = quantities != null && quantities.TryGetValue(animalId, out var q) && q > 0
                    ? q
                    : (batchType == ProductBatchType.RawMilk ? 25.0m : 520.0m);

                contributions.Add(new BatchContributionInput
                {
                    AnimalId = animalId,
                    QuantityLitersOrKg = qty
                });
            }

            var command = new CreateProductBatchCommand
            {
                BatchNumber = batchNumber,
                BatchType = batchType,
                Destination = destination,
                FatPercent = fatPercent,
                ProteinPercent = proteinPercent,
                SomaticCellsThousands = somaticCellsThousands,
                OperatorName = User.Identity?.Name ?? "Петров М.И. (Оператор доильного зала)",
                Notes = notes,
                Contributions = contributions
            };

            var outcome = await _batchService.CreateBatchWithQuarantineCheckAsync(command, ct);

            if (Request.Headers.ContainsKey("HX-Request"))
            {
                return PartialView("_BatchOutcomeBanner", outcome);
            }

            TempData["BatchOutcomeJson"] = JsonSerializer.Serialize(outcome);
            return RedirectToAction(nameof(Index));
        }
        catch (DomainRuleException ex)
        {
            var errorOutcome = new BatchCreationOutcomeDto(false, null, batchNumber, ex.Message);
            if (Request.Headers.ContainsKey("HX-Request"))
            {
                return PartialView("_BatchOutcomeBanner", errorOutcome);
            }

            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Демонстрационная кнопка в 1 клик: намеренная попытка оператора включить корову под активным антибиотиком
    /// в товарный танк молока — мгновенно срабатывает «Карантинный замок»!
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> TriggerDemoQuarantineTest(CancellationToken ct)
    {
        var all = await _herdService.GetAnimalsAsync(new AnimalFilterQuery(), ct);
        var lockedCow = all.FirstOrDefault(a => a.Sex == AnimalSex.Female && a.IsMilkQuarantineLocked);
        var healthyCow = all.FirstOrDefault(a => a.Sex == AnimalSex.Female && !a.IsMilkQuarantineLocked);

        if (lockedCow is null)
        {
            TempData["InfoMessage"] = "В данный момент в стаде нет коров с активным карантином по молоку. Назначьте антибиотик в Журнале ЛПМ!";
            return RedirectToAction(nameof(Index));
        }

        var items = new List<BatchContributionInput>();
        if (healthyCow is not null)
        {
            items.Add(new BatchContributionInput { AnimalId = healthyCow.Id, QuantityLitersOrKg = healthyCow.AverageDailyMilkKg ?? 30m });
        }
        items.Add(new BatchContributionInput { AnimalId = lockedCow.Id, QuantityLitersOrKg = lockedCow.AverageDailyMilkKg ?? 26m });

        var outcome = await _batchService.CreateBatchWithQuarantineCheckAsync(new CreateProductBatchCommand
        {
            BatchNumber = $"MILK-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
            BatchType = ProductBatchType.RawMilk,
            Destination = "АО «Янтарь» (Иркутский молочный завод)",
            FatPercent = 3.95m,
            ProteinPercent = 3.30m,
            SomaticCellsThousands = 150,
            OperatorName = User.Identity?.Name ?? "Петров М.И. (Оператор доильного зала)",
            Notes = "Проверка автоматической блокировки слива карантинного молока",
            Contributions = items
        }, ct);

        if (Request.Headers.ContainsKey("HX-Request"))
        {
            return PartialView("_BatchOutcomeBanner", outcome);
        }

        TempData["BatchOutcomeJson"] = JsonSerializer.Serialize(outcome);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Approve(Guid batchId, string? rowVersionBase64, CancellationToken ct)
    {
        try
        {
            var approved = await _batchService.ApproveBatchAsync(new ApproveProductBatchCommand
            {
                BatchId = batchId,
                ExpectedRowVersionBase64 = rowVersionBase64,
                VeterinarianName = User.Identity?.Name ?? "Д-р Соколов А.В., к.в.н."
            }, ct);

            TempData["SuccessMessage"] = $"Партия № {approved.BatchNumber} прошла повторную проверку карантина и официально допущена к реализации!";
        }
        catch (DomainRuleException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Ship(Guid batchId, CancellationToken ct)
    {
        try
        {
            var shipped = await _batchService.MarkShippedAsync(batchId, ct);
            TempData["SuccessMessage"] = $"Партия № {shipped.BatchNumber} отгружена получателю ({shipped.Destination}).";
        }
        catch (DomainRuleException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> CertificatePdf(Guid id, CancellationToken ct)
    {
        var batch = await _batchService.GetBatchByIdAsync(id, ct);
        if (batch is null)
        {
            return NotFound();
        }

        var pdf = _pdfService.GenerateBatchVetCertificatePdf(batch);
        return File(pdf, "application/pdf", $"VetCertificate_{batch.BatchNumber}.pdf");
    }

    private async Task<BatchesIndexViewModel> BuildIndexViewModelAsync(BatchCreationOutcomeDto? outcome, CancellationToken ct)
    {
        var batches = await _batchService.GetBatchesAsync(ct);
        var logs = await _batchService.GetBlockedLogsAsync(25, ct);
        var herd = await _herdService.GetAnimalsAsync(new AnimalFilterQuery(), ct);

        return new BatchesIndexViewModel
        {
            Batches = batches,
            BlockedLogs = logs,
            HerdAnimals = herd,
            LastOutcome = outcome,
            SuggestedBatchNumber = $"MILK-{DateTime.UtcNow.AddHours(8):yyyyMMdd-HHmm}"
        };
    }
}
