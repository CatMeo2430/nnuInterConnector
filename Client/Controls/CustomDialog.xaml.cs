using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Client.Controls;

public partial class CustomDialog : Window
{
    private TaskCompletionSource<bool?>? _resultTaskSource;
    private bool? _dialogResult;
    
    public new bool? DialogResult 
    { 
        get => _dialogResult; 
        private set => _dialogResult = value; 
    }

    public Task<bool?> ResultTask => _resultTaskSource?.Task ?? Task.FromResult<bool?>(null);

    public CustomDialog(string title, string message, bool showCancelButton = true)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        _resultTaskSource = new TaskCompletionSource<bool?>();
        
        if (!showCancelButton)
        {
            CancelButton.Visibility = Visibility.Collapsed;
            Grid.SetColumn(ConfirmButton, 0);
            Grid.SetColumnSpan(ConfirmButton, 3);
        }
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        _dialogResult = true;
        _resultTaskSource?.TrySetResult(true);
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _dialogResult = false;
        _resultTaskSource?.TrySetResult(false);
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _resultTaskSource?.TrySetResult(_dialogResult);
    }

    // 模态对话框（阻塞式，需要返回值）
    public static bool? ShowModal(string title, string message, bool showCancelButton = true)
    {
        var dialog = new CustomDialog(title, message, showCancelButton);
        dialog.ShowDialog();
        return dialog.DialogResult;
    }
    
    // 非模态对话框（非阻塞式，返回Task等待结果）
    public static CustomDialog Show(string title, string message, bool showCancelButton = true)
    {
        var dialog = new CustomDialog(title, message, showCancelButton);
        dialog.Show();
        return dialog;
    }
}