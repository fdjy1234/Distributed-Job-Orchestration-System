using Grpc.Core;
using Microsoft.AspNetCore.SignalR;
using SkiJobControl.Console.Hubs;
using SkiJobControl.Protos;
using System.Collections.Concurrent;

namespace SkiJobControl.Console.Services;

public class JobControlServiceImplementation : JobControlService.JobControlServiceBase
{
    private readonly IHubContext<NodeHub> _hubContext;
    private readonly NodeSessionManager _sessionManager;
    private readonly ILogger<JobControlServiceImplementation> _logger;
    private readonly ConcurrentDictionary<string, string> _workerStates = new();

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
                    await BroadcastLogAsync("info", $"Node connected: {nodeId}");
                }

                foreach (var worker in status.Workers)
                {
                    string key = $"{status.NodeId}:{worker.ProcessId}";
                    string currentState = $"{worker.Status}|{worker.JobId}";
                    if (!_workerStates.TryGetValue(key, out var oldState) || !string.Equals(oldState, currentState, StringComparison.Ordinal))
                    {
                        _workerStates[key] = currentState;
                        var jobText = string.IsNullOrWhiteSpace(worker.JobId) ? "N/A" : worker.JobId;
                        await BroadcastLogAsync("info", $"{status.NodeId} Worker {worker.ProcessId} => {worker.Status} (Job: {jobText})");
                    }
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
            if (!string.IsNullOrWhiteSpace(nodeId))
            {
                await BroadcastLogAsync("warn", $"Node disconnected: {nodeId}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in session for node {NodeId}", nodeId);
            await BroadcastLogAsync("error", $"Session error on node {nodeId ?? "unknown"}: {ex.Message}");
        }
        finally
        {
            if (nodeId != null)
            {
                _sessionManager.Unregister(nodeId);
                foreach (var key in _workerStates.Keys.Where(k => k.StartsWith(nodeId + ":", StringComparison.Ordinal)))
                {
                    _workerStates.TryRemove(key, out _);
                }
            }
        }
    }

    private Task BroadcastLogAsync(string level, string message)
    {
        return _hubContext.Clients.All.SendAsync("AppendSystemLog", new
        {
            level,
            message,
            timestamp = DateTimeOffset.Now
        });
    }
}
