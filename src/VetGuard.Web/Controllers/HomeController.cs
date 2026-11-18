using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using VetGuard.Application.Interfaces;
using VetGuard.Web.Models;

namespace VetGuard.Web.Controllers;

public class HomeController : Controller
{
    private readonly IHerdService _herdService;

    public HomeController(IHerdService herdService)
    {
        _herdService = herdService;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var summary = await _herdService.GetDashboardSummaryAsync(ct);
        return View(summary);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
