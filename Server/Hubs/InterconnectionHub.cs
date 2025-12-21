using Microsoft.AspNetCore.SignalR;
using Server.Models;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Server.Hubs;

public class InterconnectionHub : Hub
{
    // 连接超时时间（秒）
    private const int CONNECTION_TIMEOUT_SECONDS = 30;
    
    // 冷却期时间（分钟）
    private const int COOLDOWN_MINUTES = 1;
    
    // 永久拒绝阈值（被拒绝次数）
    private const int PERMANENT_REJECT_THRESHOLD = 3;
    
    // 心跳超时时间（分钟）
    public const int HEARTBEAT_TIMEOUT_MINUTES = 2;
    
    // 随机ID范围
    private const int MIN_CLIENT_ID = 100000;
    private const int MAX_CLIENT_ID = 999999;
    
    private static readonly ConcurrentDictionary<int, ClientInfo> _clients = new();
    private static readonly ConcurrentDictionary<string, int> _uuidToId = new();
    private static readonly ConcurrentDictionary<int, HashSet<int>> _connections = new();
    private static readonly ConcurrentDictionary<(int requesterId, int targetId), CancellationTokenSource> _pendingRequests = new();
    private static readonly ConcurrentDictionary<(int requesterId, int targetId), (int rejectCount, DateTime lastRejectTime)> _rejectHistory = new();
    private static readonly Random _random = new();
    private readonly ILogger<InterconnectionHub> _logger;
    private readonly IHubContext<InterconnectionHub> _hubContext;

