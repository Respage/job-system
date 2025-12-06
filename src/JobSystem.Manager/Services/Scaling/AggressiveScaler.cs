using JobSystem.Manager.Data.Entities;

namespace JobSystem.Manager.Services.Scaling;

public class AggressiveScaler : IScalingStrategy
{
    private readonly IConfiguration _configuration;

    public ScaleMode Mode => ScaleMode.Aggressive;

    public AggressiveScaler(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public ScalingDecision CalculateScaling(int queueDepth, int currentWorkers, int minWorkers, int maxWorkers)
    {
        var itemsPerWorker = _configuration.GetValue<int>("Scaling:Aggressive:ItemsPerWorker", 10);

        // Calculate desired workers based on queue depth
        var desiredWorkers = queueDepth > 0
            ? Math.Max(minWorkers, (int)Math.Ceiling((double)queueDepth / itemsPerWorker))
            : minWorkers;

        // Clamp to min/max bounds
        var targetWorkers = Math.Clamp(desiredWorkers, minWorkers, maxWorkers);

        var workersToAdd = Math.Max(0, targetWorkers - currentWorkers);
        var workersToRemove = Math.Max(0, currentWorkers - targetWorkers);

        var reason = workersToAdd > 0
            ? $"Queue depth {queueDepth} requires {targetWorkers} workers (aggressive: {itemsPerWorker} items/worker)"
            : workersToRemove > 0
                ? $"Queue depth {queueDepth} only needs {targetWorkers} workers"
                : "No scaling needed";

        return new ScalingDecision
        {
            TargetWorkerCount = targetWorkers,
            WorkersToAdd = workersToAdd,
            WorkersToRemove = workersToRemove,
            Reason = reason
        };
    }
}
