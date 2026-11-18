using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using VetGuard.Web.Security;

namespace VetGuard.Web.Controllers;

public sealed class AccountController : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View(DemoRoles.Profiles);
    }

    [HttpPost]
    public async Task<IActionResult> DemoLogin(string roleKey, string? returnUrl = null)
    {
        var profile = DemoRoles.GetByKey(roleKey);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, profile.FullName),
            new(ClaimTypes.Role, profile.Role),
            new("RoleTitle", profile.RoleTitle),
            new("Department", profile.Department),
            new("BadgeIcon", profile.BadgeIcon)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true });

        TempData["SuccessMessage"] = $"Выполнен демо-вход: {profile.BadgeIcon} {profile.RoleTitle} — {profile.FullName} ({profile.Department}).";

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["InfoMessage"] = "Сессия завершена. Вы можете переключить демо-роль в 1 клик.";
        return RedirectToAction("Login");
    }
}
