using Microsoft.Extensions.Configuration;
using JobSystem.Manager.Services.Scaling;
using Xunit;

namespace JobSystem.Manager.Tests.Scaling;

public class GradualScalerTests
{
    private readonly GradualScaler _scaler;

    public GradualScalerTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Scaling:Gradual:ItemsPerWorker"] = "25"
            })
            .Build();

        _scaler = new GradualScaler(configuration);
    }

    [Fact]
    public void CalculateScaling_ScalesUpOneAtATime()
    {
        // Arrange
        var queueDepth = 100; // Would need 4 workers
        var currentWorkers = 1;
        var minWorkers = 1;
        var maxWorkers = 10;

        // Act
        var decision = _scaler.CalculateScaling(queueDepth, currentWorkers, minWorkers, maxWorkers);

        // Assert
        Assert.Equal(1, decision.WorkersToAdd); // Only 1 at a time
        Assert.Equal(0, decision.WorkersToRemove);
    }

    [Fact]
    public void CalculateScaling_ScalesDownOneAtATime()
    {
        // Arrange
        var queueDepth = 25; // Needs 1 worker
        var currentWorkers = 5;
        var minWorkers = 1;
        var maxWorkers = 10;

        // Act
        var decision = _scaler.CalculateScaling(queueDepth, currentWorkers, minWorkers, maxWorkers);

        // Assert
        Assert.Equal(0, decision.WorkersToAdd);
        Assert.Equal(1, decision.WorkersToRemove); // Only 1 at a time
    }

    [Fact]
    public void CalculateScaling_WithEmptyQueue_ScalesDownGradually()
    {
        // Arrange
        var queueDepth = 0;
        var currentWorkers = 5;
        var minWorkers = 0;
        var maxWorkers = 10;

        // Act
        var decision = _scaler.CalculateScaling(queueDepth, currentWorkers, minWorkers, maxWorkers);

        // Assert
        Assert.Equal(1, decision.WorkersToRemove);
        Assert.Equal(0, decision.WorkersToAdd);
    }

    [Fact]
    public void CalculateScaling_AtTargetCount_NoScaling()
    {
        // Arrange
        var queueDepth = 50; // Needs 2 workers
        var currentWorkers = 2;
        var minWorkers = 1;
        var maxWorkers = 10;

        // Act
        var decision = _scaler.CalculateScaling(queueDepth, currentWorkers, minWorkers, maxWorkers);

        // Assert
        Assert.False(decision.ShouldScale);
    }
}
