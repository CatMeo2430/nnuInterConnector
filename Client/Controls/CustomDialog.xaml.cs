using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Client.Controls;

public partial class CustomDialog : Window
{
    private bool? _dialogResult;
    public new bool? DialogResult 
    { 
        get => _dialogResult; 
        private set => _dialogResult = value; 
    }

    public CustomDialog(string title, string message, bool showCancelButton = true)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        
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

    public static bool? Show(string title, string message, bool showCancelButton = true)
    {
        var dialog = new CustomDialog(title, message, showCancelButton);
        dialog.ShowDialog();
        return dialog.DialogResult;
    }
}