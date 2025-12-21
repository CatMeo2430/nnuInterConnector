using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Client.Services;

public class NetworkService
{
    // 支持的校园网网段列表
    private static readonly string[] CampusNetworkPrefixes = new[]
    {
        "10.0.", "10.7.", "10.20.", "10.24.", "10.28.", "10.29.",
        "10.30.", "10.100.", "10.128.", "10.132.", "10.136.", "10.137.",
        "10.247.", "10.252.", "10.253."
    };

    public static string GetCampusNetworkIp()
    {
        try
        {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up)
                    continue;

                if (networkInterface.NetworkInterfaceType != NetworkInterfaceType.Wireless80211 &&
                    networkInterface.NetworkInterfaceType != NetworkInterfaceType.Ethernet)
                    continue;

                var ipProperties = networkInterface.GetIPProperties();
                foreach (var ip in ipProperties.UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        var ipString = ip.Address.ToString();
                        foreach (var prefix in CampusNetworkPrefixes)
                        {
                            if (ipString.StartsWith(prefix))
                            {
                                return ipString;
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // 静默处理异常，返回空字符串
        }

        return string.Empty;
    }

    public static bool IsSameSubnet(string ip1, string ip2)
    {
        var parts1 = ip1.Split('.');
        var parts2 = ip2.Split('.');

        if (parts1.Length < 2 || parts2.Length < 2)
            return false;

        return parts1[0] == parts2[0] && parts1[1] == parts2[1];
    }

    public static string GetGatewayForIp(string ip)
    {
        foreach (var prefix in CampusNetworkPrefixes)
        {
            if (ip.StartsWith(prefix))
            {
                // 从前缀中提取第二段数字，如"10.20." -> "20"
                var secondOctet = prefix.Split('.')[1];
                return $"10.{secondOctet}.0.1";
            }
        }
        return string.Empty;
    }
}