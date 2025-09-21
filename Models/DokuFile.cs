using System.ComponentModel.DataAnnotations;

namespace ITDoku.Models;

public class DokuFile
{
    public Guid Id { get; set; }
    public Guid ObjectId { get; set; }
    public DokuObject DokuObject { get; set; } = default!;

    [Required, MaxLength(260)] public string FileName { get; set; } = string.Empty;
    [MaxLength(255)] public string? ContentType { get; set; }
    public long ByteSize { get; set; }

    public byte[]? Content { get; set; } // demo storage in DB

    public int Version { get; set; } = 1;
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public ICollection<DokuFileVersion> Versions { get; set; } = new List<DokuFileVersion>();
}

public class DokuFileVersion
{
    public long Id { get; set; }
    public Guid FileId { get; set; }
    public DokuFile File { get; set; } = default!;

    public int Version { get; set; }
    [Required, MaxLength(260)] public string FileName { get; set; } = string.Empty;
    [MaxLength(255)] public string? ContentType { get; set; }
    public long ByteSize { get; set; }
    public byte[]? Content { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
}
