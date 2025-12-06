using MongoDB.Bson;
using MongoDB.Driver;
using JobSystem.Manager.Data.Entities;

namespace JobSystem.Manager.Services.QueueMonitor;

public class MongoQueueMonitor : IQueueMonitor
{
    private readonly ILogger<MongoQueueMonitor> _logger;

    public MongoQueueMonitor(ILogger<MongoQueueMonitor> logger)
    {
        _logger = logger;
    }

    public bool CanHandle(QueueType queueType) => queueType == QueueType.MongoDB;

    public async Task<int> GetQueueDepthAsync(JobDefinition job, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = new MongoClient(job.QueueConnectionString);

            // Parse the queue name which should be in format "database.collection"
            var parts = job.QueueName.Split('.');
            if (parts.Length != 2)
            {
                throw new InvalidOperationException(
                    $"Invalid MongoDB queue name format: {job.QueueName}. Expected format: 'database.collection'");
            }

            var databaseName = parts[0];
            var collectionName = parts[1];

            var database = client.GetDatabase(databaseName);
            var collection = database.GetCollection<BsonDocument>(collectionName);

            // Count documents with status = "queued" or "pending"
            // This assumes a standard queue schema with a status field
            var filter = Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.Eq("status", "queued"),
                Builders<BsonDocument>.Filter.Eq("status", "pending"),
                Builders<BsonDocument>.Filter.Eq("status", "Queued"),
                Builders<BsonDocument>.Filter.Eq("status", "Pending")
            );

            var count = await collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

            _logger.LogDebug(
                "MongoDB queue {Database}.{Collection} depth: {Count}",
                databaseName, collectionName, count);

            return (int)count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get queue depth for MongoDB queue {QueueName}", job.QueueName);
            throw;
        }
    }
}
