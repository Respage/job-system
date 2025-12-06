using JobSystem.Manager.Data.Entities;

namespace JobSystem.Manager.Services.Scaling;

public class GradualScaler : IScalingStrategy
{
    private readonly IConfiguration _configuration;

    public ScaleMode Mode => ScaleMode.Gradual;

    public GradualScaler(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public ScalingDecision CalculateScaling(int queueDepth, int currentWorkers, int minWorkers, int maxWorkers)
    {
        var itemsPerWorker = _configuration.GetValue<int>("Scaling:Gradual:ItemsPerWorker", 25);

        // Calculate desired workers based on queue depth
        var desiredWorkers = queueDepth > 0
            ? Math.Max(minWorkers, (int)Math.Ceiling((double)queueDepth / itemsPerWorker))
            : minWorkers;

        // Clamp to min/max bounds
        var targetWorkers = Math.Clamp(desiredWorkers, minWorkers, maxWorkers);

        // Gradual mode only adds/removes 1 worker at a time
        var workersToAdd = targetWorkers > currentWorkers ? 1 : 0;
        var workersToRemove = targetWorkers < currentWorkers ? 1 : 0;

        var reason = workersToAdd > 0
            ? $"Queue depth {queueDepth} - gradually scaling up (target: {targetWorkers}, current: {currentWorkers})"
            : workersToRemove > 0
                ? $"Queue depth {queueDepth} - gradually scaling down (target: {targetWorkers}, current: {currentWorkers})"
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
