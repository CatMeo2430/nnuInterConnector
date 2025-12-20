using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RegistrationController : ControllerBase
{
    private static readonly ConcurrentDictionary<string, string> _pendingRegistrations = new();
    private readonly ILogger<RegistrationController> _logger;
    private const int MaxPendingRegistrations = 10000;

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

        CleanupOldRegistrations();
        
        if (_pendingRegistrations.Count >= MaxPendingRegistrations)
        {
            _logger.LogWarning("Registration queue is full");
            return StatusCode(503, new { error = "Server is busy, please try again later" });
        }

        _pendingRegistrations[clientUuid] = clientIp;
        _logger.LogInformation("Client registration request: UUID={Uuid}, IP={Ip}", clientUuid, clientIp);
        return Ok(new { message = "等待WebSocket连接完成注册" });
    }

    private void CleanupOldRegistrations()
    {
        if (_pendingRegistrations.Count > MaxPendingRegistrations * 0.9)
        {
            var oldRegistrations = _pendingRegistrations.Keys.Take(100).ToList();
            foreach (var uuid in oldRegistrations)
            {
                _pendingRegistrations.TryRemove(uuid, out _);
            }
            _logger.LogInformation("Cleaned up {Count} old registration entries", oldRegistrations.Count);
        }
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