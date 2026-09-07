using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SalesDesk.Application.Recurring;

namespace SalesDesk.Infrastructure.BackgroundServices;

/// <summary>
/// Periodically runs the recurring-retainer engine (VERSION-2 roadmap item 3) by
/// dispatching <see cref="GenerateDueRecurringDocumentsCommand"/> in its own DI
/// scope — same shape as <see cref="ReminderDispatchHostedService"/>. An hourly
/// sweep is more than frequent enough for a schedule keyed to a calendar date
/// (Weekly/Monthly/Quarterly/Yearly), and it's idempotent: a schedule's
/// NextRunDate advances immediately after it generates, so a same-day re-check
/// never regenerates it.
/// </summary>
public sealed class RecurringDocumentHostedService(
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment,
    ILogger<RecurringDocumentHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Same reasoning as ReminderDispatchHostedService: WebApplicationFactory
        // tests boot under "Testing" specifically so a config/DI smoke test doesn't
        // need a live, reachable Postgres.
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
                var generatedCount = await sender.Send(new GenerateDueRecurringDocumentsCommand(), stoppingToken);

                if (generatedCount > 0)
                {
                    logger.LogInformation("Recurring schedule sweep generated {Count} document(s).", generatedCount);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A bad run (e.g. a transient DB blip) must not take the whole host
                // down — retried on the next tick.
                logger.LogError(ex, "Recurring schedule sweep failed.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
