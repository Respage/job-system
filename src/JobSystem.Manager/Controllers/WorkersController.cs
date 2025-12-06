using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JobSystem.Manager.Data;
using JobSystem.Manager.Data.Entities;
using JobSystem.Manager.Models;

namespace JobSystem.Manager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkersController : ControllerBase
{
    private readonly JobSystemDbContext _dbContext;
    private readonly ILogger<WorkersController> _logger;

    public WorkersController(JobSystemDbContext dbContext, ILogger<WorkersController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<WorkerRegistrationResponse>> Register(WorkerRegistrationRequest request)
    {
        var job = await _dbContext.JobDefinitions
            .FirstOrDefaultAsync(j => j.Name == request.JobName);

        if (job == null)
        {
            return NotFound(new { message = $"Job '{request.JobName}' not found" });
        }

        // Find existing worker instance by EC2 instance ID or create new
        var worker = await _dbContext.WorkerInstances
            .FirstOrDefaultAsync(w => w.Ec2InstanceId == request.InstanceId && w.JobDefinitionId == job.Id);

        if (worker == null)
        {
            worker = new WorkerInstance
            {
                Id = Guid.NewGuid(),
                JobDefinitionId = job.Id,
                Ec2InstanceId = request.InstanceId,
                InstanceType = "unknown", // Will be updated by manager
                IsSpotInstance = false,   // Will be updated by manager
                Status = WorkerStatus.Running,
                LaunchedAt = DateTime.UtcNow,
                LastHeartbeatAt = DateTime.UtcNow
            };
            _dbContext.WorkerInstances.Add(worker);
        }
        else
        {
            worker.Status = WorkerStatus.Running;
            worker.LastHeartbeatAt = DateTime.UtcNow;
        }

        // Add status report
        _dbContext.WorkerStatusReports.Add(new WorkerStatusReport
        {
            WorkerInstanceId = worker.Id,
            Status = WorkerReportStatus.Started,
            ReportedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Worker {WorkerId} registered for job {JobName}", worker.Id, job.Name);

        return Ok(new WorkerRegistrationResponse
        {
            WorkerId = worker.Id,
            Config = new WorkerConfigResponse
            {
                JobName = job.Name,
                QueueType = job.QueueType.ToString(),
                QueueConnectionString = job.QueueConnectionString,
                QueueName = job.QueueName,
                QueueItemTimeoutSeconds = job.QueueItemTimeoutSeconds
            }
        });
    }

    [HttpPost("{workerId:guid}/status")]
    public async Task<IActionResult> ReportStatus(Guid workerId, WorkerStatusRequest request, [FromHeader(Name = "X-Api-Key")] string? apiKey)
    {
        var worker = await _dbContext.WorkerInstances
            .Include(w => w.JobDefinition)
            .FirstOrDefaultAsync(w => w.Id == workerId);

        if (worker == null)
        {
            return NotFound(new { message = "Worker not found" });
        }

        // Validate API key
        if (string.IsNullOrEmpty(apiKey) || apiKey != worker.JobDefinition.ApiKey)
        {
            return Unauthorized(new { message = "Invalid API key" });
        }

        // Parse status
        if (!Enum.TryParse<WorkerReportStatus>(request.Status, true, out var status))
        {
            return BadRequest(new { message = $"Invalid status: {request.Status}" });
        }

        worker.LastHeartbeatAt = DateTime.UtcNow;

        _dbContext.WorkerStatusReports.Add(new WorkerStatusReport
        {
            WorkerInstanceId = workerId,
            Status = status,
            ItemId = request.ItemId,
            ProgressPercent = request.ProgressPercent,
            Message = request.Message,
            ReportedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("{workerId:guid}/heartbeat")]
    public async Task<IActionResult> Heartbeat(Guid workerId, [FromHeader(Name = "X-Api-Key")] string? apiKey)
    {
        var worker = await _dbContext.WorkerInstances
            .Include(w => w.JobDefinition)
            .FirstOrDefaultAsync(w => w.Id == workerId);

        if (worker == null)
        {
            return NotFound(new { message = "Worker not found" });
        }

        // Validate API key
        if (string.IsNullOrEmpty(apiKey) || apiKey != worker.JobDefinition.ApiKey)
        {
            return Unauthorized(new { message = "Invalid API key" });
        }

        worker.LastHeartbeatAt = DateTime.UtcNow;

        _dbContext.WorkerStatusReports.Add(new WorkerStatusReport
        {
            WorkerInstanceId = workerId,
            Status = WorkerReportStatus.Heartbeat,
            ReportedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("{workerId:guid}/complete")]
    public async Task<IActionResult> Complete(Guid workerId, WorkerCompleteRequest request, [FromHeader(Name = "X-Api-Key")] string? apiKey)
    {
        var worker = await _dbContext.WorkerInstances
            .Include(w => w.JobDefinition)
            .FirstOrDefaultAsync(w => w.Id == workerId);

        if (worker == null)
        {
            return NotFound(new { message = "Worker not found" });
        }

        // Validate API key
        if (string.IsNullOrEmpty(apiKey) || apiKey != worker.JobDefinition.ApiKey)
        {
            return Unauthorized(new { message = "Invalid API key" });
        }

        worker.LastHeartbeatAt = DateTime.UtcNow;

        _dbContext.WorkerStatusReports.Add(new WorkerStatusReport
        {
            WorkerInstanceId = workerId,
            Status = request.Success ? WorkerReportStatus.Completed : WorkerReportStatus.Failed,
            ItemId = request.ItemId,
            Message = request.Message,
            ReportedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Worker {WorkerId} completed item {ItemId} with success={Success}",
            workerId, request.ItemId, request.Success);

        return Ok();
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<WorkerInstanceResponse>>> GetAllWorkers([FromQuery] WorkerStatus? status = null)
    {
        var query = _dbContext.WorkerInstances
            .Include(w => w.JobDefinition)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(w => w.Status == status.Value);
        }

        var workers = await query
            .OrderByDescending(w => w.LaunchedAt)
            .Take(100)
            .ToListAsync();

        return Ok(workers.Select(w => new WorkerInstanceDetailResponse
        {
            Id = w.Id,
            JobName = w.JobDefinition.Name,
            Ec2InstanceId = w.Ec2InstanceId,
            PrivateIpAddress = w.PrivateIpAddress,
            InstanceType = w.InstanceType,
            IsSpotInstance = w.IsSpotInstance,
            Status = w.Status.ToString(),
            LaunchedAt = w.LaunchedAt,
            LastHeartbeatAt = w.LastHeartbeatAt,
            TerminatedAt = w.TerminatedAt
        }));
    }

    [HttpGet("{workerId:guid}")]
    public async Task<ActionResult<WorkerInstanceDetailResponse>> GetWorker(Guid workerId)
    {
        var worker = await _dbContext.WorkerInstances
            .Include(w => w.JobDefinition)
            .FirstOrDefaultAsync(w => w.Id == workerId);

        if (worker == null)
        {
            return NotFound();
        }

        return Ok(new WorkerInstanceDetailResponse
        {
            Id = worker.Id,
            JobName = worker.JobDefinition.Name,
            Ec2InstanceId = worker.Ec2InstanceId,
            PrivateIpAddress = worker.PrivateIpAddress,
            InstanceType = worker.InstanceType,
            IsSpotInstance = worker.IsSpotInstance,
            Status = worker.Status.ToString(),
            LaunchedAt = worker.LaunchedAt,
            LastHeartbeatAt = worker.LastHeartbeatAt,
            TerminatedAt = worker.TerminatedAt
        });
    }

    [HttpGet("{workerId:guid}/logs")]
    public async Task<ActionResult<IEnumerable<WorkerStatusReportResponse>>> GetWorkerLogs(Guid workerId, [FromQuery] int limit = 100)
    {
        var worker = await _dbContext.WorkerInstances.FindAsync(workerId);

        if (worker == null)
        {
            return NotFound();
        }

        var reports = await _dbContext.WorkerStatusReports
            .Where(r => r.WorkerInstanceId == workerId)
            .OrderByDescending(r => r.ReportedAt)
            .Take(limit)
            .ToListAsync();

        return Ok(reports.Select(r => new WorkerStatusReportResponse
        {
            Id = r.Id,
            Status = r.Status.ToString(),
            ItemId = r.ItemId,
            ProgressPercent = r.ProgressPercent,
            Message = r.Message,
            ReportedAt = r.ReportedAt
        }));
    }
}

public record WorkerInstanceDetailResponse
{
    public Guid Id { get; init; }
    public string JobName { get; init; } = string.Empty;
    public string Ec2InstanceId { get; init; } = string.Empty;
    public string? PrivateIpAddress { get; init; }
    public string InstanceType { get; init; } = string.Empty;
    public bool IsSpotInstance { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime LaunchedAt { get; init; }
    public DateTime? LastHeartbeatAt { get; init; }
    public DateTime? TerminatedAt { get; init; }
}

public record WorkerStatusReportResponse
{
    public long Id { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? ItemId { get; init; }
    public int? ProgressPercent { get; init; }
    public string? Message { get; init; }
    public DateTime ReportedAt { get; init; }
}
