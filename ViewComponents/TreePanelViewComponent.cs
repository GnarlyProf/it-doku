using ITDoku.Data;
using ITDoku.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITDoku.TreePanel;

public class TreePanelViewComponent : ViewComponent
{
    private readonly AppDbContext _db;
    public TreePanelViewComponent(AppDbContext db) => _db = db;

    public async Task<IViewComponentResult> InvokeAsync(Guid? currentId = null)
    {
        // 1) Alle Objekte laden
        var items = await _db.Objects
            .AsNoTracking()
            .OrderBy(o => o.SortOrder).ThenBy(o => o.Name)
            .ToListAsync();

        // Map & Children-Listen vorbereiten
        var byId = items.ToDictionary(x => x.Id);
        foreach (var o in items) o.Children = new List<DokuObject>();

        // 2) Reale Parent/Child-Verknüpfung
        var roots = new List<DokuObject>();
        foreach (var o in items)
        {
            if (o.ParentId is Guid pid && byId.TryGetValue(pid, out var parent))
                parent.Children!.Add(o);
            else
                roots.Add(o);
        }

        // 3) Virtuelle Links laden und als zusätzliche Kinder einfügen
        var links = await _db.Links
            .AsNoTracking()
            .Select(l => new { l.Id, l.ParentId, TargetId = l.TargetObjectId }) // <— Zielspalte anpassen falls abweichend
            .ToListAsync();

        foreach (var l in links)
        {
            if (byId.TryGetValue(l.ParentId, out var parent) && byId.TryGetValue(l.TargetId, out var target))
            {
                // Ziel als "virtuelles" Kind unter dem Link-Parent einhängen (Duplikate vermeiden)
                if (!parent.Children!.Any(c => c.Id == target.Id))
                    parent.Children!.Add(target);
            }
        }

        // 4) Offene Knoten = Ahnen des aktuellen Objekts
        var openIds = new HashSet<Guid>();
        if (currentId is Guid cur && byId.TryGetValue(cur, out var curNode))
        {
            var p = curNode.ParentId;
            while (p is Guid pid && byId.TryGetValue(pid, out var par))
            {
                openIds.Add(par.Id);
                p = par.ParentId;
            }
        }

        // 5) LinkMap für das _Tree-Partial, damit es Links kennzeichnen kann
        var linkMap = links.ToDictionary(x => (x.ParentId, x.TargetId), x => x.Id);

        var vm = new TreePanelVm
        {
            Roots = roots,
            CurrentId = currentId,
            LinkMap = linkMap,
            OpenIds = openIds
        };

        return View(vm);
    }
}

public class TreePanelVm
{
    public IEnumerable<DokuObject> Roots { get; set; } = Enumerable.Empty<DokuObject>();
    public Guid? CurrentId { get; set; }
    public IDictionary<(Guid ParentId, Guid TargetId), Guid> LinkMap { get; set; } =
        new Dictionary<(Guid, Guid), Guid>();
    public ISet<Guid> OpenIds { get; set; } = new HashSet<Guid>();
}
