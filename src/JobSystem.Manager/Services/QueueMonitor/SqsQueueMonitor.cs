using Amazon.SQS;
using Amazon.SQS.Model;
using JobSystem.Manager.Data.Entities;

namespace JobSystem.Manager.Services.QueueMonitor;

public class SqsQueueMonitor : IQueueMonitor
{
    private readonly IAmazonSQS _sqsClient;
    private readonly ILogger<SqsQueueMonitor> _logger;

    public SqsQueueMonitor(IAmazonSQS sqsClient, ILogger<SqsQueueMonitor> logger)
    {
        _sqsClient = sqsClient;
        _logger = logger;
    }

    public bool CanHandle(QueueType queueType) => queueType == QueueType.SQS;

    public async Task<int> GetQueueDepthAsync(JobDefinition job, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetQueueAttributesRequest
            {
                QueueUrl = job.QueueName, // QueueName contains the full SQS URL
                AttributeNames = new List<string>
                {
                    "ApproximateNumberOfMessages",
                    "ApproximateNumberOfMessagesNotVisible"
                }
            };

            var response = await _sqsClient.GetQueueAttributesAsync(request, cancellationToken);

            var visibleMessages = 0;
            var invisibleMessages = 0;

            if (response.Attributes.TryGetValue("ApproximateNumberOfMessages", out var visible))
            {
                int.TryParse(visible, out visibleMessages);
            }

            if (response.Attributes.TryGetValue("ApproximateNumberOfMessagesNotVisible", out var invisible))
            {
                int.TryParse(invisible, out invisibleMessages);
            }

            var totalDepth = visibleMessages + invisibleMessages;

            _logger.LogDebug(
                "SQS queue {QueueUrl} depth: {Visible} visible, {Invisible} in-flight, {Total} total",
                job.QueueName, visibleMessages, invisibleMessages, totalDepth);

            return totalDepth;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get queue depth for SQS queue {QueueUrl}", job.QueueName);
            throw;
        }
    }
}
