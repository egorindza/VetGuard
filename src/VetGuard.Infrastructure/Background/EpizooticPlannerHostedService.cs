using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VetGuard.Application.Interfaces;
using VetGuard.Application.Models;

namespace VetGuard.Infrastructure.Background;

/// <summary>
/// Фоновый планировщик противоэпизоотических мероприятий и автоматического снятия истекших карантинных замков.
/// </summary>
public sealed class EpizooticPlannerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EpizooticPlannerHostedService> _logger;
    private DateTime _lastEpizooticBroadcastUtc = DateTime.MinValue;

    public EpizooticPlannerHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<EpizooticPlannerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Фоновый планировщик VetGuard (контроль карантина и противоэпизоотических мероприятий) запущен.");

        // Небольшая стартовая пауза, чтобы миграции и сидер успели отработать
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var treatmentService = scope.ServiceProvider.GetRequiredService<ITreatmentAndDrugService>();
                var epizooticService = scope.ServiceProvider.GetRequiredService<IEpizooticService>();
                var notifier = scope.ServiceProvider.GetRequiredService<IVetAlertNotifier>();

                var clearedCount = await treatmentService.ProcessExpiredWithdrawalsAsync(stoppingToken);
                if (clearedCount > 0)
                {
                    _logger.LogInformation("Фоновый планировщик снял истекшие карантинные замки у {Count} животных.", clearedCount);
                }

                // Периодическая проверка противоэпизоотического плана (не чаще раза в 10 минут для уведомлений)
                if ((DateTime.UtcNow - _lastEpizooticBroadcastUtc) >= TimeSpan.FromMinutes(10))
                {
                    var plans = await epizooticService.GetEpizooticCalendarPlanAsync(stoppingToken);
                    var overdueCount = plans.Sum(p => p.ActionRequiredAnimals.Count(a => a.State == EpizooticComplianceState.Overdue));
                    var dueSoonCount = plans.Sum(p => p.ActionRequiredAnimals.Count(a => a.State == EpizooticComplianceState.DueSoon));

                    if (overdueCount > 0 || dueSoonCount > 0)
                    {
                        await notifier.NotifyEpizooticReminderAsync(
                            $"Противоэпизоотический контроль: просрочено {overdueCount} голово-обработок, в плане на 14 дней — {dueSoonCount}.",
                            overdueCount,
                            dueSoonCount,
                            stoppingToken);
                    }

                    _lastEpizooticBroadcastUtc = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка фонового цикла мониторинга карантинов и эпизоотического плана.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
