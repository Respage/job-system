using Microsoft.Extensions.Configuration;
using Moq;
using JobSystem.Manager.Services.Scaling;
using Xunit;

namespace JobSystem.Manager.Tests.Scaling;

public class AggressiveScalerTests
{
    private readonly AggressiveScaler _scaler;

    public AggressiveScalerTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Scaling:Aggressive:ItemsPerWorker"] = "10"
            })
            .Build();

        _scaler = new AggressiveScaler(configuration);
    }

    [Fact]
    public void CalculateScaling_WithEmptyQueue_ReturnsMinWorkers()
    {
        // Arrange
        var queueDepth = 0;
        var currentWorkers = 2;
        var minWorkers = 1;
        var maxWorkers = 10;

        // Act
        var decision = _scaler.CalculateScaling(queueDepth, currentWorkers, minWorkers, maxWorkers);

        // Assert
        Assert.Equal(minWorkers, decision.TargetWorkerCount);
        Assert.Equal(1, decision.WorkersToRemove);
        Assert.Equal(0, decision.WorkersToAdd);
    }

    [Fact]
    public void CalculateScaling_WithQueueItems_ScalesUp()
    {
        // Arrange
        var queueDepth = 50; // Should need 5 workers (50/10)
        var currentWorkers = 2;
        var minWorkers = 1;
        var maxWorkers = 10;

        // Act
        var decision = _scaler.CalculateScaling(queueDepth, currentWorkers, minWorkers, maxWorkers);

        // Assert
        Assert.Equal(5, decision.TargetWorkerCount);
        Assert.Equal(3, decision.WorkersToAdd);
        Assert.Equal(0, decision.WorkersToRemove);
    }

    [Fact]
    public void CalculateScaling_RespectsMaxWorkers()
    {
        // Arrange
        var queueDepth = 1000; // Would need 100 workers
        var currentWorkers = 5;
        var minWorkers = 1;
        var maxWorkers = 10;

        // Act
        var decision = _scaler.CalculateScaling(queueDepth, currentWorkers, minWorkers, maxWorkers);

        // Assert
        Assert.Equal(10, decision.TargetWorkerCount);
        Assert.Equal(5, decision.WorkersToAdd);
    }

    [Fact]
    public void CalculateScaling_RespectsMinWorkers()
    {
        // Arrange
        var queueDepth = 5; // Would need 1 worker
        var currentWorkers = 5;
        var minWorkers = 3;
        var maxWorkers = 10;

        // Act
        var decision = _scaler.CalculateScaling(queueDepth, currentWorkers, minWorkers, maxWorkers);

        // Assert
        Assert.Equal(3, decision.TargetWorkerCount);
    }

    [Fact]
    public void CalculateScaling_NoChangeNeeded_ReturnsNoScaling()
    {
        // Arrange
        var queueDepth = 30; // Needs 3 workers
        var currentWorkers = 3;
        var minWorkers = 1;
        var maxWorkers = 10;

        // Act
        var decision = _scaler.CalculateScaling(queueDepth, currentWorkers, minWorkers, maxWorkers);

        // Assert
        Assert.False(decision.ShouldScale);
        Assert.Equal(0, decision.WorkersToAdd);
        Assert.Equal(0, decision.WorkersToRemove);
    }
}
