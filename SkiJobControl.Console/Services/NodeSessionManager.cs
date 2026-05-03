using System.Collections.Concurrent;
using SkiJobControl.Protos;
using Grpc.Core;

namespace SkiJobControl.Console.Services;

public class NodeSessionManager
{
    private readonly ConcurrentDictionary<string, IServerStreamWriter<ControlCommand>> _sessions = new();

    public void Register(string nodeId, IServerStreamWriter<ControlCommand> writer)
    {
        _sessions[nodeId] = writer;
    }

    public void Unregister(string nodeId)
    {
        _sessions.TryRemove(nodeId, out _);
    }

    public async Task<bool> SendCommandAsync(string nodeId, ControlCommand command)
    {
        if (_sessions.TryGetValue(nodeId, out var writer))
        {
            try
            {
                await writer.WriteAsync(command);
                return true;
            }
            catch
            {
                Unregister(nodeId);
            }
        }
        return false;
    }

    public IEnumerable<string> GetActiveNodes() => _sessions.Keys;
}
