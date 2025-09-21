using System.ComponentModel.DataAnnotations;

namespace ITDoku.Models;
public static class DokuConsts { public const int MaxDepth = 10; }
public enum NodeType : byte
{
    Ordner = 0,
    Infrastruktur = 1,
    Drittanbieter = 2,
    Geräte = 3,
    Server = 4,
    Software = 5,
    Tresor = 6,
    Hilfe = 7,
    Webseiten = 8,
    Zugangsdaten = 9,
    Netzwerke = 10
}

public class DokuObject
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public DokuObject? Parent { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public NodeType NodeType { get; set; } = NodeType.Ordner;

    [Range(0, DokuConsts.MaxDepth)]
    public int Level { get; set; } = 0;

    public int SortOrder { get; set; } = 0;
    public string? Description { get; set; }

    // Nur bei Webseiten(8) genutzt
    [MaxLength(1000)]
    [Url(ErrorMessage = "Bitte eine gültige URL (http/https) angeben.")]
    public string? Url { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<DokuObject> Children { get; set; } = new List<DokuObject>();
    public ICollection<DeviceIp> DeviceIps { get; set; } = new List<DeviceIp>();
    public ICollection<DokuFile> Files { get; set; } = new List<DokuFile>();
}