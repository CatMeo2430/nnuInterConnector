using Client.Models;
using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.ObjectModel;
using System.Net.Http;

namespace Client.Services;

public class SignalRService
{
    private HubConnection? _connection;
    private string _serverUrl = string.Empty;
    private string _uuid = string.Empty;
    private string _ipAddress = string.Empty;
    private System.Timers.Timer? _heartbeatTimer;

    private const string ServerIpAddress = "10.20.214.145";
    private const int HttpPort = 8080;
    private const int WebSocketPort = 8081;
    
    public SignalRService()
    {
        _serverUrl = $"http://{ServerIpAddress}:{WebSocketPort}";
    }

    public ObservableCollection<ConnectionInfo> Connections { get; } = new();
    public event EventHandler<string>? LogMessage;
    public event EventHandler<int>? RegistrationSuccess;
    public event EventHandler<(int, string)>? ConnectionRequestReceived;
    public event EventHandler<(int, string)>? ConnectionEstablished;
    public event EventHandler<int>? ConnectionRejected;
    public event EventHandler<int>? ConnectionFailed;

    public int? ClientId { get; private set; }
    public string IpAddress => _ipAddress;
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public async Task InitializeAsync()
    {
        try
        {
            _uuid = Guid.NewGuid().ToString();
            
            OnLogMessage("正在检测网络环境...");
            _ipAddress = NetworkService.GetCampusNetworkIp();

            if (string.IsNullOrEmpty(_ipAddress))
            {
                OnLogMessage("❌ 错误：未检测到校园网IP地址");
                OnLogMessage("请确保已连接到NNU校园网WiFi或有线网络");
                OnLogMessage("程序将退出");
                
                await Task.Delay(2000);
                Environment.Exit(1);
                return;
            }

            OnLogMessage($"✅ 检测到校园网IP: {_ipAddress}");
            
            if (_ipAddress.StartsWith("10.20."))
            {
                OnLogMessage("检测到10.20网段，正在配置服务器路由...");
                
                var routeSuccess = RouteService.AddRoute(ServerIpAddress, "10.20.0.1");
                if (routeSuccess)
                {
                    OnLogMessage($"✅ 服务器路由已配置: {ServerIpAddress} -> 10.20.0.1");
                }
                else
                {
                    OnLogMessage($"⚠️ 服务器路由配置失败，可能影响连接");
                }
            }
            
            OnLogMessage($"生成客户端UUID: {_uuid}");

            await RegisterWithHttpAsync();
            await ConnectToSignalRAsync();
        }
        catch (Exception ex)
        {
            OnLogMessage($"❌ 初始化失败: {ex.Message}");
            OnLogMessage("请检查网络连接和服务器配置");
        }
    }

