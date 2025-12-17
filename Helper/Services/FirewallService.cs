using System.Diagnostics;
using System.Management;

namespace Helper.Services;

public class FirewallService
{
    public static bool AddFirewallRule(string ipAddress)
    {
        try
        {
            var ruleName = $"NNU_InterConnector_{ipAddress.Replace('.', '_')}";
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"advfirewall firewall add rule name=\"{ruleName}\" dir=in action=allow remoteip={ipAddress}/32 enable=yes",
                    Verb = "runas",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                Console.WriteLine($"防火墙规则已添加: {ruleName}");
                return true;
            }
            else
            {
                Console.WriteLine($"添加防火墙规则失败: {error}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"添加防火墙规则异常: {ex.Message}");
            return false;
        }
    }

    public static bool RemoveFirewallRule(string ipAddress)
    {
        try
        {
            var ruleName = $"NNU_InterConnector_{ipAddress.Replace('.', '_')}";
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"advfirewall firewall delete rule name=\"{ruleName}\"",
                    Verb = "runas",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0 || output.Contains("已删除") || output.Contains("Deleted"))
            {
                Console.WriteLine($"防火墙规则已删除: {ruleName}");
                return true;
            }
            else
            {
                Console.WriteLine($"删除防火墙规则失败: {error}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"删除防火墙规则异常: {ex.Message}");
            return false;
        }
    }

    public static bool IsRuleExists(string ipAddress)
    {
        try
        {
            var ruleName = $"NNU_InterConnector_{ipAddress.Replace('.', '_')}";
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "advfirewall firewall show rule name=all",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return output.Contains(ruleName);
        }
        catch
        {
            return false;
        }
    }
}