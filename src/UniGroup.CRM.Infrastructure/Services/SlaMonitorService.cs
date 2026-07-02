using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Features.Tickets.Commands.EscalateOverdueTickets;

namespace UniGroup.CRM.Infrastructure.Services;

/// <summary>
/// Background hosted service that runs every 15 minutes to check and escalate overdue tickets.
/// </summary>
public class SlaMonitorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SlaMonitorService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SlaMonitorService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving scoped dependencies.</param>
    /// <param name="logger">The logger instance.</param>
    public SlaMonitorService(IServiceProvider serviceProvider, ILogger<SlaMonitorService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SLA Monitor Service started.");

        // Run once immediately on startup
        await CheckSlaDeadlinesAsync(stoppingToken);

        using (var timer = new PeriodicTimer(TimeSpan.FromMinutes(15)))
        {
            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CheckSlaDeadlinesAsync(stoppingToken);
            }
        }

        _logger.LogInformation("SLA Monitor Service stopped.");
    }

    private async Task CheckSlaDeadlinesAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("SLA Monitor Service checking active tickets for SLA breach...");

            using (var scope = _serviceProvider.CreateScope())
            {
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Send(new EscalateOverdueTicketsCommand(), stoppingToken);
            }

            _logger.LogInformation("SLA Monitor Service successfully completed checking active tickets.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during SLA monitoring run.");
        }
    }
}
