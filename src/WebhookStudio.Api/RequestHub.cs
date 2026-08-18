using Microsoft.AspNetCore.SignalR;

namespace WebhookStudio.Api;

public sealed class RequestHub : Hub
{
    public Task JoinEndpoint(string endpointId) => Groups.AddToGroupAsync(Context.ConnectionId, endpointId);
    public Task LeaveEndpoint(string endpointId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, endpointId);
}
