using Amazon.S3;
using Amazon.S3.Transfer;
using LibGit2Sharp;
using Microsoft.EntityFrameworkCore;
using JobSystem.Manager.Data;
using JobSystem.Manager.Data.Entities;

namespace JobSystem.Manager.Services.CodeSync;

public class GitToS3SyncService : ICodeSyncService
{
    private readonly IAmazonS3 _s3Client;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GitToS3SyncService> _logger;

    private readonly string _workDir;

    public GitToS3SyncService(
        IAmazonS3 s3Client,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<GitToS3SyncService> logger)
    {
        _s3Client = s3Client;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;

        _workDir = Path.Combine(Path.GetTempPath(), "job-system-code-sync");
        Directory.CreateDirectory(_workDir);
    }

    public async Task<CodeSyncHistory> SyncJobCodeAsync(JobDefinition job, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<JobSystemDbContext>();

        var syncHistory = new CodeSyncHistory
        {
            JobDefinitionId = job.Id,
            Status = CodeSyncStatus.InProgress,
            StartedAt = DateTime.UtcNow
        };

        dbContext.CodeSyncHistory.Add(syncHistory);
        await dbContext.SaveChangesAsync(cancellationToken);

        var repoPath = Path.Combine(_workDir, job.Id.ToString());

        try
        {
            _logger.LogInformation("Starting code sync for job {JobName} from {GitRepo}", job.Name, job.GitRepoUrl);

            // Clone or pull the repository
            var commitHash = await CloneOrPullRepositoryAsync(job, repoPath, cancellationToken);
            syncHistory.GitCommitHash = commitHash;

            // Upload to S3
            await UploadToS3Async(job, repoPath, cancellationToken);

            syncHistory.Status = CodeSyncStatus.Completed;
            syncHistory.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Code sync completed for job {JobName}. Commit: {CommitHash}",
                job.Name, commitHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Code sync failed for job {JobName}", job.Name);

            syncHistory.Status = CodeSyncStatus.Failed;
            syncHistory.ErrorMessage = ex.Message;
            syncHistory.CompletedAt = DateTime.UtcNow;
        }
        finally
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return syncHistory;
    }

    private async Task<string> CloneOrPullRepositoryAsync(JobDefinition job, string repoPath, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            if (Directory.Exists(repoPath) && Repository.IsValid(repoPath))
            {
                // Pull existing repository
                using var repo = new Repository(repoPath);

                // Fetch
                var remote = repo.Network.Remotes["origin"];
                var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
                Commands.Fetch(repo, remote.Name, refSpecs, new FetchOptions(), null);

                // Checkout the specified branch
                var branch = repo.Branches[$"origin/{job.GitBranch}"];
                if (branch == null)
                {
                    throw new InvalidOperationException($"Branch '{job.GitBranch}' not found");
                }

                Commands.Checkout(repo, branch);

                return branch.Tip.Sha;
            }
            else
            {
                // Clean up if exists but invalid
                if (Directory.Exists(repoPath))
                {
                    Directory.Delete(repoPath, true);
                }

                // Clone repository
                var cloneOptions = new CloneOptions
                {
                    BranchName = job.GitBranch
                };

                Repository.Clone(job.GitRepoUrl, repoPath, cloneOptions);

                using var repo = new Repository(repoPath);
                return repo.Head.Tip.Sha;
            }
        }, cancellationToken);
    }

    private async Task UploadToS3Async(JobDefinition job, string repoPath, CancellationToken cancellationToken)
    {
        var bucket = _configuration["AWS:S3Bucket"]
            ?? throw new InvalidOperationException("S3 bucket not configured");

        var transferUtility = new TransferUtility(_s3Client);

        // Get all files excluding .git directory
        var files = Directory.GetFiles(repoPath, "*", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar)
                && !f.EndsWith(".git"));

        var uploadTasks = new List<Task>();

        foreach (var filePath in files)
        {
            var relativePath = Path.GetRelativePath(repoPath, filePath);
            var s3Key = $"{job.S3CodePath}/{relativePath}".Replace("\\", "/");

            uploadTasks.Add(transferUtility.UploadAsync(new TransferUtilityUploadRequest
            {
                BucketName = bucket,
                Key = s3Key,
                FilePath = filePath
            }, cancellationToken));
        }

        await Task.WhenAll(uploadTasks);

        _logger.LogInformation("Uploaded {Count} files to S3 for job {JobName}", uploadTasks.Count, job.Name);
    }

    public async Task<CodeSyncHistory?> GetLatestSyncAsync(Guid jobDefinitionId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<JobSystemDbContext>();

        return await dbContext.CodeSyncHistory
            .Where(h => h.JobDefinitionId == jobDefinitionId)
            .OrderByDescending(h => h.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
