using ITDoku.Models;
using Microsoft.EntityFrameworkCore;

namespace ITDoku.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<DokuObject> Objects => Set<DokuObject>();
    public DbSet<DokuFile> Files => Set<DokuFile>();
    public DbSet<DokuFileVersion> FileVersions => Set<DokuFileVersion>();
    public DbSet<DokuSecret> Secrets => Set<DokuSecret>(); // NEU
    public DbSet<Network> Networks => Set<Network>();
    public DbSet<DeviceIp> DeviceIPs => Set<DeviceIp>();
    public DbSet<DokuLink> Links => Set<DokuLink>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // ---- DokuObject ----
        b.Entity<DokuObject>(e =>
        {
            e.ToTable("Objects");

            e.HasKey(x => x.Id);

            e.HasOne(x => x.Parent)
             .WithMany(x => x.Children)
             .HasForeignKey(x => x.ParentId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => new { x.ParentId, x.SortOrder });
            e.Property(x => x.Level).HasDefaultValue(0);
            e.HasCheckConstraint("CK_DokuObject_Level", "[Level] BETWEEN 0 AND 10");
            e.Property(x => x.Url).HasMaxLength(1000);
        });

        // ---- DokuFile ----
        b.Entity<DokuFile>(e =>
        {
            e.ToTable("Files");

            e.HasOne(x => x.DokuObject)
             .WithMany(x => x.Files)
             .HasForeignKey(x => x.ObjectId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.ObjectId);
        });

        // ---- DokuFileVersion ----
        b.Entity<DokuFileVersion>(e =>
        {
            e.ToTable("FileVersions");
            e.HasIndex(x => new { x.FileId, x.Version }).IsUnique();
        });

        // ---- DokuSecret ----
        b.Entity<DokuSecret>(e =>
        {
            e.ToTable("Secrets");
            e.HasOne(s => s.DokuObject)
             .WithMany()
             .HasForeignKey(s => s.ObjectId)   // Stelle sicher: DokuSecret.ObjectId ist GUID!
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(s => s.ObjectId).IsUnique();
        });

        // ---- Network ----
        b.Entity<Network>(e =>
        {
            e.ToTable("Networks");
            e.HasKey(x => x.NetworkId);

            e.Property(x => x.CidrNotation).HasMaxLength(50).IsRequired();
            e.Property(x => x.Description).HasMaxLength(255);
            e.Property(x => x.DhcpRangeStart).HasMaxLength(50);
            e.Property(x => x.DhcpRangeEnd).HasMaxLength(50);
            e.Property(x => x.DnsServer).HasMaxLength(50);

            e.HasIndex(x => x.CidrNotation).IsUnique();

            e.HasOne(x => x.AssignedToDokuObject)
             .WithMany()
             .HasForeignKey(x => x.AssignedToDokuObjectId) // ✅ FK-Property, nicht die Navigation!
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- DeviceIp ----
        b.Entity<DeviceIp>(e =>
        {
            e.ToTable("DeviceIPs");
            e.HasKey(x => x.DeviceIpId);

            e.Property(x => x.IpAddress).HasMaxLength(50).IsRequired();
            e.Property(x => x.SubnetMask).HasMaxLength(50);
            e.Property(x => x.Gateway).HasMaxLength(50);
            e.Property(x => x.InterfaceName).HasMaxLength(50);

            e.Property(x => x.AssignmentType)
             .HasConversion<int>() // speichert Enum als int
             .IsRequired();

            e.HasIndex(x => x.DokuObjectId);
            e.HasIndex(x => new { x.DokuObjectId, x.IpAddress }).IsUnique();

            e.HasOne(x => x.DokuObject)
             .WithMany(o => o.DeviceIps)   // <-- hier die Gegen-Navigation explizit angeben
             .HasForeignKey(x => x.DokuObjectId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<DokuLink>(e =>
        {
            e.ToTable("Links");                         // dbo.Links
            e.HasKey(x => x.Id);

            // Pro Parent nur 1x dasselbe Target zulassen
            e.HasIndex(x => new { x.ParentId, x.TargetObjectId }).IsUnique();

            // WICHTIG: Kein Cascade, sonst „multiple cascade paths“
            e.HasOne(x => x.Parent)
             .WithMany()
             .HasForeignKey(x => x.ParentId)
             .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(x => x.TargetObject)
             .WithMany()
             .HasForeignKey(x => x.TargetObjectId)
             .OnDelete(DeleteBehavior.NoAction);
        });

        base.OnModelCreating(b);
    }
}
