using Microsoft.AspNetCore.SignalR;
using VetGuard.Application.Interfaces;

namespace VetGuard.Web.Hubs;

public sealed class VetAlertHub : Hub
{
}

public sealed class SignalRVetAlertNotifier : IVetAlertNotifier
{
    private readonly IHubContext<VetAlertHub> _hubContext;

    public SignalRVetAlertNotifier(IHubContext<VetAlertHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyQuarantineLockActivatedAsync(
        string animalTag,
        string animalName,
        string drugName,
        int milkHours,
        int meatDays,
        DateTime clearanceUtc,
        CancellationToken ct = default)
    {
        return _hubContext.Clients.All.SendAsync(
            "QuarantineLockActivated",
            new
            {
                animalTag,
                animalName,
                drugName,
                milkHours,
                meatDays,
                clearanceText = clearanceUtc.AddHours(8).ToString("dd.MM.yyyy HH:mm"),
                timestamp = DateTime.UtcNow.AddHours(8).ToString("HH:mm:ss")
            },
            ct);
    }

    public Task NotifyQuarantineViolationBlockedAsync(
        string batchNumber,
        string animalTag,
        string animalName,
        string? drugName,
        double remainingHours,
        string attemptedBy,
        CancellationToken ct = default)
    {
        return _hubContext.Clients.All.SendAsync(
            "QuarantineViolationBlocked",
            new
            {
                batchNumber,
                animalTag,
                animalName,
                drugName = drugName ?? "Карантинный изолятор",
                remainingHours = Math.Round(remainingHours, 1),
                attemptedBy,
                timestamp = DateTime.UtcNow.AddHours(8).ToString("HH:mm:ss")
            },
            ct);
    }

    public Task NotifyQuarantineExpiredAsync(
        string animalTag,
        string animalName,
        string drugName,
        CancellationToken ct = default)
    {
        return _hubContext.Clients.All.SendAsync(
            "QuarantineExpired",
            new
            {
                animalTag,
                animalName,
                drugName,
                timestamp = DateTime.UtcNow.AddHours(8).ToString("HH:mm:ss")
            },
            ct);
    }

    public Task NotifyEpizooticReminderAsync(
        string message,
        int overdueCount,
        int dueSoonCount,
        CancellationToken ct = default)
    {
        return _hubContext.Clients.All.SendAsync(
            "EpizooticReminder",
            new
            {
                message,
                overdueCount,
                dueSoonCount,
                timestamp = DateTime.UtcNow.AddHours(8).ToString("HH:mm:ss")
            },
            ct);
    }
}
