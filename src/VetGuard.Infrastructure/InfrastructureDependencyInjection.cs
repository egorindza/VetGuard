using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VetGuard.Application.Interfaces;
using VetGuard.Infrastructure.Background;
using VetGuard.Infrastructure.Persistence;
using VetGuard.Infrastructure.Services;

namespace VetGuard.Infrastructure;

public static class InfrastructureDependencyInjection
{
    public static IServiceCollection AddVetGuardInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool enableBackgroundPlanner = true)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<VetGuardDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure(3);
            }));

        services.AddSingleton<IQrCodeService, QrCodeService>();
        services.AddSingleton<IPdfReportService, PdfReportService>();

        services.AddScoped<IHerdService, HerdService>();
        services.AddScoped<ITreatmentAndDrugService, TreatmentAndDrugService>();
        services.AddScoped<IProductBatchService, ProductBatchService>();
        services.AddScoped<IPedigreeService, DapperPedigreeService>();
        services.AddScoped<IEpizooticService, EpizooticService>();
        services.AddScoped<ITemporalAuditService, TemporalAuditService>();

        if (enableBackgroundPlanner)
        {
            services.AddHostedService<EpizooticPlannerHostedService>();
        }

        return services;
    }
}
