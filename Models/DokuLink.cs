namespace ITDoku.Models;

public class DokuLink
{
    public Guid Id { get; set; }

    // Wo soll der Link angezeigt werden?
    public Guid ParentId { get; set; }
    public DokuObject Parent { get; set; } = default!;

    // Welches Objekt wird verlinkt?
    public Guid TargetObjectId { get; set; }
    public DokuObject TargetObject { get; set; } = default!;
}
