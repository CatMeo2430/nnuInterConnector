using Microsoft.AspNetCore.SignalR;
using Server.Models;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Server.Hubs;

public class InterconnectionHub : Hub
{
    private static readonly ConcurrentDictionary<int, ClientInfo> _clients = new();
    private static readonly ConcurrentDictionary<string, int> _uuidToId = new();
    private static readonly Random _random = new();
    private readonly ILogger<InterconnectionHub> _logger;

    public InterconnectionHub(ILogger<InterconnectionHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var uuid = Context.GetHttpContext()?.Request.Headers["X-Client-UUID"].ToString();
        
        if (string.IsNullOrEmpty(uuid))
        {
            _logger.LogWarning("Client connected without UUID");
            Context.Abort();
            return;
        }

        if (_uuidToId.TryGetValue(uuid, out var clientId))
        {
            if (_clients.TryGetValue(clientId, out var clientInfo))
            {
                clientInfo.ConnectionId = Context.ConnectionId;
                clientInfo.LastHeartbeat = DateTime.UtcNow;
                _logger.LogInformation("Client reconnected: ID={ClientId}, UUID={Uuid}", clientId, uuid);
            }
        }

        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var kvp in _clients)
        {
            if (kvp.Value.ConnectionId == Context.ConnectionId)
            {
                _logger.LogInformation("Client disconnected: ID={ClientId}", kvp.Key);
                break;
            }
        }

        return base.OnDisconnectedAsync(exception);
    }

    public async Task RegisterClient(string uuid, string ipAddress)
    {
        if (_uuidToId.TryGetValue(uuid, out var existingId))
        {
            _logger.LogInformation("Client already registered: ID={ClientId}, IP={Ip}", existingId, ipAddress);
            await Clients.Caller.SendAsync("RegistrationSuccess", existingId);
            return;
        }

        var clientId = GenerateUniqueId();
        var clientInfo = new ClientInfo
        {
            Uuid = uuid,
            IpAddress = ipAddress,
            LastHeartbeat = DateTime.UtcNow,
            ConnectionId = Context.ConnectionId
        };

        _clients[clientId] = clientInfo;
        _uuidToId[uuid] = clientId;

        _logger.LogInformation("New client registered: ID={ClientId}, UUID={Uuid}, IP={Ip}", clientId, uuid, ipAddress);
        await Clients.Caller.SendAsync("RegistrationSuccess", clientId);
    }

    public Task UpdateHeartbeat(string ipAddress)
    {
        var uuid = Context.GetHttpContext()?.Request.Headers["X-Client-UUID"].ToString();
        if (string.IsNullOrEmpty(uuid) || !_uuidToId.TryGetValue(uuid, out var clientId))
            return Task.CompletedTask;

        if (_clients.TryGetValue(clientId, out var clientInfo))
        {
            clientInfo.LastHeartbeat = DateTime.UtcNow;
            clientInfo.IpAddress = ipAddress;
            _logger.LogDebug("Heartbeat updated: ID={ClientId}, IP={Ip}", clientId, ipAddress);
        }
        
        return Task.CompletedTask;
    }

    public async Task RequestConnection(int targetId)
    {
        var uuid = Context.GetHttpContext()?.Request.Headers["X-Client-UUID"].ToString();
        if (string.IsNullOrEmpty(uuid) || !_uuidToId.TryGetValue(uuid, out var requesterId))
            return;

        if (!_clients.TryGetValue(requesterId, out var requesterInfo))
            return;

        if (!_clients.TryGetValue(targetId, out var targetInfo))
        {
            await Clients.Caller.SendAsync("ConnectionFailed", "目标客户端不存在或已离线");
            return;
        }

        _logger.LogInformation("Connection request: {RequesterId} -> {TargetId}", requesterId, targetId);
        await Clients.Client(targetInfo.ConnectionId).SendAsync("ConnectionRequest", requesterId, requesterInfo.IpAddress);
    }

    public async Task AcceptConnection(int requesterId)
    {
        var uuid = Context.GetHttpContext()?.Request.Headers["X-Client-UUID"].ToString();
        if (string.IsNullOrEmpty(uuid) || !_uuidToId.TryGetValue(uuid, out var accepterId))
            return;

        if (!_clients.TryGetValue(requesterId, out var requesterInfo) || 
            !_clients.TryGetValue(accepterId, out var accepterInfo))
            return;

        _logger.LogInformation("Connection accepted: {RequesterId} <-> {AccepterId}", requesterId, accepterId);

        await Clients.Client(requesterInfo.ConnectionId).SendAsync("ConnectionEstablished", accepterId, accepterInfo.IpAddress);
        await Clients.Client(accepterInfo.ConnectionId).SendAsync("ConnectionEstablished", requesterId, requesterInfo.IpAddress);
    }

    public async Task RejectConnection(int requesterId)
    {
        var uuid = Context.GetHttpContext()?.Request.Headers["X-Client-UUID"].ToString();
        if (string.IsNullOrEmpty(uuid) || !_uuidToId.TryGetValue(uuid, out var rejecterId))
            return;

        if (!_clients.TryGetValue(requesterId, out var requesterInfo))
            return;

        _logger.LogInformation("Connection rejected: {RequesterId} <- {RejecterId}", requesterId, rejecterId);
        await Clients.Client(requesterInfo.ConnectionId).SendAsync("ConnectionRejected", rejecterId);
    }

    private int GenerateUniqueId()
    {
        int id;
        do
        {
            id = _random.Next(100000, 1000000);
        } while (_clients.ContainsKey(id));

        return id;
    }

    public static void CleanupInactiveClients(TimeSpan timeout)
    {
        var cutoffTime = DateTime.UtcNow - timeout;
        var inactiveClients = _clients.Where(kvp => kvp.Value.LastHeartbeat < cutoffTime).ToList();

        foreach (var kvp in inactiveClients)
        {
            _clients.TryRemove(kvp.Key, out _);
            _uuidToId.TryRemove(kvp.Value.Uuid, out _);
        }
    }
}