using Microsoft.EntityFrameworkCore;
using JobSystem.Manager.Data;
using JobSystem.Manager.Data.Entities;

namespace JobSystem.Manager.Services.Workers;

public interface IWorkerHealthMonitor
{
    Task CheckWorkerHealthAsync(CancellationToken cancellationToken = default);
    Task ReclaimTimedOutWorkItemsAsync(CancellationToken cancellationToken = default);
}

public class WorkerHealthMonitor : IWorkerHealthMonitor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWorkerManager _workerManager;
    private readonly ILogger<WorkerHealthMonitor> _logger;
    private readonly TimeSpan _heartbeatTimeout = TimeSpan.FromMinutes(2);

    public WorkerHealthMonitor(
        IServiceScopeFactory scopeFactory,
        IWorkerManager workerManager,
        ILogger<WorkerHealthMonitor> logger)
    {
        _scopeFactory = scopeFactory;
        _workerManager = workerManager;
        _logger = logger;
    }

    public async Task CheckWorkerHealthAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<JobSystemDbContext>();

        var cutoffTime = DateTime.UtcNow - _heartbeatTimeout;

        // Find workers that haven't sent a heartbeat recently
        var unhealthyWorkers = await dbContext.WorkerInstances
            .Include(w => w.JobDefinition)
            .Where(w => (w.Status == WorkerStatus.Running || w.Status == WorkerStatus.Pending)
                && w.LastHeartbeatAt < cutoffTime)
            .ToListAsync(cancellationToken);

        foreach (var worker in unhealthyWorkers)
        {
            _logger.LogWarning(
                "Worker {WorkerId} (EC2: {Ec2InstanceId}) for job {JobName} has not sent heartbeat since {LastHeartbeat}. Marking as terminated.",
                worker.Id, worker.Ec2InstanceId, worker.JobDefinition.Name, worker.LastHeartbeatAt);

            try
            {
                // Try to terminate the EC2 instance
                await _workerManager.TerminateWorkerAsync(worker, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to terminate unhealthy worker {WorkerId}", worker.Id);
            }

            worker.Status = WorkerStatus.Terminated;
            worker.TerminatedAt = DateTime.UtcNow;
        }

        if (unhealthyWorkers.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Marked {Count} unhealthy workers as terminated", unhealthyWorkers.Count);
        }
    }

    public async Task ReclaimTimedOutWorkItemsAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<JobSystemDbContext>();

        // Get all jobs with their timeout settings
        var jobs = await dbContext.JobDefinitions
            .Where(j => j.IsEnabled)
            .ToListAsync(cancellationToken);

        foreach (var job in jobs)
        {
            var timeoutCutoff = DateTime.UtcNow - TimeSpan.FromSeconds(job.QueueItemTimeoutSeconds);

            // Find workers that are processing items but haven't reported in longer than the timeout
            var timedOutWorkers = await dbContext.WorkerInstances
                .Where(w => w.JobDefinitionId == job.Id
                    && w.Status == WorkerStatus.Running
                    && w.LastHeartbeatAt < timeoutCutoff)
                .ToListAsync(cancellationToken);

            if (timedOutWorkers.Count > 0)
            {
                _logger.LogWarning(
                    "Job {JobName} has {Count} workers that may have timed out processing items",
                    job.Name, timedOutWorkers.Count);

                // The actual work item reclaim would need to be done in the specific queue
                // (SQS visibility timeout or MongoDB status field update)
                // This is a notification/logging mechanism
            }
        }
    }
}
