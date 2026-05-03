using SkiJobControl.Console.Services;
using SkiJobControl.Protos;
using Grpc.Core;
using Moq;

namespace SkiJobControl.Tests;

public class NodeSessionManagerTests
{
    [Fact]
    public void Register_ShouldAddNodeToActiveNodes()
    {
        // Arrange
        var manager = new NodeSessionManager();
        var mockWriter = new Mock<IServerStreamWriter<ControlCommand>>();

        // Act
        manager.Register("node_1", mockWriter.Object);

        // Assert
        Assert.Contains("node_1", manager.GetActiveNodes());
    }

    [Fact]
    public async Task SendCommandAsync_ShouldReturnFalse_WhenNodeNotRegistered()
    {
        // Arrange
        var manager = new NodeSessionManager();
        var command = new ControlCommand { CommandId = "cmd_1" };

        // Act
        var result = await manager.SendCommandAsync("unknown_node", command);

        // Assert
        Assert.False(result);
    }
}
