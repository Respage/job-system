# Job System Manager Configuration Guide

## Overview

The Job System Manager is configured via `appsettings.json` or environment variables.

## Configuration File

```json
{
  "ConnectionStrings": {
    "JobSystem": "Server=your-sql-server;Database=JobSystem;User Id=sa;Password=yourpassword;TrustServerCertificate=True;"
  },
  "AWS": {
    "Region": "us-east-1",
    "S3Bucket": "your-job-system-code-bucket",
    "VpcId": "vpc-xxxxxxxxx",
    "SubnetIds": ["subnet-aaaaaaaa", "subnet-bbbbbbbb"],
    "SecurityGroupId": "sg-xxxxxxxxx",
    "WorkerInstanceProfile": "job-system-worker-profile"
  },
  "InstanceTypes": {
    "Small": "t3.small",
    "Medium": "t3.medium",
    "Large": "t3.large"
  },
  "Scaling": {
    "Aggressive": {
      "ItemsPerWorker": 10,
      "IdleTimeoutSeconds": 60
    },
    "Gradual": {
      "ItemsPerWorker": 25,
      "ScaleUpDelaySeconds": 30,
      "IdleTimeoutSeconds": 120
    }
  },
  "OpenTelemetry": {
    "Endpoint": "http://signoz:4317",
    "ServiceName": "job-system-manager"
  },
  "ManagerUrl": "http://your-manager-host:5000"
}
```

## Configuration Sections

### ConnectionStrings

| Key | Description |
|-----|-------------|
| `JobSystem` | SQL Server connection string for the manager database |

### AWS

| Key | Description |
|-----|-------------|
| `Region` | AWS region for EC2 and SQS operations |
| `S3Bucket` | S3 bucket name for job code storage |
| `VpcId` | VPC ID where workers will be launched |
| `SubnetIds` | Array of subnet IDs for worker placement |
| `SecurityGroupId` | Security group ID for workers |
| `WorkerInstanceProfile` | IAM instance profile name for workers |

### InstanceTypes

Maps instance sizes to EC2 instance types:

| Key | Default | Description |
|-----|---------|-------------|
| `Small` | `t3.small` | 2 vCPU, 2 GB RAM |
| `Medium` | `t3.medium` | 2 vCPU, 4 GB RAM |
| `Large` | `t3.large` | 2 vCPU, 8 GB RAM |

### Scaling

#### Aggressive Mode

| Key | Default | Description |
|-----|---------|-------------|
| `ItemsPerWorker` | 10 | Queue items per worker for scaling calculations |
| `IdleTimeoutSeconds` | 60 | Seconds before terminating idle workers |

#### Gradual Mode

| Key | Default | Description |
|-----|---------|-------------|
| `ItemsPerWorker` | 25 | Queue items per worker for scaling calculations |
| `ScaleUpDelaySeconds` | 30 | Delay between adding workers |
| `IdleTimeoutSeconds` | 120 | Seconds before terminating idle workers |

### OpenTelemetry

| Key | Description |
|-----|-------------|
| `Endpoint` | OTLP exporter endpoint (e.g., SigNoz collector) |
| `ServiceName` | Service name for tracing |

### ManagerUrl

The public URL of the manager, used by workers to call back.

## Environment Variables

All configuration can be overridden with environment variables using the `__` separator:

```bash
# Connection string
export ConnectionStrings__JobSystem="Server=..."

# AWS settings
export AWS__Region="us-east-1"
export AWS__S3Bucket="my-bucket"
export AWS__SubnetIds__0="subnet-aaaaaaaa"
export AWS__SubnetIds__1="subnet-bbbbbbbb"

# Scaling
export Scaling__Aggressive__ItemsPerWorker="10"
```

## Database Setup

### Create Database

```sql
CREATE DATABASE JobSystem;
```

### Run Migrations

```bash
dotnet ef database update --project src/JobSystem.Manager
```

Or generate a SQL script:

```bash
dotnet ef migrations script --project src/JobSystem.Manager -o migration.sql
```

## AWS Credentials

The manager uses the AWS SDK default credential chain:

1. Environment variables (`AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`)
2. AWS credentials file (`~/.aws/credentials`)
3. IAM instance profile (when running on EC2)

For EC2 deployment, use the IAM instance profile created by Terraform.

## Running the Manager

### Development

```bash
cd src/JobSystem.Manager
dotnet run
```

### Production

```bash
dotnet publish -c Release -o ./publish
cd publish
dotnet JobSystem.Manager.dll
```

### Docker

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY publish/ .
EXPOSE 5000
ENTRYPOINT ["dotnet", "JobSystem.Manager.dll"]
```

## Health Checks

The manager exposes a health endpoint:

```
GET /health
```

## Logging

Logs are written to:
- Console (development)
- OpenTelemetry (if configured)

Log levels can be configured in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  }
}
```
