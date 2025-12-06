namespace JobSystem.Worker.Sdk;

/// <summary>
/// Interface for job worker implementations.
/// Implement this interface in your job code to integrate with the Job System.
/// </summary>
public interface IJobWorker
{
    /// <summary>
    /// Main entry point for the job worker.
    /// This method should contain the main processing loop.
    /// </summary>
    /// <param name="client">The job manager client for reporting status</param>
    /// <param name="cancellationToken">Token to signal shutdown</param>
    Task RunAsync(IJobManagerClient client, CancellationToken cancellationToken);
}
