using Grpc.Core;
using Microsoft.AspNetCore.SignalR;
using SkiJobControl.Console.Hubs;
using SkiJobControl.Protos;

namespace SkiJobControl.Console.Services;

public class JobControlServiceImplementation : JobControlService.JobControlServiceBase
{
    private readonly IHubContext<NodeHub> _hubContext;
    private readonly NodeSessionManager _sessionManager;
    private readonly ILogger<JobControlServiceImplementation> _logger;

    public JobControlServiceImplementation(
        IHubContext<NodeHub> hubContext, 
        NodeSessionManager sessionManager,
        ILogger<JobControlServiceImplementation> logger)
    {
        _hubContext = hubContext;
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public override async Task OpenSession(IAsyncStreamReader<NodeStatus> requestStream, IServerStreamWriter<ControlCommand> responseStream, ServerCallContext context)
    {
        string? nodeId = null;

        try
        {
            await foreach (var status in requestStream.ReadAllAsync())
            {
                if (nodeId == null)
                {
                    nodeId = status.NodeId;
                    _sessionManager.Register(nodeId, responseStream);
                    _logger.LogInformation("Node {NodeId} connected.", nodeId);
                }

                // Broadcast status to SignalR
                await _hubContext.Clients.All.SendAsync("UpdateNodeStatus", status);
                
                // Also broadcast to specific group if needed
                await _hubContext.Clients.Group(status.NodeId).SendAsync("UpdateNodeStatus", status);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Node {NodeId} disconnected (cancelled).", nodeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in session for node {NodeId}", nodeId);
        }
        finally
        {
            if (nodeId != null)
            {
                _sessionManager.Unregister(nodeId);
            }
        }
    }
}
