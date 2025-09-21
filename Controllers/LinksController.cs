using ITDoku.Data;
using ITDoku.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITDoku.Controllers;

[AutoValidateAntiforgeryToken] // alle unsafe HTTP-Methoden abgesichert
public class LinksController : Controller
{
    private readonly AppDbContext db;
    public LinksController(AppDbContext db) => this.db = db;

    // POST: /Links/Create
    [HttpPost]
    public async Task<IActionResult> Create(Guid parentId, Guid targetId, string? returnUrl = null)
    {
        // Soft-Validierungen
        if (parentId == Guid.Empty || targetId == Guid.Empty)
            return BadRequest("Ungültige Ids.");
        if (parentId == targetId)
            return BadRequest("Parent und Target dürfen nicht identisch sein.");

        // Existenz prüfen (verhindert "Zombie"-Links)
        var existsBoth = await db.Objects.AsNoTracking()
            .Where(o => o.Id == parentId || o.Id == targetId)
            .Select(o => o.Id).ToListAsync();

        if (!existsBoth.Contains(parentId) || !existsBoth.Contains(targetId))
            return NotFound();

        // Upsert-ähnlich: Versuchen zu inserten; Unique-Index (ParentId, TargetObjectId) verhindert Dubletten
        db.Links.Add(new DokuLink
        {
            Id = Guid.NewGuid(),
            ParentId = parentId,
            TargetObjectId = targetId
        });

        try
        {
            await db.SaveChangesAsync();
            TempData["Info"] = "Link erstellt.";
        }
        catch (DbUpdateException)
        {
            // Doppelter Link (Unique-Index) oder anderer DB-Fehler
            // Wenn es "nur" ein Duplikat war, ist der gewünschte Endzustand bereits erreicht.
            TempData["Info"] = "Link existierte bereits.";
        }

        return RedirectBack(returnUrl, parentId);
    }

    // POST: /Links/Delete/{id:guid}
    [HttpPost]
    public async Task<IActionResult> Delete(Guid id, string? returnUrl = null)
    {
        if (id == Guid.Empty) return BadRequest();

        // Nur Id bekannt -> effizient anhängen und löschen
        // (Falls du noch ParentId für Rückleitung brauchst, vorher laden.)
        var link = await db.Links.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id);

        if (link == null) return NotFound();

        // Entity mit Id anhängen und löschen (keine Cascades konfiguriert)
        db.Entry(new DokuLink { Id = id }).State = EntityState.Deleted;

        try
        {
            await db.SaveChangesAsync();
            TempData["Info"] = "Link gelöscht.";
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "Link konnte nicht gelöscht werden.";
        }

        return RedirectBack(returnUrl, link.ParentId);
    }

    // Optional: /Links/DeleteByComposite
    [HttpPost]
    public async Task<IActionResult> DeleteByComposite(Guid parentId, Guid targetId, string? returnUrl = null)
    {
        var link = await db.Links.FirstOrDefaultAsync(l => l.ParentId == parentId && l.TargetObjectId == targetId);
        if (link == null) return NotFound();

        db.Links.Remove(link);
        await db.SaveChangesAsync();
        TempData["Info"] = "Link gelöscht.";

        return RedirectBack(returnUrl, parentId);
    }

    private IActionResult RedirectBack(string? returnUrl, Guid parentId)
    {
        // 1) returnUrl (nur lokal zulassen)
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        // 2) Referer (falls lokal)
        if (Request.Headers.TryGetValue("Referer", out var referer) && Url.IsLocalUrl(referer!))
            return Redirect(referer!);

        // 3) Fallback: zurück zum Parent-Objekt
        return RedirectToAction("Index", "Objects", new { id = parentId });
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

        // Prüfe Unique-Constraint (ParentId, TargetObjectId)
        bool duplicate = await db.Links.AnyAsync(l =>
            l.ParentId == link.ParentId && l.TargetObjectId == newTargetId && l.Id != id);
        if (duplicate)
            return Conflict("Dieser Link existiert bereits.");

        link.TargetObjectId = newTargetId;

        try
        {
            await db.SaveChangesAsync();
            return Ok();
        }
        catch (DbUpdateException)
        {
            return StatusCode(500, "Speichern fehlgeschlagen.");
        }
    }

}
