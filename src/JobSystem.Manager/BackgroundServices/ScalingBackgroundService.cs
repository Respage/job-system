using Microsoft.EntityFrameworkCore;
using JobSystem.Manager.Data;
using JobSystem.Manager.Data.Entities;
using JobSystem.Manager.Services.QueueMonitor;
using JobSystem.Manager.Services.Scaling;

namespace JobSystem.Manager.BackgroundServices;

public class ScalingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IScalingEngine _scalingEngine;
    private readonly IQueueMonitorFactory _queueMonitorFactory;
    private readonly ILogger<ScalingBackgroundService> _logger;

    private readonly Dictionary<Guid, DateTime> _lastScaleTime = new();
    private readonly Dictionary<Guid, DateTime> _lastCheckTime = new();

    public ScalingBackgroundService(
        IServiceScopeFactory scopeFactory,
        IScalingEngine scalingEngine,
        IQueueMonitorFactory queueMonitorFactory,
        ILogger<ScalingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _scalingEngine = scalingEngine;
        _queueMonitorFactory = queueMonitorFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scaling Background Service starting");

        // Main loop runs every 5 seconds, but individual jobs have their own polling intervals
        var checkInterval = TimeSpan.FromSeconds(5);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scaling evaluation");
            }

            await Task.Delay(checkInterval, stoppingToken);
        }

        _logger.LogInformation("Scaling Background Service stopping");
    }

    private async Task ProcessJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<JobSystemDbContext>();

        var enabledJobs = await dbContext.JobDefinitions
            .Where(j => j.IsEnabled)
            .Include(j => j.WorkerInstances.Where(w => w.Status == WorkerStatus.Running || w.Status == WorkerStatus.Pending))
            .ToListAsync(cancellationToken);

        foreach (var job in enabledJobs)
        {
            try
            {
                // Check if it's time to poll this job's queue
                if (!ShouldCheckJob(job))
                {
                    continue;
                }

                _lastCheckTime[job.Id] = DateTime.UtcNow;

                // Get queue depth
                var monitor = _queueMonitorFactory.GetMonitor(job.QueueType);
                var queueDepth = await monitor.GetQueueDepthAsync(job, cancellationToken);
                var currentWorkers = job.WorkerInstances.Count;

                _logger.LogDebug(
                    "Job {JobName}: Queue depth = {QueueDepth}, Current workers = {Workers}",
                    job.Name, queueDepth, currentWorkers);

                // Check if we should respect gradual scaling delay
                if (job.ScaleMode == ScaleMode.Gradual && !ShouldScaleGradual(job))
                {
                    continue;
                }

                // Evaluate scaling
                await _scalingEngine.EvaluateAndScaleAsync(job, queueDepth, currentWorkers, cancellationToken);

                _lastScaleTime[job.Id] = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing job {JobName}", job.Name);
            }
        }
    }

    private bool ShouldCheckJob(JobDefinition job)
    {
        if (!_lastCheckTime.TryGetValue(job.Id, out var lastCheck))
        {
            return true;
        }

        var interval = TimeSpan.FromSeconds(job.QueuePollingIntervalSeconds);
        return DateTime.UtcNow - lastCheck >= interval;
    }

    private bool ShouldScaleGradual(JobDefinition job)
    {
        if (!_lastScaleTime.TryGetValue(job.Id, out var lastScale))
        {
            return true;
        }

        // Get gradual scale delay from configuration (default 30 seconds)
        var delaySeconds = 30; // Could be injected from configuration
        return DateTime.UtcNow - lastScale >= TimeSpan.FromSeconds(delaySeconds);
    }
}
