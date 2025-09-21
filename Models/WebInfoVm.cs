namespace ITDoku.Models;

public class WebInfoVm
{
    public string? Url { get; set; }
    public string? Username { get; set; }
    public bool HasPassword { get; set; }
    public string? Notes { get; set; }
    public Guid? CredObjectId { get; set; }
}
