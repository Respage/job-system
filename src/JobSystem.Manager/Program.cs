using Amazon.EC2;
using Amazon.S3;
using Amazon.SQS;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using JobSystem.Manager.Components;
using JobSystem.Manager.Data;
using JobSystem.Manager.BackgroundServices;
using JobSystem.Manager.Services.CodeSync;
using JobSystem.Manager.Services.QueueMonitor;
using JobSystem.Manager.Services.Scaling;
using JobSystem.Manager.Services.Workers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Enable detailed errors for Blazor Server in development
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddServerSideBlazor()
        .AddCircuitOptions(options =>
        {
            options.DetailedErrors = true;
        })
        .AddHubOptions(options =>
        {
            options.EnableDetailedErrors = true;
        });
}

// Add controllers for API
builder.Services.AddControllers();

// Configure Entity Framework with SQL Server
builder.Services.AddDbContext<JobSystemDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("JobSystem")));

// Configure AWS services
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
builder.Services.AddAWSService<IAmazonEC2>();
builder.Services.AddAWSService<IAmazonS3>();
builder.Services.AddAWSService<IAmazonSQS>();

// Register Queue Monitors
builder.Services.AddSingleton<IQueueMonitor, SqsQueueMonitor>();
builder.Services.AddSingleton<IQueueMonitor, MongoQueueMonitor>();
builder.Services.AddSingleton<IQueueMonitorFactory, QueueMonitorFactory>();

// Register Scaling Services
builder.Services.AddSingleton<IScalingStrategy, AggressiveScaler>();
builder.Services.AddSingleton<IScalingStrategy, GradualScaler>();
builder.Services.AddSingleton<IScalingEngine, ScalingEngine>();

// Register Worker Services
builder.Services.AddSingleton<IWorkerManager, Ec2WorkerManager>();
builder.Services.AddSingleton<IWorkerHealthMonitor, WorkerHealthMonitor>();

// Register Code Sync Service
builder.Services.AddSingleton<ICodeSyncService, GitToS3SyncService>();

// Register Background Services
builder.Services.AddHostedService<WorkerHealthCheckService>();
builder.Services.AddHostedService<ScalingBackgroundService>();

// Configure OpenTelemetry
var serviceName = builder.Configuration.GetValue<string>("OpenTelemetry:ServiceName") ?? "job-system-manager";
var otlpEndpoint = builder.Configuration.GetValue<string>("OpenTelemetry:Endpoint");

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();

        if (!string.IsNullOrEmpty(otlpEndpoint))
        {
            tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        }
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();

        if (!string.IsNullOrEmpty(otlpEndpoint))
        {
            metrics.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        }
    });

// Configure logging with OpenTelemetry
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;

    if (!string.IsNullOrEmpty(otlpEndpoint))
    {
        logging.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

// Map API controllers
app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
