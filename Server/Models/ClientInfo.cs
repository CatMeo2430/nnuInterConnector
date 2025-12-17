namespace Server.Models;

public class ClientInfo
{
    public string Uuid { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public DateTime LastHeartbeat { get; set; }
    public string ConnectionId { get; set; } = string.Empty;
}