using Microsoft.AspNetCore.Mvc.Rendering;

namespace ITDoku.Models;

public class DokuObjectEditVm
{
    public DokuObject Item { get; set; } = new();
    public List<SelectListItem> ParentOptions { get; set; } = new();

    // Nur Webseiten: URL
    public string? Url => Item.Url;

    // Optional Zugangsdaten anlegen (nur wenn Webseiten)
    public bool CreateCredentials { get; set; }
    public string? CredUsername { get; set; }
    public string? CredPassword { get; set; }
    public string? CredNotes { get; set; }
}
