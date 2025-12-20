using Client.Services;
using Client.ViewModels;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Client;

public partial class ConnectionProgressWindow : Window
{
    private int _targetId;
    private readonly SignalRService _signalRService;
    private readonly MainViewModel _mainViewModel;

    public ConnectionProgressWindow(SignalRService signalRService, MainViewModel mainViewModel)
    {
        InitializeComponent();
        _signalRService = signalRService;
        _mainViewModel = mainViewModel;
        ResetProgress();
    }

    private void ResetProgress()
    {
        ProgressBar.Value = 0;
        StatusText.Text = "准备就绪";
        TargetIdTextBox.IsEnabled = true;
        StartButton.IsEnabled = true;
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
        TargetIdTextBox.IsEnabled = false;
        StartButton.IsEnabled = false;

        UpdateProgress(0, "正在初始化...");
        await Task.Delay(300);

        UpdateProgress(15, "正在发送连接请求...");
        await _signalRService.RequestConnectionAsync(_targetId);
        await Task.Delay(500);

        UpdateProgress(30, "等待对方确认...");
        await Task.Delay(2000);

        UpdateProgress(50, "正在配置防火墙规则...");
        await Task.Delay(1000);

        UpdateProgress(70, "正在配置强制路由...");
        await Task.Delay(1000);

        UpdateProgress(85, "正在测试连接...");
        await Task.Delay(1000);

        UpdateProgress(100, "连接成功！");
        await Task.Delay(500);
        
        Close();
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
        Close();
    }
}