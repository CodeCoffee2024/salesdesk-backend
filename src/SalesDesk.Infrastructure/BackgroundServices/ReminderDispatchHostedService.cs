using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SalesDesk.Application.Reminders;

namespace SalesDesk.Infrastructure.BackgroundServices;

/// <summary>
/// Periodically runs the automated reminder engine (TASK-025) by dispatching
/// <see cref="DispatchDueRemindersCommand"/> in its own DI scope — a plain
/// <see cref="BackgroundService"/> rather than Hangfire/Quartz.NET, since a single
/// hourly sweep needs no persistent job store, retry dashboard, or extra
/// infrastructure dependency the task's other listed options would pull in.
/// </summary>
public sealed class ReminderDispatchHostedService(
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment,
    ILogger<ReminderDispatchHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // WebApplicationFactory-based tests boot under "Testing" specifically so a
        // config/DI smoke test doesn't need a live, reachable Postgres (see
        // SalesDeskApiFactory) — this loop would otherwise hit that same
        // unreachable database on every tick.
        if (environment.IsEnvironment("Testing"))
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval);

        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var sentCount = await sender.Send(new DispatchDueRemindersCommand(), stoppingToken);

                if (sentCount > 0)
                {
                    logger.LogInformation("Reminder dispatch sent {Count} reminder(s).", sentCount);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A bad run (e.g. a transient DB blip) must not take the whole host
                // down — an unhandled exception from ExecuteAsync stops the app by
                // default, so this is caught and just retried on the next tick.
                logger.LogError(ex, "Reminder dispatch run failed.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
