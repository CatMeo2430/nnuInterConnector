using Client.Models;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace Client.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly SignalRService _signalRService;

    [ObservableProperty]
    private string _myId = "未连接";

    [ObservableProperty]
    private string _myIp = "检测中...";

    [ObservableProperty]
    private string _targetId = string.Empty;

    [ObservableProperty]
    private string _logText = string.Empty;

    [ObservableProperty]
    private ConnectionInfo? _selectedConnection;

    public ObservableCollection<ConnectionInfo> Connections => _signalRService.Connections;

    public MainViewModel()
    {
        _signalRService = new SignalRService();
        _signalRService.LogMessage += OnLogMessage;
        _signalRService.RegistrationSuccess += OnRegistrationSuccess;
        _signalRService.ConnectionRequestReceived += OnConnectionRequestReceived;
        _signalRService.ConnectionEstablished += OnConnectionEstablished;
        _signalRService.ConnectionRejected += OnConnectionRejected;

        LogMessage("NNU InterConnector 客户端启动");
        
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await _signalRService.InitializeAsync();
        MyIp = _signalRService.IpAddress;
    }

    private void OnLogMessage(object? sender, string message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LogText += message + "\n";
        }, System.Windows.Threading.DispatcherPriority.Normal);
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
        
        Application.Current.Dispatcher.Invoke(async () =>
        {
            var result = MessageBox.Show(
                $"收到来自 ID {requesterId} (IP: {requesterIp}) 的连接请求\n\n是否接受？",
                "连接请求",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                await _signalRService.AcceptConnectionAsync(requesterId);
                LogMessage($"已接受 ID {requesterId} 的连接请求");
            }
            else
            {
                await _signalRService.RejectConnectionAsync(requesterId);
                LogMessage($"已拒绝 ID {requesterId} 的连接请求");
            }
        });
    }

    private void OnConnectionEstablished(object? sender, (int, string) e)
    {
        var (peerId, peerIp) = e;
        LogMessage($"与 ID {peerId} 的连接已建立");
    }

    private void OnConnectionRejected(object? sender, int e)
    {
        LogMessage($"ID {e} 拒绝了您的连接请求");
        MessageBox.Show($"ID {e} 拒绝了您的连接请求", "连接失败", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private async Task ConnectToPeer()
    {
        if (string.IsNullOrWhiteSpace(TargetId))
        {
            MessageBox.Show("请输入目标ID", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(TargetId, out var targetId))
        {
            MessageBox.Show("ID必须是数字", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (targetId.ToString() == MyId)
        {
            MessageBox.Show("不能连接到自己", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        await _signalRService.RequestConnectionAsync(targetId);
        TargetId = string.Empty;
    }

    [RelayCommand]
    private async Task DisconnectSelected()
    {
        if (SelectedConnection == null)
        {
            MessageBox.Show("请选择要断开的连接", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"确定要断开与 ID {SelectedConnection.PeerId} 的连接吗？\n这将删除防火墙规则和路由配置。",
            "确认断开",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );

        if (result == MessageBoxResult.Yes)
        {
            await _signalRService.DisconnectPeerAsync(SelectedConnection);
            SelectedConnection = null;
        }
    }

    private void LogMessage(string message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LogText += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
        });
    }

    public async Task CleanupAsync()
    {
        await _signalRService.DisposeAsync();
    }
}