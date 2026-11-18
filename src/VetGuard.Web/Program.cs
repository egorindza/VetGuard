using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using VetGuard.Application.Interfaces;
using VetGuard.Application.Validators;
using VetGuard.Infrastructure;
using VetGuard.Infrastructure.Persistence;
using VetGuard.Web.Hubs;

var builder = WebApplication.CreateBuilder(args);

if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS"))
    && !args.Any(a => a.StartsWith("--urls", StringComparison.OrdinalIgnoreCase)))
{
    builder.WebHost.UseUrls("http://localhost:5104");
}

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "VetGuard.DemoAuth";
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });

builder.Services.AddAuthorization();

builder.Services.AddVetGuardApplication();
builder.Services.AddVetGuardInfrastructure(builder.Configuration, enableBackgroundPlanner: true);
builder.Services.AddSingleton<IVetAlertNotifier, SignalRVetAlertNotifier>();

var app = builder.Build();

var ruCulture = new CultureInfo("ru-RU");
ruCulture.NumberFormat.NumberDecimalSeparator = ".";
var supportedCultures = new[] { ruCulture, CultureInfo.InvariantCulture };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(ruCulture),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

// Автоматическое применение миграций и наполнение демонстрационными данными ИрГАУ при старте
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<VetGuardDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("VetGuardStartup");
    await VetGuardDbSeeder.SeedAsync(db, logger);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapHub<VetAlertHub>("/hubs/vet-alerts");

app.MapControllerRoute(
    name: "passport",
    pattern: "Passport/{tagNumber}",
    defaults: new { controller = "Passport", action = "Index" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

public partial class Program { }
