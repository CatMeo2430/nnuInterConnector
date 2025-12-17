using Microsoft.AspNetCore.Mvc;
using Server.Hubs;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RegistrationController : ControllerBase
{
    private readonly ILogger<RegistrationController> _logger;

    public RegistrationController(ILogger<RegistrationController> logger)
    {
        _logger = logger;
    }

    [HttpPost]
    public IActionResult Register([FromHeader(Name = "X-Client-UUID")] string? clientUuid)
    {
        if (string.IsNullOrEmpty(clientUuid))
        {
            _logger.LogWarning("Registration attempt without UUID");
            return BadRequest(new { error = "UUID is required" });
        }

        _logger.LogInformation("Client registration request: UUID={Uuid}", clientUuid);
        return Ok(new { message = "等待WebSocket连接完成注册" });
    }
}