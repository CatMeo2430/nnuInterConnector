using Client.Services;
using Client.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Client;

public partial class ConnectionProgressWindow : Window
{
    private int _targetId;
    private readonly SignalRService _signalRService;
    private readonly MainViewModel _mainViewModel;
    private bool _isConnecting = false;
    private TaskCompletionSource<bool> _connectionResult = new TaskCompletionSource<bool>();

    public ConnectionProgressWindow(SignalRService signalRService, MainViewModel mainViewModel)
    {
        InitializeComponent();
        _signalRService = signalRService;
        _mainViewModel = mainViewModel;
        
        // 订阅连接失败事件
        _signalRService.ConnectionFailed += OnConnectionFailed;
        _signalRService.ConnectionRejected += OnConnectionRejected;
        _signalRService.ConnectionEstablished += OnConnectionEstablished;
        
        ResetProgress();
    }

    private void ResetProgress()
    {
        ProgressBar.Value = 0;
        StatusText.Text = "准备就绪";
        TargetIdTextBox.IsEnabled = true;
        StartButton.IsEnabled = true;
        _isConnecting = false;
        _connectionResult = new TaskCompletionSource<bool>();
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TargetIdTextBox.Text))
        {
            MessageBox.Show("请输入对方ID", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(TargetIdTextBox.Text, out _targetId))
        {
            MessageBox.Show("ID必须是数字", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_targetId < 100000 || _targetId > 999999)
        {
            MessageBox.Show("ID必须是6位数字（100000-999999）", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_targetId.ToString() == _mainViewModel.MyId)
        {
            MessageBox.Show("不能连接到自己", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        StartConnection();
    }

    private async void StartConnection()
    {
        _isConnecting = true;
        TargetIdTextBox.IsEnabled = false;
        StartButton.IsEnabled = false;

        try
        {
            // 步骤1: 发送连接请求
            UpdateProgress(0, "正在发送连接请求...");
            await _signalRService.RequestConnectionAsync(_targetId);
            UpdateProgress(20, "连接请求已发送，等待响应...");

            // 步骤2: 等待对方响应（接受或拒绝）
            UpdateProgress(30, "等待对方确认...");
            var connectionSuccess = await WaitForConnectionResponse();
            
            if (!connectionSuccess)
            {
                UpdateProgress(0, "连接失败");
                ResetProgress();
                return;
            }

            // 步骤3: 等待连接建立和配置完成
            UpdateProgress(60, "正在配置网络和防火墙...");
            var setupSuccess = await WaitForSetupCompletion();
            
            if (!setupSuccess)
            {
                UpdateProgress(0, "配置失败");
                StatusText.Text = "配置失败，请检查系统权限";
                await Task.Delay(2000);
                Close();
                return;
            }

            // 步骤4: 完成
            UpdateProgress(100, "连接成功！");
            await Task.Delay(1000);
            Close();
        }
        catch (Exception ex)
        {
            UpdateProgress(0, $"错误: {ex.Message}");
            StatusText.Text = $"发生错误: {ex.Message}";
            await Task.Delay(3000);
            ResetProgress();
        }
    }

    private async Task<bool> WaitForConnectionResponse()
    {
        // 等待连接结果（成功、失败或被拒绝）
        var delayTask = Task.Delay(30000); // 30秒超时
        var completedTask = await Task.WhenAny(_connectionResult.Task, delayTask);
        
        if (completedTask == delayTask)
        {
            // 超时
            return false;
        }
        
        return await _connectionResult.Task;
    }

    private async Task<bool> WaitForSetupCompletion()
    {
        // 等待3秒让SetupPeerConnectionAsync完成
        await Task.Delay(3000);
        
        // 检查是否已连接
        var connection = _signalRService.Connections.FirstOrDefault(c => c.PeerId == _targetId);
        return connection?.Status == "已连接";
    }

    private void OnConnectionFailed(object? sender, int errorCode)
    {
        if (!_isConnecting) return;
        
        string errorMessage = errorCode switch
        {
            1 => "目标ID不存在",
            2 => "目标不在线",
            3 => "连接超时",
            _ => "未知错误"
        };
        
        Dispatcher.BeginInvoke(() =>
        {
            StatusText.Text = $"连接失败: {errorMessage}";
            UpdateProgress(0, "连接失败");
        });
        
        _connectionResult.TrySetResult(false);
    }

    private void OnConnectionRejected(object? sender, int rejecterId)
    {
        if (!_isConnecting) return;
        
        Dispatcher.BeginInvoke(() =>
        {
            StatusText.Text = $"ID {rejecterId} 拒绝了您的连接请求";
            UpdateProgress(0, "连接被拒绝");
        });
        
        _connectionResult.TrySetResult(false);
    }

    private void OnConnectionEstablished(object? sender, (int, string) e)
    {
        if (!_isConnecting) return;
        
        var (peerId, peerIp) = e;
        if (peerId == _targetId)
        {
            Dispatcher.BeginInvoke(() =>
            {
                UpdateProgress(50, "连接已建立，正在配置...");
            });
            
            _connectionResult.TrySetResult(true);
        }
    }

    private void UpdateProgress(double value, string status)
    {
        Dispatcher.BeginInvoke(() =>
        {
            ProgressBar.Value = value;
            StatusText.Text = status;
        }, DispatcherPriority.Render);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // 取消订阅事件
        _signalRService.ConnectionFailed -= OnConnectionFailed;
        _signalRService.ConnectionRejected -= OnConnectionRejected;
        _signalRService.ConnectionEstablished -= OnConnectionEstablished;
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // 取消订阅事件
        _signalRService.ConnectionFailed -= OnConnectionFailed;
        _signalRService.ConnectionRejected -= OnConnectionRejected;
        _signalRService.ConnectionEstablished -= OnConnectionEstablished;
        base.OnClosing(e);
    }
}