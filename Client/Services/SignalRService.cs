using Client.Models;
using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.ObjectModel;
using System.Net.Http;

namespace Client.Services;

public class SignalRService
{
    private HubConnection? _connection;
    private readonly string _serverUrl = "http://120.55.67.157:8081";
    private string _uuid = string.Empty;
    private string _ipAddress = string.Empty;
    private System.Timers.Timer? _heartbeatTimer;

    public ObservableCollection<ConnectionInfo> Connections { get; } = new();
    public event EventHandler<string>? LogMessage;
    public event EventHandler<int>? RegistrationSuccess;
    public event EventHandler<(int, string)>? ConnectionRequestReceived;
    public event EventHandler<(int, string)>? ConnectionEstablished;
    public event EventHandler<int>? ConnectionRejected;

    public int? ClientId { get; private set; }
    public string IpAddress => _ipAddress;
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public async Task InitializeAsync()
    {
        _uuid = Guid.NewGuid().ToString();
        _ipAddress = NetworkService.GetCampusNetworkIp();

        if (string.IsNullOrEmpty(_ipAddress))
        {
            OnLogMessage("未检测到校园网IP地址 (10.20.x.x 或 10.30.x.x)");
            return;
        }

        OnLogMessage($"检测到校园网IP: {_ipAddress}");
        OnLogMessage($"生成客户端UUID: {_uuid}");

        await RegisterWithHttpAsync();
        await ConnectToSignalRAsync();
    }

    private async Task RegisterWithHttpAsync()
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("X-Client-UUID", _uuid);
            httpClient.DefaultRequestHeaders.Add("X-Client-IP", _ipAddress);
            
            var response = await httpClient.PostAsync("http://120.55.67.157:8080/api/Registration", null);
            
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
                Status = "已连接",
                ConnectedTime = DateTime.Now
            };
            
            App.Current.Dispatcher.Invoke(() =>
            {
                Connections.Add(connection);
            });
            
            ConnectionEstablished?.Invoke(this, (peerId, peerIp));
            
            _ = Task.Run(async () =>
            {
                await SetupPeerConnectionAsync(peerId, peerIp);
            });
        });

        _connection.On<int>("ConnectionFailed", message =>
        {
            OnLogMessage($"连接失败: {message}");
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
                await DisconnectPeerAsync(connectionToRemove);
            }
        });

        _connection.Closed += async (error) =>
        {
            OnLogMessage($"WebSocket连接断开: {(error?.Message ?? "未知原因")}");
            StopHeartbeat();
            await Task.Delay(new Random().Next(0, 5) * 1000);
            await ConnectToSignalRAsync();
        };

        try
        {
            await _connection.StartAsync();
            OnLogMessage("WebSocket连接成功");
            OnLogMessage("正在注册客户端...");
            
            await _connection.InvokeAsync("RegisterClient", _uuid);
        }
        catch (Exception ex)
        {
            OnLogMessage($"WebSocket连接失败: {ex.Message}");
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

    private async Task SetupPeerConnectionAsync(int peerId, string peerIp)
    {
        try
        {
            var helperPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Helper.exe");
            if (!System.IO.File.Exists(helperPath))
            {
                OnLogMessage("错误: Helper.exe 未找到");
                return;
            }

            var arguments = $"add {peerIp}";
            
            if (NetworkService.IsSameSubnet(_ipAddress, peerIp))
            {
                var gateway = NetworkService.GetGatewayForIp(_ipAddress);
                if (!string.IsNullOrEmpty(gateway))
                {
                    arguments += $" {gateway}";
                    OnLogMessage($"同网段连接，将配置强制路由 (网关: {gateway})");
                }
            }
            else
            {
                OnLogMessage("跨网段连接，无需额外配置");
            }

            var processStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = helperPath,
                Arguments = arguments,
                Verb = "runas",
                UseShellExecute = true,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(processStartInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                if (process.ExitCode == 0)
                {
                    OnLogMessage($"Helper配置成功，防火墙已放行 {peerIp}");
                }
                else
                {
                    OnLogMessage($"Helper配置失败，退出码: {process.ExitCode}");
                }
            }
        }
        catch (Exception ex)
        {
            OnLogMessage($"配置连接失败: {ex.Message}");
        }
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

    public async Task DisconnectPeerAsync(ConnectionInfo connection)
    {
        try
        {
            if (_connection?.State == HubConnectionState.Connected)
            {
                OnLogMessage($"正在通知服务器断开与ID {connection.PeerId} 的连接...");
                await _connection.InvokeAsync("DisconnectPeer", connection.PeerId);
            }

            var helperPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Helper.exe");
            if (System.IO.File.Exists(helperPath))
            {
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = helperPath,
                    Arguments = $"remove {connection.PeerIp}",
                    Verb = "runas",
                    UseShellExecute = true,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processStartInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    if (process.ExitCode == 0)
                    {
                        OnLogMessage($"已删除 {connection.PeerIp} 的防火墙规则和路由配置");
                    }
                }
            }

            App.Current.Dispatcher.Invoke(() =>
            {
                Connections.Remove(connection);
            });

            OnLogMessage($"已断开与ID {connection.PeerId} 的连接");
        }
        catch (Exception ex)
        {
            OnLogMessage($"断开连接失败: {ex.Message}");
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