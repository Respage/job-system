using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JobSystem.Manager.Data;
using JobSystem.Manager.Data.Entities;
using JobSystem.Manager.Models;
using System.Security.Cryptography;

namespace JobSystem.Manager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly JobSystemDbContext _dbContext;
    private readonly ILogger<JobsController> _logger;

    public JobsController(JobSystemDbContext dbContext, ILogger<JobsController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<JobResponse>>> GetJobs()
    {
        var jobs = await _dbContext.JobDefinitions
            .Include(j => j.WorkerInstances.Where(w => w.Status == WorkerStatus.Running || w.Status == WorkerStatus.Pending))
            .OrderBy(j => j.Name)
            .ToListAsync();

        return Ok(jobs.Select(j => JobResponse.FromEntity(j, j.WorkerInstances.Count)));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<JobResponse>> GetJob(Guid id)
    {
        var job = await _dbContext.JobDefinitions
            .Include(j => j.WorkerInstances.Where(w => w.Status == WorkerStatus.Running || w.Status == WorkerStatus.Pending))
            .FirstOrDefaultAsync(j => j.Id == id);

        if (job == null)
        {
            return NotFound();
        }

        return Ok(JobResponse.FromEntity(job, job.WorkerInstances.Count));
    }

    [HttpPost]
    public async Task<ActionResult<JobResponse>> CreateJob(CreateJobRequest request)
    {
        // Check for duplicate name
        if (await _dbContext.JobDefinitions.AnyAsync(j => j.Name == request.Name))
        {
            return Conflict(new { message = $"Job with name '{request.Name}' already exists" });
        }

        var job = new JobDefinition
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Language = request.Language,
            QueueType = request.QueueType,
            QueueConnectionString = request.QueueConnectionString,
            QueueName = request.QueueName,
            InstanceSize = request.InstanceSize,
            UseSpotInstances = request.UseSpotInstances,
            ScaleMode = request.ScaleMode,
            MinWorkers = request.MinWorkers,
            MaxWorkers = request.MaxWorkers,
            QueueItemTimeoutSeconds = request.QueueItemTimeoutSeconds,
            QueuePollingIntervalSeconds = request.QueuePollingIntervalSeconds,
            GitRepoUrl = request.GitRepoUrl,
            GitBranch = request.GitBranch,
            S3CodePath = request.S3CodePath,
            ApiKey = GenerateApiKey(),
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.JobDefinitions.Add(job);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Created job {JobName} with ID {JobId}", job.Name, job.Id);

        return CreatedAtAction(nameof(GetJob), new { id = job.Id }, JobResponse.FromEntity(job));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<JobResponse>> UpdateJob(Guid id, UpdateJobRequest request)
    {
        var job = await _dbContext.JobDefinitions
            .Include(j => j.WorkerInstances.Where(w => w.Status == WorkerStatus.Running || w.Status == WorkerStatus.Pending))
            .FirstOrDefaultAsync(j => j.Id == id);

        if (job == null)
        {
            return NotFound();
        }

        // Don't allow updates if job has active workers
        if (job.WorkerInstances.Any())
        {
            return BadRequest(new { message = "Cannot update job while workers are active. Disable the job first." });
        }

        // Apply updates
        if (request.Description != null) job.Description = request.Description;
        if (request.InstanceSize.HasValue) job.InstanceSize = request.InstanceSize.Value;
        if (request.UseSpotInstances.HasValue) job.UseSpotInstances = request.UseSpotInstances.Value;
        if (request.ScaleMode.HasValue) job.ScaleMode = request.ScaleMode.Value;
        if (request.MinWorkers.HasValue) job.MinWorkers = request.MinWorkers.Value;
        if (request.MaxWorkers.HasValue) job.MaxWorkers = request.MaxWorkers.Value;
        if (request.QueueItemTimeoutSeconds.HasValue) job.QueueItemTimeoutSeconds = request.QueueItemTimeoutSeconds.Value;
        if (request.QueuePollingIntervalSeconds.HasValue) job.QueuePollingIntervalSeconds = request.QueuePollingIntervalSeconds.Value;
        if (request.GitBranch != null) job.GitBranch = request.GitBranch;

        job.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Updated job {JobName} with ID {JobId}", job.Name, job.Id);

        return Ok(JobResponse.FromEntity(job));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteJob(Guid id)
    {
        var job = await _dbContext.JobDefinitions
            .Include(j => j.WorkerInstances.Where(w => w.Status == WorkerStatus.Running || w.Status == WorkerStatus.Pending))
            .FirstOrDefaultAsync(j => j.Id == id);

        if (job == null)
        {
            return NotFound();
        }

        // Don't allow deletion if job has active workers
        if (job.WorkerInstances.Any())
        {
            return BadRequest(new { message = "Cannot delete job while workers are active. Disable the job first." });
        }

        _dbContext.JobDefinitions.Remove(job);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Deleted job {JobName} with ID {JobId}", job.Name, job.Id);

        return NoContent();
    }

    [HttpPost("{id:guid}/enable")]
    public async Task<ActionResult<JobResponse>> EnableJob(Guid id)
    {
        var job = await _dbContext.JobDefinitions.FindAsync(id);

        if (job == null)
        {
            return NotFound();
        }

        job.IsEnabled = true;
        job.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Enabled job {JobName} with ID {JobId}", job.Name, job.Id);

        return Ok(JobResponse.FromEntity(job));
    }

    [HttpPost("{id:guid}/disable")]
    public async Task<ActionResult<JobResponse>> DisableJob(Guid id)
    {
        var job = await _dbContext.JobDefinitions.FindAsync(id);

        if (job == null)
        {
            return NotFound();
        }

        job.IsEnabled = false;
        job.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Disabled job {JobName} with ID {JobId}", job.Name, job.Id);

        return Ok(JobResponse.FromEntity(job));
    }

    [HttpGet("{id:guid}/workers")]
    public async Task<ActionResult<IEnumerable<WorkerInstanceResponse>>> GetJobWorkers(Guid id)
    {
        var job = await _dbContext.JobDefinitions.FindAsync(id);

        if (job == null)
        {
            return NotFound();
        }

        var workers = await _dbContext.WorkerInstances
            .Where(w => w.JobDefinitionId == id)
            .OrderByDescending(w => w.LaunchedAt)
            .Take(100)
            .ToListAsync();

        return Ok(workers.Select(WorkerInstanceResponse.FromEntity));
    }

    [HttpPost("{id:guid}/regenerate-api-key")]
    public async Task<ActionResult<object>> RegenerateApiKey(Guid id)
    {
        var job = await _dbContext.JobDefinitions.FindAsync(id);

        if (job == null)
        {
            return NotFound();
        }

        job.ApiKey = GenerateApiKey();
        job.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Regenerated API key for job {JobName} with ID {JobId}", job.Name, job.Id);

        return Ok(new { apiKey = job.ApiKey });
    }

    private static string GenerateApiKey()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}

public record WorkerInstanceResponse
{
    public Guid Id { get; init; }
    public string Ec2InstanceId { get; init; } = string.Empty;
    public string? PrivateIpAddress { get; init; }
    public string InstanceType { get; init; } = string.Empty;
    public bool IsSpotInstance { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime LaunchedAt { get; init; }
    public DateTime? LastHeartbeatAt { get; init; }
    public DateTime? TerminatedAt { get; init; }

    public static WorkerInstanceResponse FromEntity(WorkerInstance entity) => new()
    {
        Id = entity.Id,
        Ec2InstanceId = entity.Ec2InstanceId,
        PrivateIpAddress = entity.PrivateIpAddress,
        InstanceType = entity.InstanceType,
        IsSpotInstance = entity.IsSpotInstance,
        Status = entity.Status.ToString(),
        LaunchedAt = entity.LaunchedAt,
        LastHeartbeatAt = entity.LastHeartbeatAt,
        TerminatedAt = entity.TerminatedAt
    };
}
