namespace Client.Models;

public class ConnectionInfo
{
    public int PeerId { get; set; }
    public string PeerIp { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ConnectedTime { get; set; }
}