using System;
using System.Windows;
using System.Threading.Tasks;
using Client.ViewModels;
using Client.Controls;

namespace Client;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        try
        {
            var mainWindow = new MainWindow();
            
            // 异步初始化ViewModel，不阻塞UI
            var viewModel = new MainViewModel();
            
            // 立即显示主窗口，ViewModel在后台初始化
            mainWindow.DataContext = viewModel;
            mainWindow.Show();
            
            // 等待初始化完成（最多1秒）
            var initTask = viewModel.WaitForInitializationAsync();
            var timeoutTask = Task.Delay(1000);
            
            var completedTask = await Task.WhenAny(initTask, timeoutTask);
            if (completedTask == timeoutTask)
            {
                // 如果1秒内未完成，继续运行，让用户看到部分加载的界面
            }
        }
        catch (Exception ex)
        {
            CustomDialog.ShowModal("启动错误", $"程序启动失败: {ex.Message}", false);
            Shutdown(1);
        }
    }
}