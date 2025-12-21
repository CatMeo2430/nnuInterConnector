using System;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
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
            // 延迟显示主窗口，给用户一个启动的感觉
            await Task.Delay(300);
            
            var mainWindow = new MainWindow();
            
            try
            {
                var viewModel = new MainViewModel();
                mainWindow.DataContext = viewModel;
            }
            catch (Exception ex)
            {
                CustomDialog.ShowModal("启动错误", $"程序初始化失败: {ex.Message}\n\n请确保：\n1. 已连接到校园网\n2. 配置文件正确\n3. 以管理员权限运行", false);
                Shutdown(1);
                return;
            }
            
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            CustomDialog.ShowModal("启动错误", $"程序启动失败: {ex.Message}", false);
            Shutdown(1);
        }
    }
}