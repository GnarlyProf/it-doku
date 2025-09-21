using ITDoku.Data;
using ITDoku.Models;
using ITDoku.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ITDoku.Controllers;

public class ObjectsController(AppDbContext db, ISecretProtector protector) : Controller
{

    private readonly AppDbContext _db = db;
    private readonly ISecretProtector _protector = protector;



    // Baum-Übersicht (links) + Details/Dateien (rechts)
    public async Task<IActionResult> Index(Guid? id)
    {
        // 1) Alle Objekte laden
        var all = await db.Objects.AsNoTracking()
            .OrderBy(x => x.ParentId).ThenBy(x => x.SortOrder).ThenBy(x => x.Name)
            .ToListAsync();

        // 2) Baum neu verdrahten
        var byId = all.ToDictionary(x => x.Id);
        foreach (var o in all)
        {
            o.Children = new List<DokuObject>();

            if (o.ParentId.HasValue && byId.TryGetValue(o.ParentId.Value, out var p))
                p.Children.Add(o);
        }

        // Links nachträglich einhängen
        var links = await db.Links.AsNoTracking().ToListAsync();
        foreach (var link in links)
        {
            if (byId.TryGetValue(link.ParentId, out var parent) &&
                byId.TryGetValue(link.TargetObjectId, out var target))
            {
                if (!parent.Children.Any(c => c.Id == target.Id))
                    parent.Children.Add(target);
            }
        }

        // 3) Wurzeln & aktuelle Auswahl
        var roots = all.Where(x => x.ParentId == null).ToList();
        var current = (id.HasValue && byId.TryGetValue(id.Value, out var cur))
            ? cur : roots.FirstOrDefault();

        // 4) Dateien/Versionen
        if (current != null)
        {
            current.Files = await db.Files.AsNoTracking()
                .Where(f => f.ObjectId == current.Id)
                .Include(f => f.Versions)
                .OrderByDescending(f => f.UpdatedAt ?? f.CreatedAt)
                .ToListAsync();

            // 5) WebInfo bei Webseiten
            if (current.NodeType == NodeType.Webseiten)
            {
                var credChild = current.Children.FirstOrDefault(c => c.NodeType == NodeType.Zugangsdaten);
                DokuSecret? secret = null;
                if (credChild != null)
                    secret = await db.Secrets.AsNoTracking().FirstOrDefaultAsync(s => s.ObjectId == credChild.Id);

                ViewBag.WebInfo = new WebInfoVm
                {
                    Url = current.Url,
                    Username = secret?.Username,
                    HasPassword = !string.IsNullOrEmpty(secret?.PasswordEnc),
                    Notes = secret?.Notes,
                    CredObjectId = credChild?.Id
                };
            }

            // 6) Netzwerke + Device-IPs NUR bei Infrastruktur/Server/Geräte
            if (current.NodeType == NodeType.Infrastruktur ||
                current.NodeType == NodeType.Server ||
                current.NodeType == NodeType.Geräte)
            {
                ViewBag.DeviceIps = await db.DeviceIPs.AsNoTracking()
                    .Where(x => x.DokuObjectId == current.Id)
                    .OrderBy(x => x.IpAddress)
                    .ToListAsync();

                var netsByObject = await db.Networks
                    .Where(n => n.AssignedToDokuObjectId != null)
                    .AsNoTracking()
                    .GroupBy(n => n.AssignedToDokuObjectId!.Value)
                    .ToDictionaryAsync(g => g.Key, g => g.ToList());

                ViewBag.NetworksByObject = netsByObject;
                ViewBag.ShowNetworkSection = true;
            }
            else
            {
                ViewBag.DeviceIps = Enumerable.Empty<DeviceIp>();
                ViewBag.NetworksByObject = null;
                ViewBag.ShowNetworkSection = false;
            }
        }
        else
        {
            ViewBag.DeviceIps = Enumerable.Empty<DeviceIp>();
            ViewBag.NetworksByObject = null;
            ViewBag.ShowNetworkSection = false;
        }

        // 7) LinkMap + Dropdown
        var linkMap = links.ToDictionary(l => (l.ParentId, l.TargetObjectId), l => l.Id);
        ViewBag.LinkMap = linkMap;
        ViewBag.AllObjects = all.OrderBy(o => o.Name).ToList();

        // Pfad zum aktuellen Knoten (für Auto-Expand)
        var openIds = new HashSet<Guid>();
        if (current != null)
        {
            var node = current;
            while (node != null)
            {
                openIds.Add(node.Id);
                node = (node.ParentId.HasValue && byId.TryGetValue(node.ParentId.Value, out var p)) ? p : null;
            }
        }
        ViewBag.CurrentId = current?.Id;
        ViewBag.OpenIds = openIds;

        // virtuelle Links für den Baum
        var virtualEdges = await db.Links
            .Select(l => new { l.ParentId, l.TargetObjectId, l.Id })
            .ToListAsync();

        var edgeMap = virtualEdges.ToDictionary(
            x => $"{x.ParentId:D}:{x.TargetObjectId:D}",
            x => x.Id);

        ViewBag.VirtualEdges = edgeMap;

        return View((roots, current));
    }

