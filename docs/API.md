# Job System Manager API Documentation

## Overview

The Job System Manager exposes a REST API for managing jobs, workers, and code synchronization.

## Authentication

Worker endpoints require an API key passed via the `X-Api-Key` header. Each job has its own unique API key.

## Base URL

```
http://your-manager-host:5000/api
```

---

## Jobs API

### List All Jobs

```
GET /api/jobs
```

**Response:**
```json
[
  {
    "id": "uuid",
    "name": "my-job",
    "description": "Job description",
    "language": "Node",
    "queueType": "SQS",
    "queueName": "https://sqs.us-east-1.amazonaws.com/...",
    "instanceSize": "Small",
    "useSpotInstances": true,
    "scaleMode": "Aggressive",
    "minWorkers": 1,
    "maxWorkers": 10,
    "queueItemTimeoutSeconds": 300,
    "queuePollingIntervalSeconds": 30,
    "gitRepoUrl": "https://github.com/...",
    "gitBranch": "main",
    "s3CodePath": "jobs/my-job",
    "isEnabled": true,
    "createdAt": "2024-01-01T00:00:00Z",
    "updatedAt": "2024-01-01T00:00:00Z",
    "activeWorkerCount": 3
  }
]
```

### Get Job Details

```
GET /api/jobs/{id}
```

### Create Job

```
POST /api/jobs
Content-Type: application/json

{
  "name": "my-job",
  "description": "Optional description",
  "language": "Node",  // or "CSharp"
  "queueType": "SQS",  // or "MongoDB"
  "queueConnectionString": "us-east-1",
  "queueName": "https://sqs.us-east-1.amazonaws.com/123456789/my-queue",
  "instanceSize": "Small",  // Small, Medium, Large
  "useSpotInstances": true,
  "scaleMode": "Gradual",  // Aggressive, Gradual
  "minWorkers": 1,
  "maxWorkers": 10,
  "queueItemTimeoutSeconds": 300,
  "queuePollingIntervalSeconds": 30,
  "gitRepoUrl": "https://github.com/myorg/my-job-repo",
  "gitBranch": "main",
  "s3CodePath": "jobs/my-job"
}
```

### Update Job

```
PUT /api/jobs/{id}
Content-Type: application/json

{
  "description": "Updated description",
  "instanceSize": "Medium",
  "minWorkers": 2
}
```

*Note: Jobs with active workers cannot be updated. Disable the job first.*

### Delete Job

```
DELETE /api/jobs/{id}
```

*Note: Jobs with active workers cannot be deleted. Disable the job first.*

### Enable Job

```
POST /api/jobs/{id}/enable
```

### Disable Job

```
POST /api/jobs/{id}/disable
```

### Get Job Workers

```
GET /api/jobs/{id}/workers
```

### Regenerate API Key

```
POST /api/jobs/{id}/regenerate-api-key
```

**Response:**
```json
{
  "apiKey": "new-api-key-here"
}
```

---

## Workers API

### Register Worker

Called by workers on startup.

```
POST /api/workers/register
Content-Type: application/json

{
  "jobName": "my-job",
  "instanceId": "i-1234567890abcdef0"
}
```

**Response:**
```json
{
  "workerId": "uuid",
  "config": {
    "jobName": "my-job",
    "queueType": "SQS",
    "queueConnectionString": "us-east-1",
    "queueName": "https://sqs...",
    "queueItemTimeoutSeconds": 300
  }
}
```

### Report Status

```
POST /api/workers/{workerId}/status
X-Api-Key: {apiKey}
Content-Type: application/json

{
  "status": "Processing",  // Started, Processing, Completed, Failed
  "itemId": "queue-item-id",
  "progressPercent": 50,
  "message": "Processing step 2 of 4"
}
```

### Send Heartbeat

```
POST /api/workers/{workerId}/heartbeat
X-Api-Key: {apiKey}
```

### Report Completion

```
POST /api/workers/{workerId}/complete
X-Api-Key: {apiKey}
Content-Type: application/json

{
  "itemId": "queue-item-id",
  "success": true,
  "message": "Completed successfully"
}
```

### List All Workers

```
GET /api/workers
GET /api/workers?status=Running
```

### Get Worker Details

```
GET /api/workers/{workerId}
```

### Get Worker Logs

```
GET /api/workers/{workerId}/logs?limit=100
```

---

## Code Sync API

### Trigger Code Sync

```
POST /api/jobs/{jobId}/code-sync
```

**Response (202 Accepted):**
```json
{
  "message": "Code sync started",
  "jobId": "uuid",
  "jobName": "my-job"
}
```

### Get Sync History

```
GET /api/jobs/{jobId}/code-sync?limit=10
```

### Get Latest Sync

```
GET /api/jobs/{jobId}/code-sync/latest
```

---

## Dashboard API

### Get Dashboard Statistics

```
GET /api/dashboard/stats
```

**Response:**
```json
{
  "totalJobs": 5,
  "enabledJobs": 4,
  "totalActiveWorkers": 12,
  "spotWorkers": 8,
  "onDemandWorkers": 4,
  "jobStats": [
    {
      "jobId": "uuid",
      "jobName": "my-job",
      "isEnabled": true,
      "activeWorkers": 3,
      "minWorkers": 1,
      "maxWorkers": 10
    }
  ]
}
```

---

## Error Responses

All endpoints may return the following error responses:

### 400 Bad Request
```json
{
  "message": "Validation error message"
}
```

### 401 Unauthorized
```json
{
  "message": "Invalid API key"
}
```

### 404 Not Found
```json
{
  "message": "Resource not found"
}
```

### 409 Conflict
```json
{
  "message": "Resource already exists or operation not allowed"
}
```

### 500 Internal Server Error
```json
{
  "message": "An error occurred processing your request"
}
```
