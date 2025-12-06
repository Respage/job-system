# Worker Development Guide

This guide explains how to develop job workers that integrate with the Job System.

## Overview

Workers are the processes that execute your job code. The Job System supports two types of workers:

- **Node.js Workers** - For JavaScript/TypeScript jobs
- **.NET Workers** - For C# jobs

## Node.js Worker Development

### Project Structure

```
my-job/
├── package.json
├── index.js          # Main entry point
└── lib/
    └── processor.js  # Your job logic
```

### Integration

Your job code must export either:
1. A `run` function that receives the Job Manager client
2. A default function that receives the client

```javascript
// index.js
const { processQueue } = require('./lib/processor');

// Option 1: Named export
module.exports.run = async function(client) {
    console.log('Worker started');

    // Your main processing loop
    while (true) {
        try {
            const item = await getNextItem();

            if (item) {
                await client.reportStatus('Processing', item.id);
                await processItem(item);
                await client.reportComplete(item.id, true);
            } else {
                // No work available, wait before checking again
                await sleep(5000);
            }
        } catch (error) {
            console.error('Processing error:', error);
            await client.reportStatus('Failed', null, null, error.message);
        }
    }
};

// Option 2: Default export
module.exports = async function(client) {
    // Same as above
};
```

### Using the Client

The client provides these methods:

```javascript
// Report status
await client.reportStatus('Processing', itemId, progressPercent, message);

// Send heartbeat (automatically done by bootstrap, but can be manual)
await client.sendHeartbeat();

// Report completion
await client.reportComplete(itemId, success, message);
```

### Environment Variables

Available in your job code:

| Variable | Description |
|----------|-------------|
| `JOB_NAME` | Name of the job |
| `MANAGER_URL` | URL of the manager API |
| `API_KEY` | API key for this job |
| `QUEUE_TYPE` | Queue type (SQS or MongoDB) |
| `QUEUE_CONNECTION` | Queue connection string |
| `QUEUE_NAME` | Queue name/URL |

### Example: SQS Queue Processor

```javascript
const { SQSClient, ReceiveMessageCommand, DeleteMessageCommand } = require('@aws-sdk/client-sqs');

module.exports.run = async function(client) {
    const sqsClient = new SQSClient({ region: process.env.AWS_REGION });
    const queueUrl = process.env.QUEUE_NAME;

    while (true) {
        const response = await sqsClient.send(new ReceiveMessageCommand({
            QueueUrl: queueUrl,
            MaxNumberOfMessages: 1,
            WaitTimeSeconds: 20
        }));

        if (response.Messages && response.Messages.length > 0) {
            const message = response.Messages[0];

            try {
                await client.reportStatus('Processing', message.MessageId);

                // Process the message
                const body = JSON.parse(message.Body);
                await processMessage(body);

                // Delete from queue
                await sqsClient.send(new DeleteMessageCommand({
                    QueueUrl: queueUrl,
                    ReceiptHandle: message.ReceiptHandle
                }));

                await client.reportComplete(message.MessageId, true);
            } catch (error) {
                await client.reportComplete(message.MessageId, false, error.message);
            }
        }
    }
};
```

---

## .NET Worker Development

### Project Structure

```
MyJob/
├── MyJob.csproj
├── Worker.cs         # Your worker implementation
└── Services/
    └── Processor.cs  # Your job logic
```

### Project File

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="JobSystem.Worker.Sdk" Version="1.0.0" />
    <PackageReference Include="AWSSDK.SQS" Version="3.*" />
  </ItemGroup>
</Project>
```

### Implementing IJobWorker

```csharp
using JobSystem.Worker.Sdk;

public class MyWorker : IJobWorker
{
    public async Task RunAsync(IJobManagerClient client, CancellationToken cancellationToken)
    {
        Console.WriteLine("Worker started");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var item = await GetNextItemAsync(cancellationToken);

                if (item != null)
                {
                    await client.ReportStatusAsync("Processing", item.Id);
                    await ProcessItemAsync(item, cancellationToken);
                    await client.ReportCompleteAsync(item.Id, true);
                }
                else
                {
                    await Task.Delay(5000, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                await client.ReportStatusAsync("Failed", null, null, ex.Message);
            }
        }
    }
}
```

### Using the Client

```csharp
public interface IJobManagerClient
{
    Task ReportStatusAsync(string status, string? itemId = null,
                           int? progressPercent = null, string? message = null,
                           CancellationToken cancellationToken = default);

    Task SendHeartbeatAsync(CancellationToken cancellationToken = default);

    Task ReportCompleteAsync(string itemId, bool success, string? message = null,
                             CancellationToken cancellationToken = default);
}
```

### Example: SQS Queue Processor

```csharp
using Amazon.SQS;
using Amazon.SQS.Model;
using JobSystem.Worker.Sdk;

public class SqsWorker : IJobWorker
{
    public async Task RunAsync(IJobManagerClient client, CancellationToken cancellationToken)
    {
        var sqsClient = new AmazonSQSClient();
        var queueUrl = Environment.GetEnvironmentVariable("QUEUE_NAME");

        while (!cancellationToken.IsCancellationRequested)
        {
            var response = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = queueUrl,
                MaxNumberOfMessages = 1,
                WaitTimeSeconds = 20
            }, cancellationToken);

            if (response.Messages.Count > 0)
            {
                var message = response.Messages[0];

                try
                {
                    await client.ReportStatusAsync("Processing", message.MessageId);

                    // Process the message
                    await ProcessMessageAsync(message.Body, cancellationToken);

                    // Delete from queue
                    await sqsClient.DeleteMessageAsync(queueUrl, message.ReceiptHandle, cancellationToken);

                    await client.ReportCompleteAsync(message.MessageId, true);
                }
                catch (Exception ex)
                {
                    await client.ReportCompleteAsync(message.MessageId, false, ex.Message);
                }
            }
        }
    }
}
```

---

## Deploying Your Job

1. **Create the job in the manager UI or API**

2. **Push your code to the Git repository**

3. **Trigger a code sync**
   ```bash
   curl -X POST http://manager:5000/api/jobs/{jobId}/code-sync
   ```

4. **Enable the job**
   ```bash
   curl -X POST http://manager:5000/api/jobs/{jobId}/enable
   ```

The manager will automatically scale workers based on queue depth.

---

## Best Practices

### Error Handling

- Always wrap processing in try/catch
- Report failures with meaningful messages
- Don't let exceptions crash the worker

### Graceful Shutdown

- Handle SIGTERM signal
- Finish processing current item before stopping
- The worker base handles this automatically

### Idempotency

- Design jobs to be safely re-run
- Queue items may be delivered more than once

### Monitoring

- Use structured logging
- Report progress for long-running items
- The client automatically sends heartbeats

### Resource Cleanup

- Clean up temporary files
- Close database connections
- Release external resources

---

## Testing Locally

### Node.js

```bash
# Set environment variables
export MANAGER_URL="http://localhost:5000"
export API_KEY="your-api-key"
export JOB_NAME="my-job"

# Run locally
node index.js
```

### .NET

```bash
# Set environment variables
export MANAGER_URL="http://localhost:5000"
export API_KEY="your-api-key"
export JOB_NAME="my-job"

# Run locally
dotnet run
```
