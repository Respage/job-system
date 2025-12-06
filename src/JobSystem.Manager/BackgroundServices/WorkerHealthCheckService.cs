using JobSystem.Manager.Services.Workers;

namespace JobSystem.Manager.BackgroundServices;

public class WorkerHealthCheckService : BackgroundService
{
    private readonly IWorkerHealthMonitor _healthMonitor;
    private readonly ILogger<WorkerHealthCheckService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);

    public WorkerHealthCheckService(
        IWorkerHealthMonitor healthMonitor,
        ILogger<WorkerHealthCheckService> logger)
    {
        _healthMonitor = healthMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker Health Check Service starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _healthMonitor.CheckWorkerHealthAsync(stoppingToken);
                await _healthMonitor.ReclaimTimedOutWorkItemsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during worker health check");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Worker Health Check Service stopping");
    }
}
