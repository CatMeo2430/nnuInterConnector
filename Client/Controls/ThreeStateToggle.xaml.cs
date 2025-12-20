using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Client.Controls;

public enum ConnectionMode
{
    Manual,
    AutoReject,
    AutoAccept
}

public partial class ThreeStateToggle : UserControl
{
    public static readonly DependencyProperty CurrentStateProperty =
        DependencyProperty.Register("CurrentState", typeof(ConnectionMode), typeof(ThreeStateToggle),
            new PropertyMetadata(ConnectionMode.Manual, OnCurrentStateChanged));

    public ConnectionMode CurrentState
    {
        get => (ConnectionMode)GetValue(CurrentStateProperty);
        set => SetValue(CurrentStateProperty, value);
    }

    public event EventHandler<ConnectionMode>? StateChanged;

    public ThreeStateToggle()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateVisualState(CurrentState, false);
        UpdateTextColors();
    }

    private static void OnCurrentStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ThreeStateToggle toggle)
        {
            toggle.UpdateVisualState((ConnectionMode)e.NewValue, true);
            toggle.UpdateTextColors();
            toggle.StateChanged?.Invoke(toggle, (ConnectionMode)e.NewValue);
        }
    }

    private void OnMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var newState = CurrentState switch
        {
            ConnectionMode.Manual => ConnectionMode.AutoReject,
            ConnectionMode.AutoReject => ConnectionMode.AutoAccept,
            ConnectionMode.AutoAccept => ConnectionMode.Manual,
            _ => ConnectionMode.Manual
        };

        CurrentState = newState;
    }

    private void UpdateVisualState(ConnectionMode state, bool animate)
    {
        if (ActualWidth == 0) return;
        
        double targetX = 0;
        Color backgroundColor;
        
        switch (state)
        {
            case ConnectionMode.Manual:
                targetX = 0;
                backgroundColor = Color.FromRgb(255, 193, 7); // 黄色
                break;
            case ConnectionMode.AutoReject:
                targetX = ActualWidth / 3;
                backgroundColor = Color.FromRgb(244, 67, 54); // 红色
                break;
            case ConnectionMode.AutoAccept:
                targetX = ActualWidth * 2 / 3;
                backgroundColor = Color.FromRgb(76, 175, 80); // 绿色
                break;
            default:
                return;
        }

        if (animate)
        {
            var animation = new DoubleAnimation(targetX, TimeSpan.FromMilliseconds(300));
            animation.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut };
            IndicatorTransform.BeginAnimation(TranslateTransform.XProperty, animation);
        }
        else
        {
            IndicatorTransform.X = targetX;
        }
        
        SelectionIndicator.Background = new SolidColorBrush(backgroundColor);
    }

    private void UpdateTextColors()
    {
        var isManual = CurrentState == ConnectionMode.Manual;
        var isAutoReject = CurrentState == ConnectionMode.AutoReject;
        var isAutoAccept = CurrentState == ConnectionMode.AutoAccept;

        ManualText.Foreground = isManual ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Colors.Black);
        AutoRejectText.Foreground = isAutoReject ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Colors.Black);
        AutoAcceptText.Foreground = isAutoAccept ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Colors.Black);
    }
}
