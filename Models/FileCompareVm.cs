namespace ITDoku.Models;

public class DiffLine
{
    public string? Left { get; set; }
    public string? Right { get; set; }
    // "same" | "add" | "remove" | "change" | "bin"
    public string Status { get; set; } = "same";
    public int LineNoLeft { get; set; }
    public int LineNoRight { get; set; }
}

public class FileCompareVm
{
    public DokuFile File { get; set; } = default!;
    public DokuFileVersion A { get; set; } = default!;
    public DokuFileVersion B { get; set; } = default!;
    public bool IsText { get; set; }
    public List<DiffLine> Lines { get; set; } = new();
}
