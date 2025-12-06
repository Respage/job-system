using JobSystem.Manager.Data.Entities;

namespace JobSystem.Manager.Services.Workers;

public interface IWorkerManager
{
    Task<WorkerInstance> LaunchWorkerAsync(JobDefinition job, bool useSpot, CancellationToken cancellationToken = default);
    Task TerminateWorkerAsync(WorkerInstance worker, CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkerInstance>> GetActiveWorkersAsync(Guid jobDefinitionId, CancellationToken cancellationToken = default);
    Task UpdateWorkerStatusFromEc2Async(WorkerInstance worker, CancellationToken cancellationToken = default);
}
