namespace JobSystem.Worker.Sdk;

public interface IJobManagerClient
{
    Guid? WorkerId { get; }
    WorkerConfig? Config { get; }

    Task<WorkerRegistrationResponse> RegisterAsync(string? instanceId = null, CancellationToken cancellationToken = default);
    Task ReportStatusAsync(string status, string? itemId = null, int? progressPercent = null, string? message = null, CancellationToken cancellationToken = default);
    Task SendHeartbeatAsync(CancellationToken cancellationToken = default);
    Task ReportCompleteAsync(string itemId, bool success, string? message = null, CancellationToken cancellationToken = default);
}

public record WorkerRegistrationResponse
{
    public Guid WorkerId { get; init; }
    public WorkerConfig Config { get; init; } = null!;
}

public record WorkerConfig
{
    public string JobName { get; init; } = string.Empty;
    public string QueueType { get; init; } = string.Empty;
    public string QueueConnectionString { get; init; } = string.Empty;
    public string QueueName { get; init; } = string.Empty;
    public int QueueItemTimeoutSeconds { get; init; }
}
