using ITDoku.Models;

public class Network
{
    public int NetworkId { get; set; }
    public string CidrNotation { get; set; } = default!;
    public string? Description { get; set; }
    public string? DhcpRangeStart { get; set; }
    public string? DhcpRangeEnd { get; set; }
    public string? DnsServer { get; set; }

    public Guid? AssignedToDokuObjectId { get; set; }   // <— Guid? statt int?
    public DokuObject? AssignedToDokuObject { get; set; }
}
