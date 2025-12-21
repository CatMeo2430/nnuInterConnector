using Client.Controls;
using Client.Models;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace Client.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly SignalRService _signalRService;
    private ConnectionProgressWindow? _currentProgressWindow;
    private readonly ConcurrentDictionary<int, Controls.CustomDialog> _pendingDialogs = new();

    [ObservableProperty]
    private string _myId = "未连接";

    [ObservableProperty]
    private string _myIp = "检测中...";
    
    [ObservableProperty]
    private ConnectionMode _connectionMode = ConnectionMode.Manual;

    public ObservableCollection<ConnectionInfo> Connections => _signalRService.Connections;

    private void SafeCopyToClipboard(string text, string itemName)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            Controls.CustomDialog.Show("错误", $"复制{itemName}失败: {ex.Message}", false);
        }
    }

    [RelayCommand]
    private void CopyPeerId(object? parameter)
    {
        if (parameter is int peerId)
        {
            SafeCopyToClipboard(peerId.ToString(), "ID");
        }
    }

    [RelayCommand]
    private void CopyPeerIp(object? parameter)
    {
        if (parameter is string peerIp)
        {
            SafeCopyToClipboard(peerIp, "IP");
        }
    }

    [RelayCommand]
    private void CopyStatus(object? parameter)
    {
        if (parameter is string status)
        {
            SafeCopyToClipboard(status, "状态");
        }
    }

    [RelayCommand]
    private void CopyTime(object? parameter)
    {
        if (parameter is DateTime time)
        {
            var timeString = time.ToString("yyyy-MM-dd HH:mm:ss");
            SafeCopyToClipboard(timeString, "时间");
        }
    }

    public MainViewModel()
    {
        _signalRService = new SignalRService();
        _signalRService.RegistrationSuccess += OnRegistrationSuccess;
        _signalRService.ConnectionRequestReceived += OnConnectionRequestReceived;
        _signalRService.ConnectionEstablished += OnConnectionEstablished;
        _signalRService.ConnectionRejected += OnConnectionRejected;
        _signalRService.ConnectionTimeout += OnConnectionTimeout;
        _signalRService.ConnectionCancelled += OnConnectionCancelled;
        
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await _signalRService.InitializeAsync();
        MyIp = _signalRService.IpAddress;
    }

    private void OnRegistrationSuccess(object? sender, int id)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            MyId = id.ToString();
        });
    }

    private void OnConnectionRequestReceived(object? sender, (int, string) e)
    {
        var (requesterId, requesterIp) = e;
        
        _ = Task.Run(async () =>
        {
            switch (ConnectionMode)
            {
                case ConnectionMode.Manual:
                    var dialog = await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var dlg = Controls.CustomDialog.Show(
                            "连接请求",
                            $"收到来自 ID {requesterId} (IP: {requesterIp}) 的连接请求\n是否接受？"
                        );
                        _pendingDialogs.TryAdd(requesterId, dlg);
                        return dlg;
                    });

                    var result = await dialog.ResultTask;
                    _pendingDialogs.TryRemove(requesterId, out _);

                    if (result == true)
                    {
                        await _signalRService.AcceptConnectionAsync(requesterId);
                    }
                    else
                    {
                        await _signalRService.RejectConnectionAsync(requesterId);
                    }
                    break;
                    
                case ConnectionMode.AutoAccept:
                    await _signalRService.AcceptConnectionAsync(requesterId);
                    break;
                    
                case ConnectionMode.AutoReject:
                    await _signalRService.RejectConnectionAsync(requesterId);
                    break;
            }
        });
    }

    private void OnConnectionEstablished(object? sender, (int, string) e)
    {
        var (peerId, peerIp) = e;
        
        Application.Current.Dispatcher.Invoke(() =>
        {
            _currentProgressWindow?.Close();
            _currentProgressWindow = null;
        });
    }

    private void OnConnectionTimeout(object? sender, int requesterId)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_pendingDialogs.TryRemove(requesterId, out var dialog))
            {
                dialog.Close();
            }
        });
    }

    private void OnConnectionCancelled(object? sender, int requesterId)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_pendingDialogs.TryRemove(requesterId, out var dialog))
            {
                dialog.Close();
            }
        });
    }

    private void OnConnectionRejected(object? sender, int e)
    {
        var progressWindow = _currentProgressWindow;
        _currentProgressWindow = null;
        
        Application.Current.Dispatcher.Invoke(() =>
        {
            Controls.CustomDialog.ShowModal("请求被拒绝", $"ID {e} 拒绝了您的连接请求", false);
            progressWindow?.Close();
        });
    }

    [RelayCommand]
    private void InitiateConnection()
    {
        _currentProgressWindow = new ConnectionProgressWindow(_signalRService, this);
        _currentProgressWindow.Closed += (s, e) => _currentProgressWindow = null;
        _currentProgressWindow.Show();
    }

    [RelayCommand]
    private async Task Disconnect(object? parameter)
    {
        if (parameter is ConnectionInfo connection)
        {
            var result = Controls.CustomDialog.ShowModal(
                "确认断开",
                $"确定要断开与 ID {connection.PeerId} 的连接吗？\n这将删除防火墙规则和路由配置。"
            );

            if (result == true)
            {
                await _signalRService.DisconnectPeerAsync(connection);
            }
        }
    }

    [RelayCommand]
    private void CopyId()
    {
        if (MyId != "未连接" && !string.IsNullOrEmpty(MyId))
        {
            try
            {
                Clipboard.SetText(MyId);
            }
            catch (Exception ex)
            {
                Controls.CustomDialog.Show("错误", $"复制ID失败: {ex.Message}", false);
            }
        }
    }

    [RelayCommand]
    private void CopyIp()
    {
        if (!string.IsNullOrEmpty(MyIp) && MyIp != "检测中...")
        {
            try
            {
                Clipboard.SetText(MyIp);
            }
            catch (Exception ex)
            {
                Controls.CustomDialog.Show("错误", $"复制IP失败: {ex.Message}", false);
            }
        }
    }

    public async Task CleanupAsync()
    {
        await _signalRService.DisposeAsync();
    }
}
