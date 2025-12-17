using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Client.Services;

public class NetworkService
{
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
                        if (ipString.StartsWith("10.20.") || ipString.StartsWith("10.30."))
                        {
                            return ipString;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to get campus network IP");
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
        if (ip.StartsWith("10.20."))
            return "10.20.0.1";
        if (ip.StartsWith("10.30."))
            return "10.30.0.1";
        return string.Empty;
    }
}