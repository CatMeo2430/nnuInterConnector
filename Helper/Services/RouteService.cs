using System.Diagnostics;

namespace Helper.Services;

public class RouteService
{
    public static bool AddRoute(string destinationIp, string gateway)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "route",
                    Arguments = $"add {destinationIp} mask 255.255.255.255 {gateway}",
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

            if (process.ExitCode == 0 || output.Contains("操作完成") || output.Contains("OK"))
            {
                Console.WriteLine($"路由已添加: {destinationIp} -> {gateway}");
                return true;
            }
            else
            {
                Console.WriteLine($"添加路由失败: {error}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"添加路由异常: {ex.Message}");
            return false;
        }
    }

    public static bool RemoveRoute(string destinationIp)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "route",
                    Arguments = $"delete {destinationIp}",
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

            if (process.ExitCode == 0 || output.Contains("已删除") || output.Contains("OK"))
            {
                Console.WriteLine($"路由已删除: {destinationIp}");
                return true;
            }
            else
            {
                Console.WriteLine($"删除路由失败: {error}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"删除路由异常: {ex.Message}");
            return false;
        }
    }

    public static bool IsRouteExists(string destinationIp)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "route",
                    Arguments = "print",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return output.Contains(destinationIp);
        }
        catch
        {
            return false;
        }
    }
}