    private async Task RegisterWithHttpAsync()
    {
        try
        {
            var httpUrl = $"http://{ServerIpAddress}:{HttpPort}/api/Registration";
            
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("X-Client-UUID", _uuid);
            httpClient.DefaultRequestHeaders.Add("X-Client-IP", _ipAddress);
            
            var response = await httpClient.PostAsync(httpUrl, null);
            
            if (response.IsSuccessStatusCode)
            {
                OnLogMessage("HTTP注册成功，等待WebSocket连接...");
            }
            else
            {
                OnLogMessage($"HTTP注册失败: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            OnLogMessage($"HTTP注册异常: {ex.Message}");
        }
    }

    private async Task ConnectToSignalRAsync()
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(_serverUrl + "/interconnectionHub", options =>
            {
                options.Headers["X-Client-UUID"] = _uuid;
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<int>("RegistrationSuccess", id =>
        {
            ClientId = id;
            OnLogMessage($"注册成功！您的ID: {id}");
            RegistrationSuccess?.Invoke(this, id);
            StartHeartbeat();
        });

        _connection.On<int, string>("ConnectionRequest", (requesterId, requesterIp) =>
        {
            OnLogMessage($"收到来自ID {requesterId} 的连接请求");
            ConnectionRequestReceived?.Invoke(this, (requesterId, requesterIp));
        });

        _connection.On<int, string>("ConnectionEstablished", (peerId, peerIp) =>
        {
            OnLogMessage($"与ID {peerId} 建立连接，IP: {peerIp}");
            
            var connection = new ConnectionInfo
            {
                PeerId = peerId,
                PeerIp = peerIp,
                Status = "连接中",
                ConnectedTime = DateTime.Now
            };
            
            App.Current.Dispatcher.Invoke(() =>
            {
                Connections.Add(connection);
            });
            
            ConnectionEstablished?.Invoke(this, (peerId, peerIp));
            
            _ = Task.Run(async () =>
            {
                var success = await SetupPeerConnectionAsync(peerId, peerIp);
                
                App.Current.Dispatcher.Invoke(() =>
                {
                    connection.Status = success ? "已连接" : "配置失败";
                });
            });
        });

        _connection.On<int>("ConnectionFailed", message =>
        {
            OnLogMessage($"连接失败: {message}");
            ConnectionFailed?.Invoke(this, message);
        });

        _connection.On<int>("ConnectionRejected", rejecterId =>
        {
            OnLogMessage($"ID {rejecterId} 拒绝了您的连接请求");
            ConnectionRejected?.Invoke(this, rejecterId);
        });

        _connection.On<int, string>("PeerDisconnected", async (peerId, peerIp) =>
        {
            OnLogMessage($"互联的客户端 ID {peerId} (IP: {peerIp}) 已断开连接");
            
            var connectionToRemove = Connections.FirstOrDefault(c => c.PeerId == peerId);
            if (connectionToRemove != null)
            {
                await CleanupPeerConnectionAsync(connectionToRemove);
            }
        });

        _connection.Closed += async (error) =>
        {
            if (error != null)
            {
                OnLogMessage($"WebSocket连接断开: {error.Message}");
                if (error.InnerException != null)
                {
                    OnLogMessage($"内部错误: {error.InnerException.Message}");
                }
            }
            else
            {
                OnLogMessage("WebSocket连接已断开");
            }
            
            StopHeartbeat();
            OnLogMessage("5秒后尝试重新连接...");
            await Task.Delay(5000);
            await ConnectToSignalRAsync();
        };

        try
        {
            await _connection.StartAsync();
            OnLogMessage("WebSocket连接成功");
            OnLogMessage("正在注册客户端...");
            
            await _connection.InvokeAsync("RegisterClient", _uuid);
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            OnLogMessage($"WebSocket连接失败: 无法连接到服务器 {new Uri(_serverUrl).Host}");
            OnLogMessage("请检查：1. 网络连接 2. 服务器地址配置 3. 服务器是否运行");
            OnLogMessage($"详细错误: {ex.Message}");
        }
        catch (Exception ex)
        {
            OnLogMessage($"WebSocket连接失败: {ex.Message}");
            OnLogMessage("请检查服务器配置和网络连接");
        }
    }

    private void StartHeartbeat()
    {
        _heartbeatTimer = new System.Timers.Timer(30000);
        _heartbeatTimer.Elapsed += async (sender, e) =>
        {
            if (_connection?.State == HubConnectionState.Connected)
            {
                try
                {
                    await _connection.InvokeAsync("UpdateHeartbeat");
                }
                catch (Exception ex)
                {
                    OnLogMessage($"心跳更新失败: {ex.Message}");
                }
            }
        };
        _heartbeatTimer.Start();
    }

    private void StopHeartbeat()
    {
        _heartbeatTimer?.Stop();
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = null;
    }

    private async Task<bool> SetupPeerConnectionAsync(int peerId, string peerIp)
    {
        return await Task.Run(() =>
        {
            try
            {
                string? gateway = null;
                if (NetworkService.IsSameSubnet(_ipAddress, peerIp))
                {
                    gateway = NetworkService.GetGatewayForIp(_ipAddress);
                    if (!string.IsNullOrEmpty(gateway))
                    {
                        OnLogMessage($"同网段连接，将配置强制路由 (网关: {gateway})");
                    }
                }
                else
                {
                    OnLogMessage("跨网段连接，无需额外配置");
                }

                var firewallSuccess = FirewallService.AddFirewallRule(peerIp);
                if (!firewallSuccess)
                {
                    OnLogMessage($"❌ 防火墙配置失败: {peerIp}");
                    return false;
                }

                OnLogMessage($"✅ 防火墙已放行 IP: {peerIp}");

                if (!string.IsNullOrEmpty(gateway))
                {
                    var routeSuccess = RouteService.AddRoute(peerIp, gateway);
                    if (routeSuccess)
                    {
                        OnLogMessage($"✅ 强制路由已配置: {peerIp} -> {gateway}");
                    }
                    else
                    {
                        OnLogMessage($"⚠️ 强制路由配置失败: {peerIp}");
                        FirewallService.RemoveFirewallRule(peerIp);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                OnLogMessage($"❌ 配置连接失败: {ex.Message}");
                OnLogMessage("请重试或检查系统配置");
                return false;
            }
        });
    }

    public async Task RequestConnectionAsync(int targetId)
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            await _connection.InvokeAsync("RequestConnection", targetId);
            OnLogMessage($"已向ID {targetId} 发送连接请求");
        }
        else
        {
            OnLogMessage("未连接到服务器，无法发送连接请求");
        }
    }

    public async Task AcceptConnectionAsync(int requesterId)
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            await _connection.InvokeAsync("AcceptConnection", requesterId);
            OnLogMessage($"已接受ID {requesterId} 的连接请求");
        }
    }

    public async Task RejectConnectionAsync(int requesterId)
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            await _connection.InvokeAsync("RejectConnection", requesterId);
            OnLogMessage($"已拒绝ID {requesterId} 的连接请求");
        }
    }

    public async Task CleanupPeerConnectionAsync(ConnectionInfo connection)
    {
        await Task.Run(() =>
        {
            try
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    connection.Status = "断开中";
                });
                
                OnLogMessage($"正在清理与ID {connection.PeerId} 的本地配置...");

                var firewallSuccess = FirewallService.RemoveFirewallRule(connection.PeerIp);
                var routeSuccess = RouteService.RemoveRoute(connection.PeerIp);

                if (firewallSuccess || routeSuccess)
                {
                    OnLogMessage($"✅ 已删除 {connection.PeerIp} 的防火墙规则和路由配置");
                }
                else
                {
                    OnLogMessage($"⚠️ 清理配置时遇到问题: {connection.PeerIp}");
                }

                App.Current.Dispatcher.Invoke(() =>
                {
                    Connections.Remove(connection);
                });

                OnLogMessage($"已清理与ID {connection.PeerId} 的本地配置");
            }
            catch (Exception ex)
            {
                OnLogMessage($"❌ 清理配置失败: {ex.Message}");
                App.Current.Dispatcher.Invoke(() =>
                {
                    connection.Status = "错误";
                });
            }
        });
    }

    public async Task DisconnectPeerAsync(ConnectionInfo connection)
    {
        try
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                connection.Status = "断开中";
            });
            
            if (_connection?.State == HubConnectionState.Connected)
            {
                OnLogMessage($"正在通知服务器断开与ID {connection.PeerId} 的连接...");
                await _connection.InvokeAsync("DisconnectPeer", connection.PeerId);
            }

            var firewallSuccess = FirewallService.RemoveFirewallRule(connection.PeerIp);
            var routeSuccess = RouteService.RemoveRoute(connection.PeerIp);

            if (firewallSuccess || routeSuccess)
            {
                OnLogMessage($"✅ 已删除 {connection.PeerIp} 的防火墙规则和路由配置");
            }
            else
            {
                OnLogMessage($"⚠️ 清理配置时遇到问题: {connection.PeerIp}");
            }

            App.Current.Dispatcher.Invoke(() =>
            {
                Connections.Remove(connection);
            });

            OnLogMessage($"已断开与ID {connection.PeerId} 的连接");
        }
        catch (Exception ex)
        {
            OnLogMessage($"❌ 断开连接失败: {ex.Message}");
            App.Current.Dispatcher.Invoke(() =>
            {
                connection.Status = "错误";
            });
        }
    }

    private void OnLogMessage(string message)
    {
        LogMessage?.Invoke(this, $"[{DateTime.Now:HH:mm:ss}] {message}");
    }

    public async ValueTask DisposeAsync()
    {
        StopHeartbeat();
        
        if (_connection != null)
        {
            await _connection.StopAsync();
            await _connection.DisposeAsync();
        }
    }
}