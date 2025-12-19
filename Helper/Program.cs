using Helper.Services;

namespace Helper;

class Program
{
    static int Main(string[] args)
    {
        Console.WriteLine("NNU InterConnector Helper");
        Console.WriteLine("=========================");
        
        if (args.Length == 0)
        {
            ShowUsage();
            return 1;
        }

        var command = args[0].ToLower();

        try
        {
            switch (command)
            {
                case "add":
                    return HandleAddCommand(args);
                case "remove":
                    return HandleRemoveCommand(args);
                default:
                    Console.WriteLine($"未知命令: {command}");
                    ShowUsage();
                    return 1;
            }
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            Console.WriteLine($"权限错误: {ex.Message}");
            Console.WriteLine("请以管理员身份运行此程序");
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"执行命令时发生错误: {ex.Message}");
            Console.WriteLine($"错误类型: {ex.GetType().Name}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"内部错误: {ex.InnerException.Message}");
            }
            return 1;
        }
    }

    static void ShowUsage()
    {
        Console.WriteLine("\n用法:");
        Console.WriteLine("  Helper add <ip_address> [gateway]     - 添加防火墙规则和路由");
        Console.WriteLine("  Helper remove <ip_address>            - 删除防火墙规则和路由");
        Console.WriteLine("\n示例:");
        Console.WriteLine("  Helper add 10.20.1.100 10.20.0.1");
        Console.WriteLine("  Helper add 10.30.5.200");
        Console.WriteLine("  Helper remove 10.20.1.100");
    }

    static int HandleAddCommand(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("错误: 需要指定IP地址");
            return 1;
        }

        var ipAddress = args[1];
        var gateway = args.Length > 2 ? args[2] : null;

        if (!IsValidIpAddress(ipAddress))
        {
            Console.WriteLine($"错误: 无效的IP地址: {ipAddress}");
            return 1;
        }

        Console.WriteLine($"正在配置连接: {ipAddress}");

        var firewallSuccess = FirewallService.AddFirewallRule(ipAddress);
        if (!firewallSuccess)
        {
            Console.WriteLine("防火墙配置失败");
            return 1;
        }

        if (!string.IsNullOrEmpty(gateway) && IsValidIpAddress(gateway))
        {
            if (IsSameSubnet(ipAddress, gateway))
            {
                var routeSuccess = RouteService.AddRoute(ipAddress, gateway);
                if (!routeSuccess)
                {
                    Console.WriteLine("路由配置失败");
                    FirewallService.RemoveFirewallRule(ipAddress);
                    return 1;
                }
            }
            else
            {
                Console.WriteLine($"警告: IP地址 {ipAddress} 和网关 {gateway} 不在同一网段，跳过路由配置");
            }
        }
        else if (!string.IsNullOrEmpty(gateway))
        {
            Console.WriteLine($"警告: 无效的网关地址: {gateway}");
        }

        Console.WriteLine("配置完成！");
        return 0;
    }

    static int HandleRemoveCommand(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("错误: 需要指定IP地址");
            return 1;
        }

        var ipAddress = args[1];

        if (!IsValidIpAddress(ipAddress))
        {
            Console.WriteLine($"错误: 无效的IP地址: {ipAddress}");
            return 1;
        }

        Console.WriteLine($"正在清理配置: {ipAddress}");

        var firewallSuccess = FirewallService.RemoveFirewallRule(ipAddress);
        var routeSuccess = RouteService.RemoveRoute(ipAddress);

        if (firewallSuccess || routeSuccess)
        {
            Console.WriteLine("配置清理完成！");
            return 0;
        }
        else
        {
            Console.WriteLine("配置清理失败或规则不存在");
            return 1;
        }
    }

    static bool IsValidIpAddress(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
            return false;

        var parts = ip.Split('.');
        if (parts.Length != 4)
            return false;

        foreach (var part in parts)
        {
            if (!int.TryParse(part, out var num) || num < 0 || num > 255)
                return false;
        }

        return true;
    }

    static bool IsSameSubnet(string ip1, string ip2)
    {
        var parts1 = ip1.Split('.');
        var parts2 = ip2.Split('.');

        if (parts1.Length < 2 || parts2.Length < 2)
            return false;

        return parts1[0] == parts2[0] && parts1[1] == parts2[1];
    }
}