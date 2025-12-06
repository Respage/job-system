using Microsoft.EntityFrameworkCore;
using JobSystem.Manager.Data;
using JobSystem.Manager.Data.Entities;
using JobSystem.Manager.Services.Workers;

namespace JobSystem.Manager.Services.Scaling;

public class ScalingEngine : IScalingEngine
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWorkerManager _workerManager;
    private readonly IEnumerable<IScalingStrategy> _strategies;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ScalingEngine> _logger;

    public ScalingEngine(
        IServiceScopeFactory scopeFactory,
        IWorkerManager workerManager,
        IEnumerable<IScalingStrategy> strategies,
        IConfiguration configuration,
        ILogger<ScalingEngine> logger)
    {
        _scopeFactory = scopeFactory;
        _workerManager = workerManager;
        _strategies = strategies;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task EvaluateAndScaleAsync(
        JobDefinition job,
        int currentQueueDepth,
        int currentWorkerCount,
        CancellationToken cancellationToken = default)
    {
        var strategy = _strategies.FirstOrDefault(s => s.Mode == job.ScaleMode);
        if (strategy == null)
        {
            _logger.LogError("No scaling strategy found for mode {ScaleMode}", job.ScaleMode);
            return;
        }

        var decision = strategy.CalculateScaling(
            currentQueueDepth,
            currentWorkerCount,
            job.MinWorkers,
            job.MaxWorkers);

        if (!decision.ShouldScale)
        {
            _logger.LogDebug("Job {JobName}: No scaling needed. Queue depth: {QueueDepth}, Workers: {Workers}",
                job.Name, currentQueueDepth, currentWorkerCount);
            return;
        }

        _logger.LogInformation(
            "Job {JobName}: Scaling decision - {Reason}",
            job.Name, decision.Reason);

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<JobSystemDbContext>();

        // Record scaling event
        dbContext.ScalingEvents.Add(new ScalingEvent
        {
            JobDefinitionId = job.Id,
            EventType = decision.WorkersToAdd > 0 ? ScalingEventType.ScaleUp : ScalingEventType.ScaleDown,
            PreviousWorkerCount = currentWorkerCount,
            NewWorkerCount = decision.TargetWorkerCount,
            QueueDepth = currentQueueDepth,
            Reason = decision.Reason,
            OccurredAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        // Scale up
        if (decision.WorkersToAdd > 0)
        {
            await ScaleUpAsync(job, decision.WorkersToAdd, currentWorkerCount, cancellationToken);
        }

        // Scale down
        if (decision.WorkersToRemove > 0)
        {
            await ScaleDownAsync(job, decision.WorkersToRemove, cancellationToken);
        }
    }

    private async Task ScaleUpAsync(JobDefinition job, int count, int currentWorkerCount, CancellationToken cancellationToken)
    {
        for (var i = 0; i < count; i++)
        {
            try
            {
                // Determine if this worker should be spot or on-demand
                // If UseSpotInstances is true and we're beyond MinWorkers, use spot
                var workerIndex = currentWorkerCount + i;
                var useSpot = job.UseSpotInstances && workerIndex >= job.MinWorkers;

                _logger.LogInformation(
                    "Job {JobName}: Launching worker {Index} (spot: {UseSpot})",
                    job.Name, workerIndex + 1, useSpot);

                await _workerManager.LaunchWorkerAsync(job, useSpot, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to launch worker for job {JobName}", job.Name);
            }
        }
    }

    private async Task ScaleDownAsync(JobDefinition job, int count, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<JobSystemDbContext>();

        // Get workers to terminate - prefer spot instances first, then by oldest launch time
        var workersToTerminate = await dbContext.WorkerInstances
            .Where(w => w.JobDefinitionId == job.Id && w.Status == WorkerStatus.Running)
            .OrderByDescending(w => w.IsSpotInstance) // Spot instances first
            .ThenBy(w => w.LaunchedAt) // Oldest first
            .Take(count)
            .ToListAsync(cancellationToken);

        foreach (var worker in workersToTerminate)
        {
            try
            {
                _logger.LogInformation(
                    "Job {JobName}: Terminating worker {WorkerId} (EC2: {Ec2InstanceId})",
                    job.Name, worker.Id, worker.Ec2InstanceId);

                await _workerManager.TerminateWorkerAsync(worker, cancellationToken);

                worker.Status = WorkerStatus.Terminating;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to terminate worker {WorkerId}", worker.Id);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
