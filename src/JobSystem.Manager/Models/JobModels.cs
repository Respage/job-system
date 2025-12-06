using JobSystem.Manager.Data.Entities;

namespace JobSystem.Manager.Models;

public record CreateJobRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public JobLanguage Language { get; init; }
    public QueueType QueueType { get; init; }
    public string QueueConnectionString { get; init; } = string.Empty;
    public string QueueName { get; init; } = string.Empty;
    public InstanceSize InstanceSize { get; init; }
    public bool UseSpotInstances { get; init; } = true;
    public ScaleMode ScaleMode { get; init; }
    public int MinWorkers { get; init; }
    public int MaxWorkers { get; init; } = 10;
    public int QueueItemTimeoutSeconds { get; init; } = 300;
    public int QueuePollingIntervalSeconds { get; init; } = 30;
    public string GitRepoUrl { get; init; } = string.Empty;
    public string GitBranch { get; init; } = "main";
    public string S3CodePath { get; init; } = string.Empty;
}

public record UpdateJobRequest
{
    public string? Description { get; init; }
    public InstanceSize? InstanceSize { get; init; }
    public bool? UseSpotInstances { get; init; }
    public ScaleMode? ScaleMode { get; init; }
    public int? MinWorkers { get; init; }
    public int? MaxWorkers { get; init; }
    public int? QueueItemTimeoutSeconds { get; init; }
    public int? QueuePollingIntervalSeconds { get; init; }
    public string? GitBranch { get; init; }
}

public record JobResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Language { get; init; } = string.Empty;
    public string QueueType { get; init; } = string.Empty;
    public string QueueName { get; init; } = string.Empty;
    public string InstanceSize { get; init; } = string.Empty;
    public bool UseSpotInstances { get; init; }
    public string ScaleMode { get; init; } = string.Empty;
    public int MinWorkers { get; init; }
    public int MaxWorkers { get; init; }
    public int QueueItemTimeoutSeconds { get; init; }
    public int QueuePollingIntervalSeconds { get; init; }
    public string GitRepoUrl { get; init; } = string.Empty;
    public string GitBranch { get; init; } = string.Empty;
    public string S3CodePath { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public int ActiveWorkerCount { get; init; }

    public static JobResponse FromEntity(JobDefinition entity, int activeWorkerCount = 0) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Description = entity.Description,
        Language = entity.Language.ToString(),
        QueueType = entity.QueueType.ToString(),
        QueueName = entity.QueueName,
        InstanceSize = entity.InstanceSize.ToString(),
        UseSpotInstances = entity.UseSpotInstances,
        ScaleMode = entity.ScaleMode.ToString(),
        MinWorkers = entity.MinWorkers,
        MaxWorkers = entity.MaxWorkers,
        QueueItemTimeoutSeconds = entity.QueueItemTimeoutSeconds,
        QueuePollingIntervalSeconds = entity.QueuePollingIntervalSeconds,
        GitRepoUrl = entity.GitRepoUrl,
        GitBranch = entity.GitBranch,
        S3CodePath = entity.S3CodePath,
        IsEnabled = entity.IsEnabled,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        ActiveWorkerCount = activeWorkerCount
    };
}

public record ManualScaleRequest
{
    public int TargetWorkerCount { get; init; }
}