    public InterconnectionHub(ILogger<InterconnectionHub> logger, IHubContext<InterconnectionHub> hubContext)
    {
        _logger = logger;
        _hubContext = hubContext;
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

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        int? disconnectedClientId = null;
        string? disconnectedClientIp = null;
        
        foreach (var kvp in _clients)
        {
            if (kvp.Value.ConnectionId == Context.ConnectionId)
            {
                disconnectedClientId = kvp.Key;
                disconnectedClientIp = kvp.Value.IpAddress;
                _logger.LogInformation("Client disconnected: ID={ClientId}, IP={Ip}", kvp.Key, disconnectedClientIp);
                break;
            }
        }

        if (disconnectedClientId.HasValue)
        {
            var clientId = disconnectedClientId.Value;
            
            if (_connections.TryRemove(clientId, out var connectedClients))
            {
                foreach (var connectedClientId in connectedClients)
                {
                    if (_connections.TryGetValue(connectedClientId, out var otherConnections))
                    {
                        otherConnections.Remove(clientId);
                    }

                    if (_clients.TryGetValue(connectedClientId, out var connectedClientInfo))
                    {
                        _logger.LogInformation("Notifying client {ClientId} about disconnection of {DisconnectedId}", 
                            connectedClientId, clientId);
                        await Clients.Client(connectedClientInfo.ConnectionId)
                            .SendAsync("PeerDisconnected", clientId, disconnectedClientIp);
                    }
                }
            }

            if (_clients.TryRemove(clientId, out var clientInfo))
            {
                _uuidToId.TryRemove(clientInfo.Uuid, out _);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task RegisterClient(string uuid)
    {
        if (_uuidToId.TryGetValue(uuid, out var existingId))
        {
            if (_clients.TryGetValue(existingId, out var existingInfo))
            {
                existingInfo.ConnectionId = Context.ConnectionId;
                existingInfo.LastHeartbeat = DateTime.UtcNow;
                _logger.LogInformation("Client re-registered: ID={ClientId}, IP={Ip}", existingId, existingInfo.IpAddress);
            }
            await Clients.Caller.SendAsync("RegistrationSuccess", existingId);
            return;
        }

        var ipAddress = Controllers.RegistrationController.GetPendingIp(uuid);
        if (string.IsNullOrEmpty(ipAddress))
        {
            _logger.LogWarning("Client registration failed: IP not found for UUID={Uuid}", uuid);
            Context.Abort();
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
        Controllers.RegistrationController.RemovePendingRegistration(uuid);

        _logger.LogInformation("New client registered: ID={ClientId}, UUID={Uuid}, IP={Ip}", clientId, uuid, ipAddress);
        await Clients.Caller.SendAsync("RegistrationSuccess", clientId);
    }

    public Task UpdateHeartbeat()
    {
        var uuid = Context.GetHttpContext()?.Request.Headers["X-Client-UUID"].ToString();
        if (string.IsNullOrEmpty(uuid) || !_uuidToId.TryGetValue(uuid, out var clientId))
            return Task.CompletedTask;

        if (_clients.TryGetValue(clientId, out var clientInfo))
        {
            clientInfo.LastHeartbeat = DateTime.UtcNow;
            _logger.LogDebug("Heartbeat updated: ID={ClientId}", clientId);
        }
        
        return Task.CompletedTask;
    }

    public async Task RequestConnection(int targetId)
    {
        // 验证targetId范围
        if (targetId < MIN_CLIENT_ID || targetId > MAX_CLIENT_ID)
        {
            await Clients.Caller.SendAsync("ConnectionFailed", 7); // 7 = 无效的ID格式
            return;
        }
        
        var uuid = Context.GetHttpContext()?.Request.Headers["X-Client-UUID"].ToString();
        if (string.IsNullOrEmpty(uuid) || !_uuidToId.TryGetValue(uuid, out var requesterId))
            return;

        if (!_clients.TryGetValue(requesterId, out var requesterInfo))
            return;

        if (!_clients.TryGetValue(targetId, out var targetInfo))
        {
            _logger.LogInformation("Connection request failed: Target ID {TargetId} does not exist", targetId);
            await Clients.Caller.SendAsync("ConnectionFailed", 1); // 1 = 目标ID不存在
            return;
        }

        // 检查请求者是否已有待处理的请求（无论目标是谁）
        var hasPendingRequest = _pendingRequests.Keys.Any(k => k.requesterId == requesterId);
        if (hasPendingRequest)
        {
            _logger.LogInformation("Connection request rejected: Requester {RequesterId} already has a pending request", requesterId);
            await Clients.Caller.SendAsync("ConnectionFailed", 4); // 4 = 重复请求
            return;
        }

        // 检查冷却期和永久拒绝
        var rejectKey = (requesterId, targetId);
        if (_rejectHistory.TryGetValue(rejectKey, out var rejectInfo))
        {
            // 检查是否达到永久拒绝阈值
            if (rejectInfo.rejectCount >= PERMANENT_REJECT_THRESHOLD)
            {
                _logger.LogInformation("Connection request permanently rejected: Requester {RequesterId} has been rejected {RejectCount} times by {TargetId}", requesterId, rejectInfo.rejectCount, targetId);
                await Clients.Caller.SendAsync("ConnectionFailed", 5); // 5 = 永久拒绝
                return;
            }
            
            // 检查冷却期
            var timeSinceLastReject = DateTime.UtcNow - rejectInfo.lastRejectTime;
            if (timeSinceLastReject < TimeSpan.FromMinutes(COOLDOWN_MINUTES))
            {
                var remainingSeconds = (int)(TimeSpan.FromMinutes(COOLDOWN_MINUTES) - timeSinceLastReject).TotalSeconds;
                _logger.LogInformation("Connection request rejected: Requester {RequesterId} is in cooldown period for {TargetId}, remaining {RemainingSeconds}s", requesterId, targetId, remainingSeconds);
                await Clients.Caller.SendAsync("ConnectionFailed", 6); // 6 = 冷却期中
                return;
            }
        }

        _logger.LogInformation("Connection request: {RequesterId} -> {TargetId}", requesterId, targetId);
        
        // 创建取消令牌源用于超时控制
        var cts = new CancellationTokenSource();
        var requestKey = (requesterId, targetId);
        _pendingRequests.TryAdd(requestKey, cts);
        
        // 发送连接请求给目标
        await Clients.Client(targetInfo.ConnectionId).SendAsync("ConnectionRequest", requesterId, requesterInfo.IpAddress);
        
        // 启动30秒超时定时器
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(CONNECTION_TIMEOUT_SECONDS), cts.Token);
                
                // 超时处理
                if (_pendingRequests.TryRemove(requestKey, out _))
                {
                    _logger.LogInformation("Connection request timeout: {RequesterId} -> {TargetId}", requesterId, targetId);
                    
                    // 向请求方发送被拒绝消息
                    await Clients.Client(requesterInfo.ConnectionId).SendAsync("ConnectionRejected", targetId);
                    
                    // 向被请求方发送超时消息
                    await Clients.Client(targetInfo.ConnectionId).SendAsync("ConnectionTimeout", requesterId);
                }
            }
            catch (TaskCanceledException)
            {
                // 请求被正常处理（接受、拒绝或取消），取消定时器
                _logger.LogInformation("Connection request cancelled: {RequesterId} -> {TargetId}", requesterId, targetId);
            }
        });
    }

