using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JobSystem.Manager.Data;
using JobSystem.Manager.Data.Entities;
using JobSystem.Manager.Services.CodeSync;

namespace JobSystem.Manager.Controllers;

[ApiController]
[Route("api/jobs/{jobId:guid}/code-sync")]
public class CodeSyncController : ControllerBase
{
    private readonly JobSystemDbContext _dbContext;
    private readonly ICodeSyncService _codeSyncService;
    private readonly ILogger<CodeSyncController> _logger;

    public CodeSyncController(
        JobSystemDbContext dbContext,
        ICodeSyncService codeSyncService,
        ILogger<CodeSyncController> logger)
    {
        _dbContext = dbContext;
        _codeSyncService = codeSyncService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<CodeSyncResponse>> TriggerSync(Guid jobId)
    {
        var job = await _dbContext.JobDefinitions.FindAsync(jobId);
        if (job == null)
        {
            return NotFound(new { message = "Job not found" });
        }

        // Check if there's already a sync in progress
        var inProgress = await _dbContext.CodeSyncHistory
            .AnyAsync(h => h.JobDefinitionId == jobId && h.Status == CodeSyncStatus.InProgress);

        if (inProgress)
        {
            return Conflict(new { message = "A code sync is already in progress for this job" });
        }

        _logger.LogInformation("Triggering code sync for job {JobName}", job.Name);

        // Start sync in background
        _ = Task.Run(async () =>
        {
            try
            {
                await _codeSyncService.SyncJobCodeAsync(job);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background code sync failed for job {JobName}", job.Name);
            }
        });

        return Accepted(new CodeSyncResponse
        {
            Message = "Code sync started",
            JobId = jobId,
            JobName = job.Name
        });
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CodeSyncHistoryResponse>>> GetSyncHistory(Guid jobId, [FromQuery] int limit = 10)
    {
        var job = await _dbContext.JobDefinitions.FindAsync(jobId);
        if (job == null)
        {
            return NotFound(new { message = "Job not found" });
        }

        var history = await _dbContext.CodeSyncHistory
            .Where(h => h.JobDefinitionId == jobId)
            .OrderByDescending(h => h.StartedAt)
            .Take(limit)
            .ToListAsync();

        return Ok(history.Select(h => new CodeSyncHistoryResponse
        {
            Id = h.Id,
            GitCommitHash = h.GitCommitHash,
            Status = h.Status.ToString(),
            ErrorMessage = h.ErrorMessage,
            StartedAt = h.StartedAt,
            CompletedAt = h.CompletedAt
        }));
    }

    [HttpGet("latest")]
    public async Task<ActionResult<CodeSyncHistoryResponse>> GetLatestSync(Guid jobId)
    {
        var job = await _dbContext.JobDefinitions.FindAsync(jobId);
        if (job == null)
        {
            return NotFound(new { message = "Job not found" });
        }

        var latest = await _codeSyncService.GetLatestSyncAsync(jobId);
        if (latest == null)
        {
            return NotFound(new { message = "No sync history found" });
        }

        return Ok(new CodeSyncHistoryResponse
        {
            Id = latest.Id,
            GitCommitHash = latest.GitCommitHash,
            Status = latest.Status.ToString(),
            ErrorMessage = latest.ErrorMessage,
            StartedAt = latest.StartedAt,
            CompletedAt = latest.CompletedAt
        });
    }
}

public record CodeSyncResponse
{
    public string Message { get; init; } = string.Empty;
    public Guid JobId { get; init; }
    public string JobName { get; init; } = string.Empty;
}

public record CodeSyncHistoryResponse
{
    public long Id { get; init; }
    public string? GitCommitHash { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}
