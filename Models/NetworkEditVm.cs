using System.ComponentModel.DataAnnotations;

public class NetworkEditVm
{
    public int? NetworkId { get; set; } // null = Create

    [Required, MaxLength(50)]
    public string CidrNotation { get; set; } = string.Empty;

    [MaxLength(255)] public string? Description { get; set; }
    [MaxLength(50)] public string? DhcpRangeStart { get; set; }
    [MaxLength(50)] public string? DhcpRangeEnd { get; set; }
    [MaxLength(50)] public string? DnsServer { get; set; }

    // Zuordnung zu DokuObject (optional)
    public Guid? AssignedToDokuObjectId { get; set; }
    public string? AssignedToDokuObjectName { get; set; }

    // Für Dropdown
    public IEnumerable<(Guid id, string name)>? AssignableObjects { get; set; }
}