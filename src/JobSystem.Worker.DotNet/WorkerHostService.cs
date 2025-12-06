using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using JobSystem.Worker.Sdk;
using System.Reflection;

namespace JobSystem.Worker.Sdk;

public class WorkerHostService : BackgroundService
{
    private readonly IJobManagerClient _client;
    private readonly ILogger<WorkerHostService> _logger;
    private readonly CancellationTokenSource _heartbeatCts = new();

    public WorkerHostService(IJobManagerClient client, ILogger<WorkerHostService> logger)
    {
        _client = client;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker host service starting");

        try
        {
            // Register with manager
            await _client.RegisterAsync();

            // Start heartbeat
            _ = HeartbeatLoopAsync(_heartbeatCts.Token);

            // Load and run job code
            await RunJobCodeAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Worker host service failed");
            throw;
        }
        finally
        {
            _heartbeatCts.Cancel();
        }
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _client.SendHeartbeatAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send heartbeat");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
        }
    }

    private async Task RunJobCodeAsync(CancellationToken cancellationToken)
    {
        var jobCodePath = "/app/job-code";
        var dllPattern = "*.dll";

        // Find job DLLs (excluding system DLLs)
        var dllFiles = Directory.GetFiles(jobCodePath, dllPattern, SearchOption.TopDirectoryOnly)
            .Where(f => !Path.GetFileName(f).StartsWith("System.")
                && !Path.GetFileName(f).StartsWith("Microsoft.")
                && !Path.GetFileName(f).StartsWith("JobSystem."))
            .ToList();

        if (dllFiles.Count == 0)
        {
            _logger.LogError("No job DLL found in {Path}", jobCodePath);
            return;
        }

        foreach (var dllFile in dllFiles)
        {
            try
            {
                _logger.LogInformation("Loading job assembly: {DllFile}", dllFile);

                var assembly = Assembly.LoadFrom(dllFile);

                // Find types that implement IJobWorker
                var workerTypes = assembly.GetTypes()
                    .Where(t => typeof(IJobWorker).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                    .ToList();

                if (workerTypes.Count > 0)
                {
                    var workerType = workerTypes.First();
                    _logger.LogInformation("Found job worker: {WorkerType}", workerType.Name);

                    var worker = (IJobWorker?)Activator.CreateInstance(workerType);
                    if (worker != null)
                    {
                        await worker.RunAsync(_client, cancellationToken);
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load job assembly: {DllFile}", dllFile);
            }
        }

        // If no IJobWorker found, just keep running (job might use its own processing loop)
        _logger.LogInformation("No IJobWorker implementation found, worker will keep running");
        await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Worker host service stopping");
        _heartbeatCts.Cancel();
        await base.StopAsync(cancellationToken);
    }
}