    //Passwort anzeigen
    [HttpGet]
    public async Task<IActionResult> RevealSecret(Guid objectId)
    {
        var s = await db.Secrets.AsNoTracking().FirstOrDefaultAsync(x => x.ObjectId == objectId);
        if (s == null) return NotFound();
        var pwd = string.IsNullOrEmpty(s.PasswordEnc) ? "" : protector.Unprotect(s.PasswordEnc);
        return Json(new { password = pwd });
    }

    // ---------- Hilfsfunktion: Parent-Dropdown bauen (hierarchisch, mit Einrückung)



    // Controllers/ObjectsController.cs

    private async Task<List<SelectListItem>> BuildParentOptionsAsync(Guid? excludeId = null)
    {
        var all = await db.Objects
            .AsNoTracking()
            .OrderBy(o => o.ParentId)
            .ThenBy(o => o.SortOrder).ThenBy(o => o.Name)
            .ToListAsync();

        // WICHTIG: Lookup statt Dictionary – unterstützt null-Keys (Root-Ebene)
        ILookup<Guid?, DokuObject> lookup = all.ToLookup(o => o.ParentId);

        List<SelectListItem> items = new()
    {
        new SelectListItem { Value = "", Text = "(Kein übergeordnetes Objekt)" }
    };

        void Walk(Guid? parentId, int level)
        {
            // über lookup[parentId] kommen auch die Root-Kinder, wenn parentId == null
            var children = lookup[parentId]
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name);

            foreach (var n in children)
            {
                if (excludeId.HasValue && n.Id == excludeId.Value) continue; // sich selbst nicht als Parent anbieten

                items.Add(new SelectListItem
                {
                    Value = n.Id.ToString(),
                    Text = $"{new string(' ', level * 2)}{(level > 0 ? "↳ " : "")}{n.Name}"
                });

                // tiefer gehen (max. 5 Ebenen ist durch Model/DB begrenzt)
                Walk(n.Id, level + 1);
            }
        }

