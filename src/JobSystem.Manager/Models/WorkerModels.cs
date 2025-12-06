using JobSystem.Manager.Data.Entities;

namespace JobSystem.Manager.Models;

public record WorkerRegistrationRequest
{
    public string JobName { get; init; } = string.Empty;
    public string InstanceId { get; init; } = string.Empty;
}

public record WorkerRegistrationResponse
{
    public Guid WorkerId { get; init; }
    public WorkerConfigResponse Config { get; init; } = null!;
}

public record WorkerConfigResponse
{
    public string JobName { get; init; } = string.Empty;
    public string QueueType { get; init; } = string.Empty;
    public string QueueConnectionString { get; init; } = string.Empty;
    public string QueueName { get; init; } = string.Empty;
    public int QueueItemTimeoutSeconds { get; init; }
}

public record WorkerStatusRequest
{
    public string Status { get; init; } = string.Empty;  // Started, Processing, Completed, Failed
    public string? ItemId { get; init; }
    public int? ProgressPercent { get; init; }
    public string? Message { get; init; }
}

public record WorkerCompleteRequest
{
    public string ItemId { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string? Message { get; init; }
}

public record DashboardStatsResponse
{
    public int TotalJobs { get; init; }
    public int EnabledJobs { get; init; }
    public int TotalActiveWorkers { get; init; }
    public int SpotWorkers { get; init; }
    public int OnDemandWorkers { get; init; }
    public IEnumerable<JobStatsResponse> JobStats { get; init; } = Enumerable.Empty<JobStatsResponse>();
}

public record JobStatsResponse
{
    public Guid JobId { get; init; }
    public string JobName { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public int ActiveWorkers { get; init; }
    public int MinWorkers { get; init; }
    public int MaxWorkers { get; init; }
}
