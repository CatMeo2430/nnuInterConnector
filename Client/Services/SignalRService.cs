using Client.Controls;
using Client.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace Client.Services;

public class SignalRService
{
    private HubConnection? _connection;
    private string _serverUrl = string.Empty;
    private string _uuid = string.Empty;
    private string _ipAddress = string.Empty;
    private System.Timers.Timer? _heartbeatTimer;
    private readonly string _serverIpAddress;
    private readonly int _httpPort;
    private readonly int _webSocketPort;
    private const int HeartbeatIntervalMs = 30000;
    private const int NetworkErrorDelayMs = 3000;
    
    public SignalRService()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        
        var serverConfig = configuration.GetSection("ServerConfig");
        _serverIpAddress = serverConfig["IpAddress"] ?? throw new InvalidOperationException("ServerConfig:IpAddress not configured");
        _httpPort = int.Parse(serverConfig["HttpPort"] ?? "8080");
        _webSocketPort = int.Parse(serverConfig["WebSocketPort"] ?? "8081");
        
        _serverUrl = $"http://{_serverIpAddress}:{_webSocketPort}";
    }

    public ObservableCollection<ConnectionInfo> Connections { get; } = new();
    public event EventHandler<int>? RegistrationSuccess;
    public event EventHandler<(int, string)>? ConnectionRequestReceived;
    public event EventHandler<(int, string)>? ConnectionEstablished;
    public event EventHandler<int>? ConnectionRejected;
    public event EventHandler<int>? ConnectionFailed;
    public event EventHandler<int>? ConnectionTimeout;
    public event EventHandler<int>? ConnectionCancelled;

    public int? ClientId { get; private set; }
    public string IpAddress => _ipAddress;
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public async Task InitializeAsync()
    {
        try
        {
            _uuid = Guid.NewGuid().ToString();
            
            _ipAddress = NetworkService.GetCampusNetworkIp();

            if (string.IsNullOrEmpty(_ipAddress))
            {
                await ShowErrorAndExitAsync("网络错误", "请在NNU仙林校区校园网内使用");
                return;
            }

            if (_ipAddress.StartsWith("10.20."))
            {
                RouteService.AddRoute(_serverIpAddress, "10.20.0.1");
            }

            await RegisterWithHttpAsync();
            await ConnectToSignalRAsync();
        }
        catch (Exception ex)
        {
            ShowErrorModal("初始化失败", $"初始化失败: {ex.Message}\n请检查网络连接和服务器配置");
        }
    }

    private async Task ShowErrorAndExitAsync(string title, string message)
    {
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            Controls.CustomDialog.ShowModal(title, message, false);
        });
        await Task.Delay(NetworkErrorDelayMs);
        Application.Current.Shutdown(1);
    }

    private void ShowErrorModal(string title, string message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Controls.CustomDialog.ShowModal(title, message, false);
        });
    }

    private async Task RegisterWithHttpAsync()
    {
        try
        {
            var httpUrl = $"http://{_serverIpAddress}:{_httpPort}/api/Registration";
            
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("X-Client-UUID", _uuid);
            httpClient.DefaultRequestHeaders.Add("X-Client-IP", _ipAddress);
            
            var response = await httpClient.PostAsync(httpUrl, null);
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Controls.CustomDialog.ShowModal("HTTP注册异常", $"HTTP注册异常: {ex.Message}", false);
            });
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
            RegistrationSuccess?.Invoke(this, id);
            StartHeartbeat();
        });

        _connection.On<int, string>("ConnectionRequest", (requesterId, requesterIp) =>
        {
            ConnectionRequestReceived?.Invoke(this, (requesterId, requesterIp));
        });

        _connection.On<int, string>("ConnectionEstablished", (peerId, peerIp) =>
        {
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

        _connection.On<int>("ConnectionFailed", errorCode =>
        {
            ConnectionFailed?.Invoke(this, errorCode);
        });

        _connection.On<int>("ConnectionRejected", rejecterId =>
        {
            ConnectionRejected?.Invoke(this, rejecterId);
        });

        _connection.On<int>("ConnectionTimeout", requesterId =>
        {
            ConnectionTimeout?.Invoke(this, requesterId);
        });

        _connection.On<int>("ConnectionCancelled", requesterId =>
        {
            ConnectionCancelled?.Invoke(this, requesterId);
        });

        _connection.On<int, string>("PeerDisconnected", async (peerId, peerIp) =>
        {
            var connectionToRemove = Connections.FirstOrDefault(c => c.PeerId == peerId);
            if (connectionToRemove != null)
            {
                await CleanupPeerConnectionAsync(connectionToRemove);
            }
        });

        _connection.Closed += async (error) =>
        {
            StopHeartbeat();
            await Task.Delay(5000);
            await ConnectToSignalRAsync();
        };

        try
        {
            await _connection.StartAsync();
            await _connection.InvokeAsync("RegisterClient", _uuid);
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            ShowErrorModal("连接失败", $"无法连接到服务器 {new Uri(_serverUrl).Host}\n请检查：1. 网络连接 2. 服务器地址配置 3. 服务器是否运行\n详细错误: {ex.Message}");
        }
        catch (Exception ex)
        {
            ShowErrorModal("连接失败", $"WebSocket连接失败: {ex.Message}\n请检查服务器配置和网络连接");
        }
    }

    private void StartHeartbeat()
    {
        _heartbeatTimer = new System.Timers.Timer(HeartbeatIntervalMs);
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
                    ShowErrorModal("心跳更新失败", $"心跳更新失败: {ex.Message}");
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
                }

                if (!FirewallService.AddFirewallRule(peerIp))
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(gateway) && !RouteService.AddRoute(peerIp, gateway))
                {
                    FirewallService.RemoveFirewallRule(peerIp);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                ShowErrorModal("配置失败", $"配置连接失败: {ex.Message}\n请重试或检查系统配置");
                return false;
            }
        });
    }

    public async Task RequestConnectionAsync(int targetId)
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            await _connection.InvokeAsync("RequestConnection", targetId);
        }
    }

    public async Task AcceptConnectionAsync(int requesterId)
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            await _connection.InvokeAsync("AcceptConnection", requesterId);
        }
    }

    public async Task RejectConnectionAsync(int requesterId)
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            await _connection.InvokeAsync("RejectConnection", requesterId);
        }
    }

    public async Task CancelConnectionAsync(int targetId)
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            await _connection.InvokeAsync("CancelConnection", targetId);
        }
    }

    public async Task CleanupPeerConnectionAsync(ConnectionInfo connection)
    {
        await Task.Run(() =>
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    connection.Status = "断开中";
                });

                FirewallService.RemoveFirewallRule(connection.PeerIp);
                RouteService.RemoveRoute(connection.PeerIp);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Connections.Remove(connection);
                });
            }
            catch (Exception ex)
            {
                ShowErrorModal("清理失败", $"清理配置失败: {ex.Message}");
                Application.Current.Dispatcher.Invoke(() =>
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
            Application.Current.Dispatcher.Invoke(() =>
            {
                connection.Status = "断开中";
            });
            
            if (_connection?.State == HubConnectionState.Connected)
            {
                await _connection.InvokeAsync("DisconnectPeer", connection.PeerId);
            }

            FirewallService.RemoveFirewallRule(connection.PeerIp);
            RouteService.RemoveRoute(connection.PeerIp);

            Application.Current.Dispatcher.Invoke(() =>
            {
                Connections.Remove(connection);
            });
        }
        catch (Exception ex)
        {
            ShowErrorModal("断开失败", $"断开连接失败: {ex.Message}");
            Application.Current.Dispatcher.Invoke(() =>
            {
                connection.Status = "错误";
            });
        }
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