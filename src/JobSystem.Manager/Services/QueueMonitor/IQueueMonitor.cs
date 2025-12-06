using JobSystem.Manager.Data.Entities;

namespace JobSystem.Manager.Services.QueueMonitor;

public interface IQueueMonitor
{
    Task<int> GetQueueDepthAsync(JobDefinition job, CancellationToken cancellationToken = default);
    bool CanHandle(QueueType queueType);
}

public interface IQueueMonitorFactory
{
    IQueueMonitor GetMonitor(QueueType queueType);
}

public class QueueMonitorFactory : IQueueMonitorFactory
{
    private readonly IEnumerable<IQueueMonitor> _monitors;

    public QueueMonitorFactory(IEnumerable<IQueueMonitor> monitors)
    {
        _monitors = monitors;
    }

    public IQueueMonitor GetMonitor(QueueType queueType)
    {
        var monitor = _monitors.FirstOrDefault(m => m.CanHandle(queueType));
        if (monitor == null)
        {
            throw new InvalidOperationException($"No queue monitor found for queue type: {queueType}");
        }
        return monitor;
    }
}
