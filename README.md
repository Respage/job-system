# Job System Manager

A distributed job system manager that orchestrates workers across AWS EC2 instances (spot and on-demand), supporting multiple language runtimes with configurable auto-scaling based on queue depth.

## Features

- **Multi-Language Support**: Run jobs in Node.js or .NET
- **Queue Sources**: Monitor SQS queues or MongoDB collections
- **Auto-Scaling**: Automatically scale workers based on queue depth
- **Spot Instances**: Use AWS spot instances for cost savings with on-demand fallback
- **Code Sync**: Automatic Git-to-S3 code synchronization
- **Web Dashboard**: Blazor Server UI for job management
- **Observability**: OpenTelemetry integration for traces and logs

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     Job Manager (ASP.NET Core)                  │
│  ┌───────────┐  ┌───────────┐  ┌────────────┐  ┌─────────────┐ │
│  │  Web UI   │  │ Worker    │  │  Scaling   │  │  Code Sync  │ │
│  │ (Blazor)  │  │   API     │  │   Engine   │  │  (Git→S3)   │ │
│  └───────────┘  └───────────┘  └────────────┘  └─────────────┘ │
│                        │                                        │
│                   SQL Server                                    │
└────────────────────────┬────────────────────────────────────────┘
                         │
           ┌─────────────┼─────────────┐
           ▼             ▼             ▼
    ┌────────────┐ ┌────────────┐ ┌────────────┐
    │ EC2 Worker │ │ EC2 Worker │ │ EC2 Worker │
    │   (Node)   │ │   (Node)   │ │   (.NET)   │
    └─────┬──────┘ └─────┬──────┘ └─────┬──────┘
          ▼              ▼              ▼
    ┌──────────┐   ┌──────────┐   ┌──────────┐
    │   SQS    │   │ MongoDB  │   │ MongoDB  │
    └──────────┘   └──────────┘   └──────────┘
```

## Quick Start

### Prerequisites

- .NET 8 SDK
- SQL Server
- AWS credentials configured
- Node.js 20+ (for Node.js workers)

### Setup

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd job-system
   ```

2. **Configure the application**

   Copy and edit the configuration:
   ```bash
   cp src/JobSystem.Manager/appsettings.json src/JobSystem.Manager/appsettings.Development.json
   ```

   Update the connection string and AWS settings in `appsettings.Development.json`.

3. **Create the database**
   ```bash
   dotnet ef database update --project src/JobSystem.Manager
   ```

4. **Run the manager**
   ```bash
   cd src/JobSystem.Manager
   dotnet run
   ```

5. **Access the dashboard**

   Open http://localhost:5000 in your browser.

## Project Structure

```
job-system/
├── src/
│   ├── JobSystem.Manager/           # Main ASP.NET Core application
│   │   ├── Controllers/             # REST API endpoints
│   │   ├── Components/Pages/        # Blazor Server pages
│   │   ├── Services/                # Business logic
│   │   │   ├── Scaling/             # Auto-scaling strategies
│   │   │   ├── QueueMonitor/        # Queue depth monitoring
│   │   │   ├── Workers/             # EC2 worker management
│   │   │   └── CodeSync/            # Git-to-S3 sync
│   │   ├── Data/                    # Entity Framework & entities
│   │   └── BackgroundServices/      # Background processing
│   │
│   ├── JobSystem.Worker.Sdk/        # .NET Worker SDK
│   ├── JobSystem.Worker.Node/       # Node.js worker base image
│   └── JobSystem.Worker.DotNet/     # .NET worker base image
│
├── infrastructure/
│   └── terraform/                   # AWS infrastructure
│
├── tests/
│   └── JobSystem.Manager.Tests/     # Unit tests
│
└── docs/
    ├── API.md                       # API documentation
    ├── Configuration.md             # Configuration guide
    └── WorkerDevelopment.md         # Worker development guide
```

## Configuration

See [docs/Configuration.md](docs/Configuration.md) for detailed configuration options.

Key configuration sections:
- **ConnectionStrings**: SQL Server connection
- **AWS**: Region, S3 bucket, VPC, subnets, security groups
- **Scaling**: Items per worker, idle timeouts
- **OpenTelemetry**: Tracing endpoint

## API Documentation

See [docs/API.md](docs/API.md) for the complete API reference.

### Key Endpoints

| Endpoint | Description |
|----------|-------------|
| `GET /api/jobs` | List all jobs |
| `POST /api/jobs` | Create a new job |
| `POST /api/jobs/{id}/enable` | Enable a job |
| `POST /api/jobs/{id}/code-sync` | Trigger code sync |
| `GET /api/dashboard/stats` | Get dashboard statistics |

## Worker Development

See [docs/WorkerDevelopment.md](docs/WorkerDevelopment.md) for how to create job workers.

### Node.js Worker Example

```javascript
module.exports.run = async function(client) {
    while (true) {
        const item = await getNextItem();
        if (item) {
            await client.reportStatus('Processing', item.id);
            await processItem(item);
            await client.reportComplete(item.id, true);
        } else {
            await sleep(5000);
        }
    }
};
```

### .NET Worker Example

```csharp
public class MyWorker : IJobWorker
{
    public async Task RunAsync(IJobManagerClient client, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var item = await GetNextItemAsync(ct);
            if (item != null)
            {
                await client.ReportStatusAsync("Processing", item.Id);
                await ProcessItemAsync(item, ct);
                await client.ReportCompleteAsync(item.Id, true);
            }
            else
            {
                await Task.Delay(5000, ct);
            }
        }
    }
}
```

## Scaling Modes

### Aggressive
- Scales immediately to match queue depth
- Best for bursty workloads
- Shorter idle timeout (60s default)

### Gradual
- Scales one worker at a time
- Best for steady workloads
- Longer idle timeout (120s default)

## Infrastructure

Deploy AWS infrastructure using Terraform:

```bash
cd infrastructure/terraform
terraform init
terraform plan
terraform apply
```

This creates:
- VPC with public/private subnets
- NAT Gateway for worker internet access
- Security groups
- IAM roles for manager and workers
- S3 bucket for job code
- ECR repositories for worker images

## Development

### Building

```bash
dotnet build
```

### Running Tests

```bash
dotnet test
```

### Building Worker Images

```bash
# Node.js worker
cd src/JobSystem.Worker.Node
docker build -t job-system-worker-node .

# .NET worker
cd src/JobSystem.Worker.DotNet
docker build -t job-system-worker-dotnet .
```

## License

Proprietary - All rights reserved.
