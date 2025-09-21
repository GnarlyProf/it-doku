namespace ITDoku.Ui;
public static class IconMap
{
    // type -> bootstrap-icon class
    public static string For(string? type) => (type ?? "").Trim().ToLower() switch
    {
        "ordner" => "bi-folder2",
        "infrastruktur" => "bi-hdd-network",
        "drittanbieter" => "bi-building",
        "geräte" => "bi-cpu",
        "server" => "bi-server",
        "software" => "bi-box-seam",
        "tresor" => "bi-shield-lock",
        "hilfe" => "bi-life-preserver",
        "webseiten" => "bi-globe",
        "zugangsdaten" => "bi-key",
        _ => "bi-file-earmark"
    };
}
