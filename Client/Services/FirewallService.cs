using System.Diagnostics;

namespace Client.Services;

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
                return true;
            }
            else
            {
                return false;
            }
        }
        catch
        {
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
                return true;
            }
            else
            {
                return false;
            }
        }
        catch
        {
            return false;
        }
    }
}