using System.Windows;
using System.Windows.Controls;

namespace Client.Controls;

public partial class CustomDialog : Window
{
    private bool? _dialogResult;
    private bool _isModal = false;
    
    public new bool? DialogResult 
    { 
        get => _dialogResult; 
        private set => _dialogResult = value; 
    }

    public CustomDialog(string title, string message, bool showCancelButton = true, bool isModal = false)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        _isModal = isModal;
        
        if (!showCancelButton)
        {
            CancelButton.Visibility = Visibility.Collapsed;
            Grid.SetColumn(ConfirmButton, 0);
            Grid.SetColumnSpan(ConfirmButton, 3);
        }
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        base.DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        base.DialogResult = false;
        Close();
    }

    // 模态对话框（阻塞式，需要返回值）
    public static bool? ShowModal(string title, string message, bool showCancelButton = true)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[CustomDialog] Creating modal dialog: title={title}, message={message}");
            var dialog = new CustomDialog(title, message, showCancelButton, true);
            System.Diagnostics.Debug.WriteLine($"[CustomDialog] Dialog created, calling ShowDialog...");
            dialog.ShowDialog();
            System.Diagnostics.Debug.WriteLine($"[CustomDialog] ShowDialog returned, result={dialog.DialogResult}");
            return dialog.DialogResult;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CustomDialog] Error in ShowModal: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[CustomDialog] Stack trace: {ex.StackTrace}");
            throw;
        }
    }
    
    // 非模态对话框（非阻塞式，不返回值）
    public static void Show(string title, string message, bool showCancelButton = true)
    {
        var dialog = new CustomDialog(title, message, showCancelButton, false);
        dialog.Show();
    }
}