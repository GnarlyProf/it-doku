using ITDoku.Data;
using ITDoku.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace ITDoku.Controllers;

public class FilesController(AppDbContext db) : Controller
{
    [HttpPost]
    [HttpGet]
    public async Task<IActionResult> Exists(Guid objectId, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return Json(new { exists = false });
        var existing = await db.Files
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.ObjectId == objectId && f.FileName == fileName);
        return existing == null
            ? Json(new { exists = false })
            : Json(new { exists = true, id = existing.Id, version = existing.Version, size = existing.ByteSize });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(Guid objectId, IFormFile file, string? note)
    {
        var obj = await db.Objects.FindAsync(objectId);
        if (obj == null) return NotFound();
        if (obj.NodeType == NodeType.Webseiten)
        {
            TempData["Info"] = "Für Webseiten-Objekte ist kein Upload erlaubt.";
            return RedirectToAction("Index", "Objects", new { id = objectId });
        }
        if (file == null || file.Length == 0) return BadRequest("Datei fehlt.");

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var content = ms.ToArray();

        // Suche vorhandene Datei mit IDENTISCHEM Namen im gleichen Ordner
        var existing = await db.Files
            .FirstOrDefaultAsync(f => f.ObjectId == objectId && f.FileName == file.FileName);

        if (existing != null)
        {
            // => AUTOMATISCHE VERSIONIERUNG
            var next = existing.Version + 1;

            db.FileVersions.Add(new DokuFileVersion
            {
                FileId = existing.Id,
                Version = next,
                FileName = file.FileName,
                ContentType = file.ContentType,
                ByteSize = file.Length,
                Content = content,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User?.Identity?.Name
            });

            existing.Version = next;
            existing.Content = content;
            existing.ContentType = file.ContentType;
            existing.ByteSize = file.Length;
            existing.Note = note ?? existing.Note;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = User?.Identity?.Name;

            await db.SaveChangesAsync();
            TempData["Info"] = $"Neue Version für {existing.FileName}: v{next}.";
            return RedirectToAction("Index", "Objects", new { id = objectId });
        }

        // => NEUE Datei anlegen (v1)
        var doc = new DokuFile
        {
            ObjectId = objectId,
            FileName = file.FileName,
            ContentType = file.ContentType,
            ByteSize = file.Length,
            Content = content,
            Note = note,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = User?.Identity?.Name
        };
        db.Files.Add(doc);
        db.FileVersions.Add(new DokuFileVersion
        {
            File = doc,
            Version = 1,
            FileName = file.FileName,
            ContentType = file.ContentType,
            ByteSize = file.Length,
            Content = content,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = User?.Identity?.Name
        });

        await db.SaveChangesAsync();
        TempData["Info"] = $"Datei gespeichert: {file.FileName} (v1).";
        return RedirectToAction("Index", "Objects", new { id = objectId });
    }

    // kleine Helferfunktion: macht einen eindeutigen Namen (Name (1).ext, Name (2).ext, …)
    private string MakeUniqueName(string name, Guid objectId)
    {
        var dot = name.LastIndexOf('.');
        string baseName = dot > 0 ? name.Substring(0, dot) : name;
        string ext = dot > 0 ? name.Substring(dot) : "";
        int i = 1;
        while (true)
        {
            var candidate = $"{baseName} ({i}){ext}";
            var exists = db.Files.Any(f => f.ObjectId == objectId && f.FileName == candidate);
            if (!exists) return candidate;
            i++;
        }
    }

