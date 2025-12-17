using Client.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace Client;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        await _viewModel.CleanupAsync();
        base.OnClosing(e);
    }
}