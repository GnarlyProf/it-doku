using System.ComponentModel.DataAnnotations;

namespace ITDoku.Models;

public class DokuSecret
{
    public Guid Id { get; set; }

    // 1:1 zu einem DokuObject vom Typ „Zugangsdaten (9)“
    public Guid ObjectId { get; set; }
    public DokuObject DokuObject { get; set; } = default!;

    [MaxLength(256)]
    public string? Username { get; set; }

    // verschlüsseltes Passwort (Base64)
    [Required]
    public string PasswordEnc { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
}