        Walk(parentId: null, level: 0);
        return items;
    }

    // ---------- Create ----------
    public async Task<IActionResult> Create(Guid? parentId)
    {
        // Tiefenlimit prüfen (max. 10)
        int nextLevel = 0;
        if (parentId.HasValue)
        {
            var parent = await db.Objects.AsNoTracking().FirstOrDefaultAsync(o => o.Id == parentId.Value);
            if (parent == null) return NotFound();

            if (parent.Level >= 10)
            {
                TempData["Info"] = "Maximale Tiefe (10) erreicht – es kann keine weitere Unterstruktur angelegt werden.";
                return RedirectToAction(nameof(Index), new { id = parent.Id });
            }
            nextLevel = parent.Level + 1;
        }

        var vm = new DokuObjectEditVm
        {
            Item = new DokuObject
            {
                ParentId = parentId,
                Level = nextLevel
            },
            ParentOptions = await BuildParentOptionsAsync(excludeId: null)
        };
        return View("Edit", vm);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DokuObjectEditVm vm)
    {
        // Level aus Parent ableiten
        await BindLevelFromParent(vm.Item);

        if (vm.Item.NodeType == NodeType.Webseiten && string.IsNullOrWhiteSpace(vm.Item.Url))
            ModelState.AddModelError("Item.Url", "Bitte eine URL angeben.");

        if (vm.Item.Level > 10)
            ModelState.AddModelError("", "Maximale Tiefe (10) erreicht – bitte einen übergeordneten Ordner wählen.");

        if (!ModelState.IsValid)
        {
            vm.ParentOptions = await BuildParentOptionsAsync();
            return View("Edit", vm);
        }

        db.Objects.Add(vm.Item);
        await BindLevelFromParent(vm.Item);
        await db.SaveChangesAsync();
        if (vm.Item.NodeType == NodeType.Webseiten && vm.CreateCredentials)
            await CreateCredentialsChildAsync(vm, vm.Item);

        return RedirectToAction(nameof(Index), new { id = vm.Item.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateChild(Guid parentId, string name)
    {
        name = (name ?? "").Trim();
        if (parentId == Guid.Empty) return BadRequest("parentId fehlt.");
        if (name.Length == 0) return BadRequest("Name darf nicht leer sein.");

        var parent = await db.Objects.FindAsync(parentId);
        if (parent == null) return NotFound("Parent nicht gefunden.");

        var child = new DokuObject
        {
            Id = Guid.NewGuid(),
            Name = name,
            NodeType = NodeType.Ordner,          // oder aus Param übernehmen
            ParentId = parent.Id,
            Level = parent.Level + 1,
            SortOrder = 0,
            CreatedAt = DateTime.UtcNow
        };

        db.Objects.Add(child);
        await db.SaveChangesAsync();
        return Ok(); // Frontend lädt neu
    }

    // Helper:
    private async Task BindLevelFromParent(DokuObject o)
    {
        if (o.ParentId == null) { o.Level = 0; return; }
        var parent = await db.Objects.FindAsync(o.ParentId);
        o.Level = ((parent?.Level) ?? -1) + 1;   // parent 0 => child 1 usw.
    }    // ---------- Edit ----------
    public async Task<IActionResult> Edit(Guid id)
    {
        var m = await db.Objects.FindAsync(id);
        if (m == null) return NotFound();

        var vm = new DokuObjectEditVm
        {
            Item = m,
            ParentOptions = await BuildParentOptionsAsync(excludeId: id)
        };

        if (m.NodeType == NodeType.Zugangsdaten)
        {
            var sec = await db.Secrets.AsNoTracking().FirstOrDefaultAsync(s => s.ObjectId == m.Id);
            vm.CredUsername = sec?.Username;
            vm.CredNotes = sec?.Notes;
            vm.CredPassword = null; // leer lassen = unverändert
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, DokuObjectEditVm vm)
    {
        var e = await db.Objects.FindAsync(id);
        if (e == null) return NotFound();

        // gemeinsame Felder übernehmen
        e.Name = vm.Item.Name;
        e.NodeType = vm.Item.NodeType;
        e.Description = vm.Item.Description;
        e.ParentId = vm.Item.ParentId;
        e.SortOrder = vm.Item.SortOrder;
        e.Url = (e.NodeType == NodeType.Webseiten) ? vm.Item.Url : null;
        await BindLevelFromParent(e);
        e.UpdatedAt = DateTime.UtcNow;

        if (e.NodeType == NodeType.Webseiten && string.IsNullOrWhiteSpace(e.Url))
            ModelState.AddModelError("Item.Url", "Bitte eine URL angeben.");

        if (!ModelState.IsValid)
        {
            vm.ParentOptions = await BuildParentOptionsAsync(excludeId: id);
            vm.Item = e;
            return View(vm);
        }

        // --- Zugangsdaten speichern/aktualisieren ---
        if (e.NodeType == NodeType.Zugangsdaten)
        {
            var sec = await db.Secrets.FirstOrDefaultAsync(s => s.ObjectId == e.Id);
            if (sec == null)
            {
                sec = new DokuSecret { ObjectId = e.Id, CreatedBy = User?.Identity?.Name };
                db.Secrets.Add(sec);
            }

            sec.Username = vm.CredUsername;
            sec.Notes = vm.CredNotes;

            // Passwort nur ändern, wenn etwas eingegeben wurde
            if (!string.IsNullOrWhiteSpace(vm.CredPassword))
                sec.PasswordEnc = protector.Protect(vm.CredPassword);
        }
        else
        {
            // wenn Typ weg von Zugangsdaten geändert wurde -> Secret löschen
            var sec = await db.Secrets.FirstOrDefaultAsync(s => s.ObjectId == e.Id);
            if (sec != null) db.Secrets.Remove(sec);
        }
        await BindLevelFromParent(vm.Item);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { id = e.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        // Alles in einer kleinen Transaktion, damit FK/NoAction sauber ist
        using var tx = await db.Database.BeginTransactionAsync();

        var node = await db.Objects
            .Include(o => o.Children)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (node == null) return NotFound();

        // 0) Symbolische Links bereinigen (wegen NO ACTION an beiden FKs)
        //    - Links, die UNTER diesem Knoten angezeigt werden (ParentId = node.Id)
        //    - Links, die dieses Objekt als Ziel verwenden (TargetObjectId = node.Id)
        var linksAsParent = await db.Links.Where(l => l.ParentId == node.Id).ToListAsync();
        var linksAsTarget = await db.Links.Where(l => l.TargetObjectId == node.Id).ToListAsync();
        db.Links.RemoveRange(linksAsParent);
        db.Links.RemoveRange(linksAsTarget);

        // 1) direkte Kinder behandeln (inkl. optionaler Zugangsdaten)
        foreach (var child in node.Children.ToList())
        {
            // vor dem Kind ebenfalls Links bereinigen (als Parent ODER Target)
            var childLinksAsParent = await db.Links.Where(l => l.ParentId == child.Id).ToListAsync();
            var childLinksAsTarget = await db.Links.Where(l => l.TargetObjectId == child.Id).ToListAsync();
            db.Links.RemoveRange(childLinksAsParent);
            db.Links.RemoveRange(childLinksAsTarget);

            if (child.NodeType == NodeType.Zugangsdaten)
            {
                var secret = await db.Secrets.FirstOrDefaultAsync(s => s.ObjectId == child.Id);
                if (secret != null) db.Secrets.Remove(secret);

                var childFiles = await db.Files.Where(f => f.ObjectId == child.Id).ToListAsync();
                db.Files.RemoveRange(childFiles);

                db.Objects.Remove(child);
            }
            else
            {
                // Dateien unter dem Kind löschen (du nutzt NoAction an FKs, also explizit bereinigen)
                var childFiles = await db.Files.Where(f => f.ObjectId == child.Id).ToListAsync();
                db.Files.RemoveRange(childFiles);

                db.Objects.Remove(child);
            }
        }

        // 2) Dateien des Knotens selbst löschen
        var files = await db.Files.Where(f => f.ObjectId == node.Id).ToListAsync();
        db.Files.RemoveRange(files);

        // 3) Knoten löschen
        db.Objects.Remove(node);

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        TempData["Info"] = "Knoten inkl. direkter Unterstruktur und Verknüpfungen gelöscht.";
        return RedirectToAction(nameof(Index));
    }
    private async Task CreateCredentialsChildAsync(DokuObjectEditVm vm, DokuObject website)
    {
        // max. Tiefe prüfen (Website.Level+1 <= 5)
        if (website.Level >= 10)
        {
            TempData["Info"] = "Zugangsdaten konnten nicht angelegt werden (max. Tiefe erreicht).";
            return;
        }

        var child = new DokuObject
        {
            Name = "Zugangsdaten",
            NodeType = NodeType.Zugangsdaten,
            ParentId = website.Id,
            Level = website.Level + 1,
            SortOrder = 0,
            Description = vm.CredNotes
        };
        db.Objects.Add(child);
        await db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(vm.CredPassword))
        {
            db.Secrets.Add(new DokuSecret
            {
                ObjectId = child.Id,
                Username = vm.CredUsername,
                PasswordEnc = protector.Protect(vm.CredPassword),
                Notes = vm.CredNotes,
                CreatedBy = User?.Identity?.Name
            });
            await db.SaveChangesAsync();
        }
    }

    // GET: /Objects/Search?term=abc&excludeId={guid}&take=20
    [HttpGet]
    public async Task<IActionResult> Search(string term, Guid? excludeId, int take = 20)
    {
        if (string.IsNullOrWhiteSpace(term))
            return Json(Array.Empty<object>());

        take = Math.Clamp(take, 5, 100);

        var q = db.Objects.AsNoTracking()
            .Where(o => EF.Functions.Like(o.Name, $"%{term}%"));

        if (excludeId is Guid ex && ex != Guid.Empty)
            q = q.Where(o => o.Id != ex);

        var items = await q
            .OrderBy(o => o.Name)
            .Take(take)
            .Select(o => new { id = o.Id, text = o.Name })
            .ToListAsync();

        return Json(items);
    }

    private async Task<bool> ShouldShowNetworkSectionAsync(DokuObject? node)
    {
        if (node == null) return false;

        // Sichtbar bei Struktur-Typ: Infrastruktur, Geräte, Server (auch wenn man darunter klickt)
        if (node.NodeType == NodeType.Infrastruktur ||
            node.NodeType == NodeType.Geräte ||
            node.NodeType == NodeType.Server)
            return true;

        // Vorfahren hochlaufen
        Guid? pid = node.ParentId;
        while (pid != null)
        {
            var p = await _db.Objects
                .AsNoTracking()
                .Where(x => x.Id == pid.Value)
                .Select(x => new { x.ParentId, x.NodeType })
                .FirstOrDefaultAsync();

            if (p == null) break;

            if (p.NodeType == NodeType.Infrastruktur ||
                p.NodeType == NodeType.Geräte ||
                p.NodeType == NodeType.Server)
                return true;

            pid = p.ParentId;
        }
        return false;
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Repoint(Guid id, Guid newTargetId)
    {
        if (id == Guid.Empty || newTargetId == Guid.Empty) return BadRequest();

        var link = await db.Links.FirstOrDefaultAsync(l => l.Id == id);
        if (link == null) return NotFound();

        if (!await db.Objects.AnyAsync(o => o.Id == newTargetId))
            return NotFound("Neues Ziel existiert nicht.");
        if (link.ParentId == newTargetId)
            return BadRequest("Parent und Target dürfen nicht identisch sein.");

        var duplicate = await db.Links.AnyAsync(l =>
            l.ParentId == link.ParentId && l.TargetObjectId == newTargetId && l.Id != id);
        if (duplicate) return Conflict("Dieser Link existiert bereits.");

        link.TargetObjectId = newTargetId;
        await db.SaveChangesAsync();
        return Ok();
    }

}