    public async Task<FileResult> Download(Guid id)
    {
        var f = await db.Files.FindAsync(id);
        if (f == null || f.Content == null) throw new FileNotFoundException();
        return File(f.Content, f.ContentType ?? "application/octet-stream", f.FileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Replace(Guid id, IFormFile file, string? note)
    {
        var f = await db.Files.Include(x => x.Versions).FirstOrDefaultAsync(x => x.Id == id);
        if (f == null) return NotFound();
        if (file == null || file.Length == 0) return BadRequest("Keine Datei gewählt.");

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var content = ms.ToArray();

        var next = f.Version + 1;
        db.FileVersions.Add(new DokuFileVersion
        {
            FileId = f.Id,
            Version = next,
            FileName = file.FileName,
            ContentType = file.ContentType,
            ByteSize = file.Length,
            Content = content,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = User?.Identity?.Name
        });

        f.Version = next;
        f.FileName = file.FileName;
        f.ContentType = file.ContentType;
        f.ByteSize = file.Length;
        f.Content = content;
        f.Note = note ?? f.Note;
        f.UpdatedAt = DateTime.UtcNow;
        f.UpdatedBy = User?.Identity?.Name;

        await db.SaveChangesAsync();
        return RedirectToAction("Index", "Objects", new { id = f.ObjectId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var f = await db.Files.Include(x => x.Versions)
                              .FirstOrDefaultAsync(x => x.Id == id);
        if (f == null) return NotFound();

        var count = f.Versions.Count;
        var objectId = f.ObjectId;

        db.Files.Remove(f);                 // -> Versionen werden per Cascade mit gelöscht
        await db.SaveChangesAsync();

        TempData["Info"] = $"Datei inkl. {count} Version(en) gelöscht.";
        return RedirectToAction("Index", "Objects", new { id = objectId });
    }

    [HttpGet]
    public async Task<IActionResult> Versions(Guid fileId)
    {
        var file = await db.Files
            .Include(f => f.DokuObject)
            .Include(f => f.Versions)
            .FirstOrDefaultAsync(f => f.Id == fileId);
        if (file == null) return NotFound();

        file.Versions = file.Versions.OrderByDescending(v => v.Version).ToList();
        return View(file);
    }

    // ========= NEU: einzelne Version herunterladen =========
    [HttpGet]
    public async Task<IActionResult> DownloadVersion(long id)
    {
        var v = await db.FileVersions.Include(x => x.File).FirstOrDefaultAsync(x => x.Id == id);
        if (v == null || v.Content == null) return NotFound();
        return File(v.Content, v.ContentType ?? "application/octet-stream", $"{v.File.FileName}.v{v.Version}");
    }

    // ========= NEU: alte Version als aktuelle wiederherstellen (als neue Version speichern) =========
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(long id)
    {
        var v = await db.FileVersions.Include(x => x.File).FirstOrDefaultAsync(x => x.Id == id);
        if (v == null) return NotFound();

        var f = v.File;

        var next = f.Version + 1;
        db.FileVersions.Add(new DokuFileVersion
        {
            FileId = f.Id,
            Version = next,
            FileName = v.FileName,
            ContentType = v.ContentType,
            ByteSize = v.ByteSize,
            Content = v.Content,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = User?.Identity?.Name
        });

        f.Version = next;
        f.FileName = v.FileName;
        f.ContentType = v.ContentType;
        f.ByteSize = v.ByteSize;
        f.Content = v.Content;
        f.UpdatedAt = DateTime.UtcNow;
        f.UpdatedBy = User?.Identity?.Name;

        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Versions), new { fileId = f.Id });
    }

    // ========= NEU: Vergleich zweier Versionen =========
    [HttpGet]
    public async Task<IActionResult> Compare(long a, long b)
    {
        var va = await db.FileVersions.Include(x => x.File).FirstOrDefaultAsync(x => x.Id == a);
        var vb = await db.FileVersions.Include(x => x.File).FirstOrDefaultAsync(x => x.Id == b);
        if (va == null || vb == null) return NotFound();
        if (va.FileId != vb.FileId) return BadRequest("Beide Versionen müssen zur selben Datei gehören.");

        var isText = IsTextual(va) && IsTextual(vb);

        var vm = new FileCompareVm
        {
            File = va.File,
            A = va,
            B = vb,
            IsText = isText,
            Lines = isText ? BuildSimpleLineDiff(GetText(va), GetText(vb)) : new List<DiffLine>
            {
                new DiffLine { Status = "bin" }
            }
        };

        return View(vm);
    }

    // ========= Helfer =========
    private static bool IsTextual(DokuFileVersion v)
    {
        if (!string.IsNullOrEmpty(v.ContentType) && v.ContentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            return true;
        var name = v.FileName?.ToLowerInvariant() ?? "";
        string[] textExt = { ".txt", ".md", ".json", ".xml", ".yml", ".yaml", ".cs", ".cshtml", ".js", ".ts", ".css", ".html", ".htm", ".sql", ".ini", ".log", ".config" };
        return textExt.Any(name.EndsWith);
    }

    // using System.Text;

    private static string GetText(DokuFileVersion v)
    {
        var bytes = v.Content ?? Array.Empty<byte>();
        if (bytes.Length == 0) return "";

        // BOM-Erkennung
        ReadOnlySpan<byte> buf = bytes;
        if (buf.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))        // UTF-8 BOM
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        if (buf.StartsWith(new byte[] { 0xFF, 0xFE }))              // UTF-16 LE BOM
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        if (buf.StartsWith(new byte[] { 0xFE, 0xFF }))              // UTF-16 BE BOM
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

        // Strenges UTF-8 (wirft bei Fehlern -> dann Fallback)
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(bytes);
        }
        catch { /* fallback */ }

        // Heuristik: viele 0x00-Bytes deuten auf UTF-16
        int zeroEven = 0, zeroOdd = 0;
        for (int i = 0; i < Math.Min(200, bytes.Length); i++)
            if (bytes[i] == 0) { if ((i & 1) == 0) zeroEven++; else zeroOdd++; }
        if (zeroOdd > zeroEven * 3) return Encoding.Unicode.GetString(bytes);         // UTF-16 LE
        if (zeroEven > zeroOdd * 3) return Encoding.BigEndianUnicode.GetString(bytes);// UTF-16 BE

        // Letzter Fallback: Windows-1252 (häufig bei CSV/Excel)
        return Encoding.GetEncoding(1252).GetString(bytes);
    }

    // sehr einfache Zeilen-Diff-Logik (kein Myers-Algorithmus, aber schnell & ausreichend)
    // ===== Zeilen-Diff mit LCS, markiert add/remove und fasst benachbarte Paare als change zusammen =====
    // LCS-basiert, erzeugt gepaarte Zeilen: same | change | add | remove
    // Zeilen-Diff mit LCS + Ähnlichkeitsprüfung:
    //  - same: identisch
    //  - change: nur wenn REMOVE+ADD deutlich ähnlich (LCS/MaxLen >= 0.7)
    //  - remove: linke Zeile existiert nicht mehr (rechte Seite leer)
    //  - add:    rechte Zeile neu (linke Seite leer)
    // Strikter Zeilen-Diff: same | add | remove  (KEIN Zusammenfassen zu "change")
    // Strenger Zeilen-Diff mit optionalem Pairing zu "change" bei ähnlichen Zeilen
    private static List<DiffLine> BuildSimpleLineDiff(string left, string right)
    {
        var L = (left ?? "").Replace("\r\n", "\n").Split('\n');
        var R = (right ?? "").Replace("\r\n", "\n").Split('\n');
        int n = L.Length, m = R.Length;

        // 1) LCS-Tabelle (Zeilen)
        var dp = new int[n + 1, m + 1];
        for (int i = n - 1; i >= 0; i--)
            for (int j = m - 1; j >= 0; j--)
                dp[i, j] = L[i] == R[j] ? dp[i + 1, j + 1] + 1 : Math.Max(dp[i + 1, j], dp[i, j + 1]);

        // 2) Roh-Operationen: EQUAL | ADD | REMOVE
        var ops = new List<(string kind, string? left, string? right)>();
        int iL = 0, iR = 0;
        while (iL < n || iR < m)
        {
            if (iL < n && iR < m && L[iL] == R[iR])
            {
                ops.Add(("EQUAL", L[iL], R[iR])); iL++; iR++;
            }
            else if (iR < m && (iL == n || dp[iL, iR + 1] >= dp[iL + 1, iR]))
            {
                ops.Add(("ADD", null, R[iR])); iR++;
            }
            else
            {
                ops.Add(("REMOVE", L[iL], null)); iL++;
            }
        }

        // 3) Zu Zeilen zusammenbauen:
        //    - REMOVE + (ähnliches) ADD => change (beide gefärbt gelb)
        //    - reines REMOVE => remove (beide rot; rechts leer)
        //    - reines ADD    => add    (beide grün; links leer)
        //    - EQUAL         => same
        var rows = new List<DiffLine>();
        for (int k = 0; k < ops.Count; k++)
        {
            var op = ops[k];
            if (op.kind == "EQUAL")
            {
                rows.Add(new DiffLine { Left = op.left, Right = op.right, Status = "same" });
                continue;
            }

            if (op.kind == "REMOVE" && k + 1 < ops.Count && ops[k + 1].kind == "ADD")
            {
                var next = ops[k + 1];
                if (AreLinesSimilar(op.left!, next.right!)) // nur ähnliche Zeilen paaren
                {
                    rows.Add(new DiffLine { Left = op.left, Right = next.right, Status = "change" });
                    k++; // ADD verbrauchen
                    continue;
                }
            }

            if (op.kind == "REMOVE")
            {
                rows.Add(new DiffLine { Left = op.left, Right = null, Status = "remove" });
            }
            else // ADD
            {
                rows.Add(new DiffLine { Left = null, Right = op.right, Status = "add" });
            }
        }

        return rows;
    }

    // Zeichen-basierte Ähnlichkeitsprüfung (LCS/MaxLen)
    private static bool AreLinesSimilar(string a, string b, double threshold = 0.6)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        int la = a.Length, lb = b.Length;
        var dp = new int[la + 1, lb + 1];
        for (int i = la - 1; i >= 0; i--)
            for (int j = lb - 1; j >= 0; j--)
                dp[i, j] = a[i] == b[j] ? dp[i + 1, j + 1] + 1 : Math.Max(dp[i + 1, j], dp[i, j + 1]);
        double ratio = (double)dp[la, lb] / Math.Max(la, lb);
        return ratio >= threshold;
    }


}
