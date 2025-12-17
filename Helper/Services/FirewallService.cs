using System.Diagnostics;
using System.Management;

namespace Helper.Services;

public class FirewallService
{
    public static bool AddFirewallRule(string ipAddress)
    {
        try
        {
            var ruleNameIn = $"NNU_InterConnector_In_{ipAddress.Replace('.', '_')}";
            var ruleNameOut = $"NNU_InterConnector_Out_{ipAddress.Replace('.', '_')}";
            
            var processIn = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"advfirewall firewall add rule name=\"{ruleNameIn}\" dir=in action=allow remoteip={ipAddress}/32 enable=yes",
                    Verb = "runas",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            processIn.Start();
            var outputIn = processIn.StandardOutput.ReadToEnd();
            var errorIn = processIn.StandardError.ReadToEnd();
            processIn.WaitForExit();

            var processOut = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"advfirewall firewall add rule name=\"{ruleNameOut}\" dir=out action=allow remoteip={ipAddress}/32 enable=yes",
                    Verb = "runas",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            processOut.Start();
            var outputOut = processOut.StandardOutput.ReadToEnd();
            var errorOut = processOut.StandardError.ReadToEnd();
            processOut.WaitForExit();

            if (processIn.ExitCode == 0 && processOut.ExitCode == 0)
            {
                Console.WriteLine($"防火墙规则已添加: {ruleNameIn} 和 {ruleNameOut}");
                return true;
            }
            else
            {
                Console.WriteLine($"添加防火墙规则失败: {errorIn} {errorOut}");
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
            var ruleNameIn = $"NNU_InterConnector_In_{ipAddress.Replace('.', '_')}";
            var ruleNameOut = $"NNU_InterConnector_Out_{ipAddress.Replace('.', '_')}";
            
            var processIn = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"advfirewall firewall delete rule name=\"{ruleNameIn}\"",
                    Verb = "runas",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            processIn.Start();
            var outputIn = processIn.StandardOutput.ReadToEnd();
            var errorIn = processIn.StandardError.ReadToEnd();
            processIn.WaitForExit();

            var processOut = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"advfirewall firewall delete rule name=\"{ruleNameOut}\"",
                    Verb = "runas",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            processOut.Start();
            var outputOut = processOut.StandardOutput.ReadToEnd();
            var errorOut = processOut.StandardError.ReadToEnd();
            processOut.WaitForExit();

            if ((processIn.ExitCode == 0 || outputIn.Contains("已删除") || outputIn.Contains("Deleted")) &&
                (processOut.ExitCode == 0 || outputOut.Contains("已删除") || outputOut.Contains("Deleted")))
            {
                Console.WriteLine($"防火墙规则已删除: {ruleNameIn} 和 {ruleNameOut}");
                return true;
            }
            else
            {
                Console.WriteLine($"删除防火墙规则失败: {errorIn} {errorOut}");
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
            var ruleNameIn = $"NNU_InterConnector_In_{ipAddress.Replace('.', '_')}";
            var ruleNameOut = $"NNU_InterConnector_Out_{ipAddress.Replace('.', '_')}";
            
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

            return output.Contains(ruleNameIn) || output.Contains(ruleNameOut);
        }
        catch
        {
            return false;
        }
    }
}