using Microsoft.AspNetCore.SignalR;

namespace SkiJobControl.Console.Hubs;

public class NodeHub : Hub
{
    public async Task SubscribeToNode(string nodeId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, nodeId);
    }

    public async Task UnsubscribeFromNode(string nodeId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, nodeId);
    }
}
