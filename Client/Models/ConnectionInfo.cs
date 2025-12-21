using System;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace Client.Models;

public partial class ConnectionInfo : ObservableObject
{
    [ObservableProperty]
    private int _peerId;
    
    [ObservableProperty]
    private string _peerIp = string.Empty;
    
    [ObservableProperty]
    private string _status = string.Empty;
    
    [ObservableProperty]
    private DateTime _connectedTime;
    
    partial void OnStatusChanged(string value)
    {
        OnPropertyChanged(nameof(StatusColor));
    }
    
    public Brush StatusColor => Status switch
    {
        "已连接" => new SolidColorBrush(Colors.Green),
        "连接中" => new SolidColorBrush(Colors.Orange),
        "已断开" => new SolidColorBrush(Colors.Red),
        "断开中" => new SolidColorBrush(Colors.Gray),
        "配置失败" => new SolidColorBrush(Colors.Red),
        _ => new SolidColorBrush(Colors.Black)
    };
}