using JobSystem.Manager.Data.Entities;

namespace JobSystem.Manager.Services.Scaling;

public interface IScalingEngine
{
    Task EvaluateAndScaleAsync(JobDefinition job, int currentQueueDepth, int currentWorkerCount, CancellationToken cancellationToken = default);
}

public interface IScalingStrategy
{
    ScaleMode Mode { get; }
    ScalingDecision CalculateScaling(int queueDepth, int currentWorkers, int minWorkers, int maxWorkers);
}

public record ScalingDecision
{
    public int TargetWorkerCount { get; init; }
    public int WorkersToAdd { get; init; }
    public int WorkersToRemove { get; init; }
    public string Reason { get; init; } = string.Empty;
    public bool ShouldScale => WorkersToAdd > 0 || WorkersToRemove > 0;
}
