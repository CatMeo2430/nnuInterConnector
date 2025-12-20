using Client.Controls;
using Client.Models;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace Client.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly SignalRService _signalRService;
    private readonly string _logFilePath;
    private ConnectionProgressWindow? _currentProgressWindow;

    [ObservableProperty]
    private string _myId = "未连接";

    [ObservableProperty]
    private string _myIp = "检测中...";
    
    [ObservableProperty]
    private ConnectionMode _connectionMode = ConnectionMode.Manual;

    partial void OnConnectionModeChanged(ConnectionMode value)
    {
        var modeText = value switch
        {
            ConnectionMode.Manual => "手动控制",
            ConnectionMode.AutoAccept => "自动同意",
            ConnectionMode.AutoReject => "自动拒绝",
            _ => "未知"
        };
        LogMessage($"连接模式切换为：{modeText}");
    }

    public ObservableCollection<ConnectionInfo> Connections => _signalRService.Connections;

    [RelayCommand]
    private void CopyPeerId(object? parameter)
    {
        if (parameter is int peerId)
        {
            try
            {
                Clipboard.SetText(peerId.ToString());
                LogMessage($"ID {peerId} 已复制到剪贴板");
            }
            catch (Exception ex)
            {
                LogMessage($"复制ID失败: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private void CopyPeerIp(object? parameter)
    {
        if (parameter is string peerIp)
        {
            try
            {
                Clipboard.SetText(peerIp);
                LogMessage($"IP {peerIp} 已复制到剪贴板");
            }
            catch (Exception ex)
            {
                LogMessage($"复制IP失败: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private void CopyStatus(object? parameter)
    {
        if (parameter is string status)
        {
            try
            {
                Clipboard.SetText(status);
                LogMessage($"状态 '{status}' 已复制到剪贴板");
            }
            catch (Exception ex)
            {
                LogMessage($"复制状态失败: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private void CopyTime(object? parameter)
    {
        if (parameter is DateTime time)
        {
            try
            {
                var timeString = time.ToString("yyyy-MM-dd HH:mm:ss");
                Clipboard.SetText(timeString);
                LogMessage($"时间 {timeString} 已复制到剪贴板");
            }
            catch (Exception ex)
            {
                LogMessage($"复制时间失败: {ex.Message}");
            }
        }
    }

    public MainViewModel()
    {
        _signalRService = new SignalRService();
        _signalRService.LogMessage += OnLogMessage;
        _signalRService.RegistrationSuccess += OnRegistrationSuccess;
        _signalRService.ConnectionRequestReceived += OnConnectionRequestReceived;
        _signalRService.ConnectionEstablished += OnConnectionEstablished;
        _signalRService.ConnectionRejected += OnConnectionRejected;

        // 初始化日志文件
        var logsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        Directory.CreateDirectory(logsDirectory);
        _logFilePath = Path.Combine(logsDirectory, $"{DateTime.Now:yyyy-MM-dd}.log");

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
        LogMessage(message);
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
            switch (ConnectionMode)
            {
                case ConnectionMode.Manual:
                    var result = Controls.CustomDialog.ShowModal(
                        "连接请求",
                        $"收到来自 ID {requesterId} (IP: {requesterIp}) 的连接请求\n是否接受？"
                    );

                    if (result == true)
                    {
                        await _signalRService.AcceptConnectionAsync(requesterId);
                        LogMessage($"✅ 已接受 ID {requesterId} 的连接请求");
                    }
                    else
                    {
                        await _signalRService.RejectConnectionAsync(requesterId);
                        LogMessage($"❌ 已拒绝 ID {requesterId} 的连接请求");
                    }
                    break;
                    
                case ConnectionMode.AutoAccept:
                    await _signalRService.AcceptConnectionAsync(requesterId);
                    LogMessage($"✅ 自动接受 ID {requesterId} 的连接请求");
                    break;
                    
                case ConnectionMode.AutoReject:
                    await _signalRService.RejectConnectionAsync(requesterId);
                    LogMessage($"❌ 自动拒绝 ID {requesterId} 的连接请求");
                    break;
            }
        });
    }

    private void OnConnectionEstablished(object? sender, (int, string) e)
    {
        var (peerId, peerIp) = e;
        LogMessage($"与 ID {peerId} 的连接已建立");
        
        // 关闭当前的连接进度窗口
        Application.Current.Dispatcher.Invoke(() =>
        {
            _currentProgressWindow?.Close();
            _currentProgressWindow = null;
        });
    }

    private void OnConnectionRejected(object? sender, int e)
    {
        LogMessage($"ID {e} 拒绝了您的连接请求");
        
        // 保存当前窗口引用
        var progressWindow = _currentProgressWindow;
        _currentProgressWindow = null;
        
        // 确保在UI线程上显示对话框并关闭窗口
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

    private void LogMessage(string message)
    {
        var logEntry = $"[{DateTime.Now:HH:mm:ss}] {message}";
        
        // 写入日志文件
        try
        {
            File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
        }
        catch (Exception ex)
        {
            // 如果写入文件失败，静默处理（避免无限递归）
            Debug.WriteLine($"日志写入失败: {ex.Message}");
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
                LogMessage("ID已复制到剪贴板");
            }
            catch (Exception ex)
            {
                LogMessage($"复制ID失败: {ex.Message}");
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
                LogMessage("IP已复制到剪贴板");
            }
            catch (Exception ex)
            {
                LogMessage($"复制IP失败: {ex.Message}");
                Controls.CustomDialog.Show("错误", $"复制IP失败: {ex.Message}", false);
            }
        }
    }

    public async Task CleanupAsync()
    {
        await _signalRService.DisposeAsync();
    }
}