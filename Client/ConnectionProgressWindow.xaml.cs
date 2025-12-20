using Client.Services;
using Client.ViewModels;
using System.Windows;
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

        StartConnectionProcess();
    }

    private async void StartConnectionProcess()
    {
        TargetIdTextBox.IsEnabled = false;
        StartButton.IsEnabled = false;
        CancelButton.IsEnabled = true;

        UpdateStep(1, "✅", "#4CAF50", "已输入: " + _targetId);

        UpdateStep(2, "⏳", "#FFA500", "正在发送连接请求...");
        await _signalRService.RequestConnectionAsync(_targetId);
        UpdateStep(2, "✅", "#4CAF50", "连接请求已发送");

        UpdateStep(3, "⏳", "#FFA500", "等待对方确认...");
    }

    public void UpdateStep(int stepNumber, string icon, string color, string detail)
    {
        Dispatcher.Invoke(() =>
        {
            switch (stepNumber)
            {
                case 1:
                    Step1Icon.Text = icon;
                    Step1Icon.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
                    Step1Detail.Text = detail;
                    break;
                case 2:
                    Step2Icon.Text = icon;
                    Step2Icon.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
                    Step2Detail.Text = detail;
                    break;
                case 3:
                    Step3Icon.Text = icon;
                    Step3Icon.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
                    Step3Detail.Text = detail;
                    break;
                case 4:
                    Step4Icon.Text = icon;
                    Step4Icon.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
                    Step4Detail.Text = detail;
                    break;
                case 5:
                    Step5Icon.Text = icon;
                    Step5Icon.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
                    Step5Detail.Text = detail;
                    break;
                case 6:
                    Step6Icon.Text = icon;
                    Step6Icon.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
                    Step6Detail.Text = detail;
                    break;
                case 7:
                    Step7Icon.Text = icon;
                    Step7Icon.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
                    Step7Detail.Text = detail;
                    break;
            }
        });
    }

    public void UpdateFirewallStep(bool success, string message)
    {
        UpdateStep(4, success ? "✅" : "❌", success ? "#4CAF50" : "#F44336", message);
    }

    public void UpdateRouteStep(bool success, string message)
    {
        UpdateStep(5, success ? "✅" : "⚠️", success ? "#4CAF50" : "#FF9800", message);
    }

    public void UpdatePingStep(bool success, string message)
    {
        UpdateStep(6, success ? "✅" : "❌", success ? "#4CAF50" : "#F44336", message);
    }

    public void CompleteConnection(bool success, string message)
    {
        UpdateStep(7, success ? "✅" : "❌", success ? "#4CAF50" : "#F44336", message);
        CancelButton.IsEnabled = false;
        if (success)
        {
            CloseButton.Content = "完成";
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