    public async Task AcceptConnection(int requesterId)
    {
        var uuid = Context.GetHttpContext()?.Request.Headers["X-Client-UUID"].ToString();
        if (string.IsNullOrEmpty(uuid) || !_uuidToId.TryGetValue(uuid, out var accepterId))
            return;

        if (!_clients.TryGetValue(requesterId, out var requesterInfo) || 
            !_clients.TryGetValue(accepterId, out var accepterInfo))
            return;

        // 取消超时定时器
        var requestKey = (requesterId, accepterId);
        if (_pendingRequests.TryRemove(requestKey, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        _logger.LogInformation("Connection accepted: {RequesterId} <-> {AccepterId}", requesterId, accepterId);

        _connections.AddOrUpdate(requesterId, 
            new HashSet<int> { accepterId }, 
            (key, oldValue) => { oldValue.Add(accepterId); return oldValue; });
        
        _connections.AddOrUpdate(accepterId, 
            new HashSet<int> { requesterId }, 
            (key, oldValue) => { oldValue.Add(requesterId); return oldValue; });

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

        // 取消超时定时器
        var requestKey = (requesterId, rejecterId);
        if (_pendingRequests.TryRemove(requestKey, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        _logger.LogInformation("Connection rejected: {RequesterId} <- {RejecterId}", requesterId, rejecterId);
        
        // 更新拒绝历史记录（只有主动拒绝才计入）
        var rejectKey = (requesterId, rejecterId);
        _rejectHistory.AddOrUpdate(rejectKey,
            (1, DateTime.UtcNow), // 第一次拒绝
            (key, oldValue) => (oldValue.rejectCount + 1, DateTime.UtcNow) // 增加拒绝次数
        );
        
        await Clients.Client(requesterInfo.ConnectionId).SendAsync("ConnectionRejected", rejecterId);
    }

    public async Task CancelConnection(int targetId)
    {
        var uuid = Context.GetHttpContext()?.Request.Headers["X-Client-UUID"].ToString();
        if (string.IsNullOrEmpty(uuid) || !_uuidToId.TryGetValue(uuid, out var requesterId))
            return;

        if (!_clients.TryGetValue(requesterId, out var requesterInfo) || 
            !_clients.TryGetValue(targetId, out var targetInfo))
            return;

        // 取消超时定时器
        var requestKey = (requesterId, targetId);
        if (_pendingRequests.TryRemove(requestKey, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        _logger.LogInformation("Connection cancelled: {RequesterId} -x-> {TargetId}", requesterId, targetId);
        
        // 通知被请求者请求被取消
        await Clients.Client(targetInfo.ConnectionId).SendAsync("ConnectionCancelled", requesterId);
        
        // 通知请求者请求已取消
        await Clients.Client(requesterInfo.ConnectionId).SendAsync("ConnectionCancelled", targetId);
    }

    public async Task DisconnectPeer(int peerId)
    {
        var uuid = Context.GetHttpContext()?.Request.Headers["X-Client-UUID"].ToString();
        if (string.IsNullOrEmpty(uuid) || !_uuidToId.TryGetValue(uuid, out var requesterId))
            return;

        if (!_clients.TryGetValue(requesterId, out var requesterInfo) || 
            !_clients.TryGetValue(peerId, out var peerInfo))
            return;

        if (!_connections.TryGetValue(requesterId, out var requesterConnections) ||
            !requesterConnections.Contains(peerId))
        {
            _logger.LogWarning("Disconnect attempt for non-existent connection: {RequesterId} -/-> {PeerId}", 
                requesterId, peerId);
            return;
        }

        _logger.LogInformation("Connection disconnected: {RequesterId} -/-> {PeerId}", requesterId, peerId);

        requesterConnections.Remove(peerId);
        if (requesterConnections.Count == 0)
            _connections.TryRemove(requesterId, out _);

        if (_connections.TryGetValue(peerId, out var peerConnections))
        {
            peerConnections.Remove(requesterId);
            if (peerConnections.Count == 0)
                _connections.TryRemove(peerId, out _);
        }

        await Clients.Client(peerInfo.ConnectionId).SendAsync("PeerDisconnected", requesterId, requesterInfo.IpAddress);
    }

    private int GenerateUniqueId()
    {
        int id;
        do
        {
            id = _random.Next(MIN_CLIENT_ID, MAX_CLIENT_ID + 1);
        } while (_clients.ContainsKey(id));

        return id;
    }

    public async Task CleanupInactiveClients(TimeSpan timeout)
    {
        var cutoffTime = DateTime.UtcNow - timeout;
        var inactiveClients = _clients.Where(kvp => kvp.Value.LastHeartbeat < cutoffTime).ToList();

        foreach (var kvp in inactiveClients)
        {
            var clientId = kvp.Key;
            var clientInfo = kvp.Value;
            
            _logger.LogInformation("Cleaning up inactive client: ID={ClientId}, IP={Ip}", clientId, clientInfo.IpAddress);

            if (_connections.TryRemove(clientId, out var connectedClients))
            {
                foreach (var connectedClientId in connectedClients)
                {
                    if (_connections.TryGetValue(connectedClientId, out var otherConnections))
                    {
                        otherConnections.Remove(clientId);
                    }

                    if (_clients.TryGetValue(connectedClientId, out var connectedClientInfo))
                    {
                        _logger.LogInformation("Notifying client {ClientId} about disconnection of inactive {DisconnectedId}", 
                            connectedClientId, clientId);
                        await _hubContext.Clients.Client(connectedClientInfo.ConnectionId)
                            .SendAsync("PeerDisconnected", clientId, clientInfo.IpAddress);
                    }
                }
            }

            _clients.TryRemove(clientId, out _);
            _uuidToId.TryRemove(clientInfo.Uuid, out _);
        }
    }
}