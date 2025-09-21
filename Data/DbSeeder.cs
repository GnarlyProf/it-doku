using ITDoku.Models;
using Microsoft.EntityFrameworkCore;

namespace ITDoku.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Objects.AnyAsync()) return;
        var root = new DokuObject { Name = "IT_Doku", Level = 0, NodeType = NodeType.Infrastruktur };
        db.Objects.Add(root);

        DokuObject Make(string name, int level, DokuObject parent) => new() { Name = name, Level = level, Parent = parent, NodeType = NodeType.Ordner };

        var ruf = Make("(0) Rufnummern", 1, root);
        var ablage = Make("Ablage", 1, root);
        var admin = Make("Administration", 1, root);
        var antraege = Make("Anträge", 1, root);
        var hw = Make("Hardware", 1, root);
        var netz = Make("Netzwerk", 1, root);
        var dh01 = Make("DH01", 2, netz);
        var dvt03 = Make("DVT-DC03", 3, dh01);
        var dvt06 = Make("DVT-DC06", 3, dh01);
        db.Objects.AddRange(ruf, ablage, admin, antraege, hw, netz, dh01, dvt03, dvt06);

        await db.SaveChangesAsync();
    }
}
