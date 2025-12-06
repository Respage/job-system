using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using JobSystem.Worker.Sdk;

var managerUrl = Environment.GetEnvironmentVariable("MANAGER_URL");
var apiKey = Environment.GetEnvironmentVariable("API_KEY");
var jobName = Environment.GetEnvironmentVariable("JOB_NAME");
var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

if (string.IsNullOrEmpty(managerUrl) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(jobName))
{
    Console.Error.WriteLine("Missing required environment variables: MANAGER_URL, API_KEY, JOB_NAME");
    Environment.Exit(1);
}

var builder = Host.CreateApplicationBuilder(args);

// Configure services
builder.Services.AddSingleton<IJobManagerClient>(sp =>
    new JobManagerClient(managerUrl, apiKey, jobName, sp.GetRequiredService<ILogger<JobManagerClient>>()));

builder.Services.AddHostedService<WorkerHostService>();

// Configure OpenTelemetry if endpoint is provided
if (!string.IsNullOrEmpty(otlpEndpoint))
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService($"job-worker-{jobName}"))
        .WithTracing(tracing =>
        {
            tracing.AddHttpClientInstrumentation();
            tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        });

    builder.Logging.AddOpenTelemetry(logging =>
    {
        logging.IncludeFormattedMessage = true;
        logging.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
    });
}

var host = builder.Build();
await host.RunAsync();
