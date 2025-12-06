using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JobSystem.Manager.Data;
using JobSystem.Manager.Data.Entities;
using JobSystem.Manager.Models;

namespace JobSystem.Manager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly JobSystemDbContext _dbContext;

    public DashboardController(JobSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsResponse>> GetStats()
    {
        var jobs = await _dbContext.JobDefinitions
            .Include(j => j.WorkerInstances.Where(w => w.Status == WorkerStatus.Running || w.Status == WorkerStatus.Pending))
            .ToListAsync();

        var activeWorkers = jobs.SelectMany(j => j.WorkerInstances).ToList();

        return Ok(new DashboardStatsResponse
        {
            TotalJobs = jobs.Count,
            EnabledJobs = jobs.Count(j => j.IsEnabled),
            TotalActiveWorkers = activeWorkers.Count,
            SpotWorkers = activeWorkers.Count(w => w.IsSpotInstance),
            OnDemandWorkers = activeWorkers.Count(w => !w.IsSpotInstance),
            JobStats = jobs.Select(j => new JobStatsResponse
            {
                JobId = j.Id,
                JobName = j.Name,
                IsEnabled = j.IsEnabled,
                ActiveWorkers = j.WorkerInstances.Count,
                MinWorkers = j.MinWorkers,
                MaxWorkers = j.MaxWorkers
            })
        });
    }
}
