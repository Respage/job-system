using JobSystem.Manager.Data.Entities;

namespace JobSystem.Manager.Services.CodeSync;

public interface ICodeSyncService
{
    Task<CodeSyncHistory> SyncJobCodeAsync(JobDefinition job, CancellationToken cancellationToken = default);
    Task<CodeSyncHistory?> GetLatestSyncAsync(Guid jobDefinitionId, CancellationToken cancellationToken = default);
}
