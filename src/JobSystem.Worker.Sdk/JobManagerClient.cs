using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace JobSystem.Worker.Sdk;

public class JobManagerClient : IJobManagerClient
{
    private readonly HttpClient _httpClient;
    private readonly string _jobName;
    private readonly ILogger<JobManagerClient> _logger;

    public Guid? WorkerId { get; private set; }
    public WorkerConfig? Config { get; private set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public JobManagerClient(string managerUrl, string apiKey, string jobName, ILogger<JobManagerClient> logger)
    {
        _jobName = jobName;
        _logger = logger;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(managerUrl.TrimEnd('/')),
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    }

    public async Task<WorkerRegistrationResponse> RegisterAsync(string? instanceId = null, CancellationToken cancellationToken = default)
    {
        var request = new
        {
            jobName = _jobName,
            instanceId = instanceId ?? GetInstanceId()
        };

        var response = await _httpClient.PostAsJsonAsync("/api/workers/register", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<WorkerRegistrationResponse>(JsonOptions, cancellationToken);
        if (result == null)
        {
            throw new InvalidOperationException("Failed to parse registration response");
        }

        WorkerId = result.WorkerId;
        Config = result.Config;

        _logger.LogInformation("Registered with manager. Worker ID: {WorkerId}", WorkerId);

        return result;
    }

    public async Task ReportStatusAsync(string status, string? itemId = null, int? progressPercent = null, string? message = null, CancellationToken cancellationToken = default)
    {
        EnsureRegistered();

        var request = new
        {
            status,
            itemId,
            progressPercent,
            message
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/workers/{WorkerId}/status", request, JsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to report status");
        }
    }

    public async Task SendHeartbeatAsync(CancellationToken cancellationToken = default)
    {
        if (WorkerId == null)
        {
            return;
        }

        try
        {
            var response = await _httpClient.PostAsync($"/api/workers/{WorkerId}/heartbeat", null, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send heartbeat");
        }
    }

    public async Task ReportCompleteAsync(string itemId, bool success, string? message = null, CancellationToken cancellationToken = default)
    {
        EnsureRegistered();

        var request = new
        {
            itemId,
            success,
            message
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/workers/{WorkerId}/complete", request, JsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to report completion");
        }
    }

    private void EnsureRegistered()
    {
        if (WorkerId == null)
        {
            throw new InvalidOperationException("Worker not registered. Call RegisterAsync first.");
        }
    }

    private static string GetInstanceId()
    {
        return Environment.GetEnvironmentVariable("EC2_INSTANCE_ID")
            ?? Environment.GetEnvironmentVariable("HOSTNAME")
            ?? $"worker-{DateTime.UtcNow.Ticks}";
    }
}
