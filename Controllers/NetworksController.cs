using ITDoku.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;

public class NetworksController : Controller
{
    private readonly AppDbContext _db;
    public NetworksController(AppDbContext db) => _db = db;

    // GET: /Networks
    // using System.Net;

    public async Task<IActionResult> Index()
    {
        var list = await _db.Networks
            .Include(n => n.AssignedToDokuObject)
            .AsNoTracking()
            .ToListAsync();

        static uint ToUInt32(IPAddress ip)
        {
            var bytes = ip.GetAddressBytes();
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }

        static bool TryParseCidrV4(string cidr, out IPAddress network, out int prefix)
        {
            network = IPAddress.None;
            prefix = 0;
            if (string.IsNullOrWhiteSpace(cidr)) return false;

            var parts = cidr.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return false;
            if (!IPAddress.TryParse(parts[0], out var ip)) return false;
            if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
            if (!int.TryParse(parts[1], out prefix) || prefix < 0 || prefix > 32) return false;

            uint mask = prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);

            var ipBytes = ip.GetAddressBytes();
            if (BitConverter.IsLittleEndian) Array.Reverse(ipBytes);
            uint ipu = BitConverter.ToUInt32(ipBytes, 0);

            uint netu = ipu & mask;
            var netBytes = BitConverter.GetBytes(netu);
            if (BitConverter.IsLittleEndian) Array.Reverse(netBytes);
            network = new IPAddress(netBytes);
            return true;
        }

        var ordered = list
            .Select(n =>
            {
                if (TryParseCidrV4(n.CidrNotation, out var net, out var p))
                    return (n, key: ToUInt32(net), prefix: p);
                // Unparsbare Einträge nach hinten
                return (n, key: uint.MaxValue, prefix: 33);
            })
            .OrderBy(x => x.key)
            .ThenBy(x => x.prefix)
            .Select(x => x.n)
            .ToList();

        return View(ordered);
    }

    // GET: /Networks/Create
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var vm = new NetworkEditVm
        {
            AssignableObjects = await _db.Objects
                .AsNoTracking()
                .OrderBy(o => o.Name)
                .Select(o => new ValueTuple<Guid, string>(o.Id, o.Name))
                .ToListAsync()
        };
        return View(vm);
    }

    // POST: /Networks/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(NetworkEditVm vm)
    {
        if (!ModelState.IsValid)
        {
            vm.AssignableObjects = await _db.Objects.AsNoTracking()
                .OrderBy(o => o.Name)
                .Select(o => new ValueTuple<Guid, string>(o.Id, o.Name))
                .ToListAsync();
            return View(vm);
        }
        if (!NetValidators.TryParseCidr(vm.CidrNotation, out var net))
            ModelState.AddModelError(nameof(vm.CidrNotation), "Ungültiges CIDR (z. B. 172.23.56.0/24).");

        if (!string.IsNullOrWhiteSpace(vm.DhcpRangeStart) && !NetValidators.IsValidIp(vm.DhcpRangeStart))
            ModelState.AddModelError(nameof(vm.DhcpRangeStart), "Ungültige IP-Adresse.");

        if (!string.IsNullOrWhiteSpace(vm.DhcpRangeEnd) && !NetValidators.IsValidIp(vm.DhcpRangeEnd))
            ModelState.AddModelError(nameof(vm.DhcpRangeEnd), "Ungültige IP-Adresse.");

        if (ModelState.IsValid && !string.IsNullOrWhiteSpace(vm.DhcpRangeStart) && !string.IsNullOrWhiteSpace(vm.DhcpRangeEnd))
        {
            var start = IPAddress.Parse(vm.DhcpRangeStart);
            var end = IPAddress.Parse(vm.DhcpRangeEnd);
            if (!net.Contains(start) || !net.Contains(end))
                ModelState.AddModelError("", "DHCP-Range liegt außerhalb des Netzwerks.");
        }

        var entity = new Network
        {
            CidrNotation = vm.CidrNotation,
            Description = vm.Description,
            DhcpRangeStart = vm.DhcpRangeStart,
            DhcpRangeEnd = vm.DhcpRangeEnd,
            DnsServer = vm.DnsServer,
            AssignedToDokuObjectId = vm.AssignedToDokuObjectId
        };
        _db.Networks.Add(entity);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // GET: /Networks/Edit/5
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var n = await _db.Networks.AsNoTracking().FirstOrDefaultAsync(x => x.NetworkId == id);
        var vm = new NetworkEditVm
        {
            NetworkId = n.NetworkId,
            CidrNotation = n.CidrNotation,
            Description = n.Description,
            DhcpRangeStart = n.DhcpRangeStart,
            DhcpRangeEnd = n.DhcpRangeEnd,
            DnsServer = n.DnsServer,
            AssignedToDokuObjectId = n.AssignedToDokuObjectId,
            AssignableObjects = await _db.Objects.AsNoTracking()
                .OrderBy(o => o.Name)
                .Select(o => new ValueTuple<Guid, string>(o.Id, o.Name))
                .ToListAsync()
        };
        return View(vm);
    }

    // POST: /Networks/Edit
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(NetworkEditVm vm)
    {
        if (!ModelState.IsValid)
        {
            vm.AssignableObjects = await _db.Objects.AsNoTracking()
                .OrderBy(o => o.Name)
                .Select(o => new ValueTuple<Guid, string>(o.Id, o.Name))
                .ToListAsync();
            return View(vm);
        }
        if (!NetValidators.TryParseCidr(vm.CidrNotation, out var net))
            ModelState.AddModelError(nameof(vm.CidrNotation), "Ungültiges CIDR (z. B. 172.23.56.0/24).");

        if (!string.IsNullOrWhiteSpace(vm.DhcpRangeStart) && !NetValidators.IsValidIp(vm.DhcpRangeStart))
            ModelState.AddModelError(nameof(vm.DhcpRangeStart), "Ungültige IP-Adresse.");

        if (!string.IsNullOrWhiteSpace(vm.DhcpRangeEnd) && !NetValidators.IsValidIp(vm.DhcpRangeEnd))
            ModelState.AddModelError(nameof(vm.DhcpRangeEnd), "Ungültige IP-Adresse.");

        if (ModelState.IsValid && !string.IsNullOrWhiteSpace(vm.DhcpRangeStart) && !string.IsNullOrWhiteSpace(vm.DhcpRangeEnd))
        {
            var start = IPAddress.Parse(vm.DhcpRangeStart);
            var end = IPAddress.Parse(vm.DhcpRangeEnd);
            if (!net.Contains(start) || !net.Contains(end))
                ModelState.AddModelError("", "DHCP-Range liegt außerhalb des Netzwerks.");
        }
        var n = await _db.Networks.FirstOrDefaultAsync(x => x.NetworkId == vm.NetworkId);
        if (n == null) return NotFound();

        n.CidrNotation = vm.CidrNotation;
        n.Description = vm.Description;
        n.DhcpRangeStart = vm.DhcpRangeStart;
        n.DhcpRangeEnd = vm.DhcpRangeEnd;
        n.DnsServer = vm.DnsServer;
        n.AssignedToDokuObjectId = vm.AssignedToDokuObjectId;

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // POST: /Networks/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var n = await _db.Networks.FirstOrDefaultAsync(x => x.NetworkId == id);
        if (n == null) return NotFound();
        _db.Networks.Remove(n);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
