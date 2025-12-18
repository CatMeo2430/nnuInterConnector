using Microsoft.AspNetCore.Mvc;
using Server.Hubs;
using System.Collections.Concurrent;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RegistrationController : ControllerBase
{
    private static readonly ConcurrentDictionary<string, string> _pendingRegistrations = new();
    private readonly ILogger<RegistrationController> _logger;

    public RegistrationController(ILogger<RegistrationController> logger)
    {
        _logger = logger;
    }

    [HttpPost]
    public IActionResult Register(
        [FromHeader(Name = "X-Client-UUID")] string? clientUuid,
        [FromHeader(Name = "X-Client-IP")] string? clientIp)
    {
        if (string.IsNullOrEmpty(clientUuid))
        {
            _logger.LogWarning("Registration attempt without UUID");
            return BadRequest(new { error = "UUID is required" });
        }

        if (string.IsNullOrEmpty(clientIp))
        {
            _logger.LogWarning("Registration attempt without IP");
            return BadRequest(new { error = "IP address is required" });
        }

        _pendingRegistrations[clientUuid] = clientIp;
        _logger.LogInformation("Client registration request: UUID={Uuid}, IP={Ip}", clientUuid, clientIp);
        return Ok(new { message = "等待WebSocket连接完成注册" });
    }

    public static string? GetPendingIp(string uuid)
    {
        _pendingRegistrations.TryGetValue(uuid, out var ip);
        return ip;
    }

    public static void RemovePendingRegistration(string uuid)
    {
        _pendingRegistrations.TryRemove(uuid, out _);
    }
}