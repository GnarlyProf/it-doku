using ITDoku.Data;
using ITDoku.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;

public class DeviceIpsController : Controller
{
    private readonly AppDbContext _db;
    public DeviceIpsController(AppDbContext db) => _db = db;

    // /DeviceIps/ForObject/{dokuObjectId}
    [HttpGet]
    public async Task<IActionResult> ForObject(Guid dokuObjectId)
    {
        var obj = await _db.Objects.AsNoTracking().FirstOrDefaultAsync(o => o.Id == dokuObjectId);
        if (obj == null) return NotFound();

        var ips = await _db.DeviceIPs
            .Where(x => x.DokuObjectId == dokuObjectId)
            .OrderBy(x => x.InterfaceName)
            .ThenBy(x => x.IpAddress)
            .ToListAsync();

        ViewBag.ObjectId = obj.Id;
        ViewBag.ObjectName = obj.Name;
        ViewBag.CurrentObjectId = obj.Id;     
        return View(ips);
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid dokuObjectId)
    {
        var obj = await _db.Objects.AsNoTracking().FirstOrDefaultAsync(o => o.Id == dokuObjectId);
        if (obj == null) return NotFound();

        var vm = new DeviceIpEditVm
        {
            DokuObjectId = dokuObjectId,
            DokuObjectName = obj.Name,
            AssignmentType = IpAssignmentType.DHCP
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DeviceIpEditVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        // Validierungen (siehe Punkt 3) können hier zusätzlich greifen
        var entity = new DeviceIp
        {
            DokuObjectId = vm.DokuObjectId,
            IpAddress = vm.IpAddress,
            SubnetMask = vm.SubnetMask,
            Gateway = vm.Gateway,
            AssignmentType = vm.AssignmentType,
            VlanId = vm.VlanId,
            InterfaceName = vm.InterfaceName
        };
        _db.DeviceIPs.Add(entity);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(ForObject), new { dokuObjectId = vm.DokuObjectId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var e = await _db.DeviceIPs.Include(x => x.DokuObject).FirstOrDefaultAsync(x => x.DeviceIpId == id);
        if (e == null) return NotFound();

        var vm = new DeviceIpEditVm
        {
            DeviceIpId = e.DeviceIpId,
            DokuObjectId = e.DokuObjectId,
            DokuObjectName = e.DokuObject?.Name,
            IpAddress = e.IpAddress,
            SubnetMask = e.SubnetMask,
            Gateway = e.Gateway,
            AssignmentType = e.AssignmentType,
            VlanId = e.VlanId,
            InterfaceName = e.InterfaceName
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(DeviceIpEditVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var e = await _db.DeviceIPs.FirstOrDefaultAsync(x => x.DeviceIpId == vm.DeviceIpId);
        if (e == null) return NotFound();

        e.IpAddress = vm.IpAddress;
        e.SubnetMask = vm.SubnetMask;
        e.Gateway = vm.Gateway;
        e.AssignmentType = vm.AssignmentType;
        e.VlanId = vm.VlanId;
        e.InterfaceName = vm.InterfaceName;

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(ForObject), new { dokuObjectId = e.DokuObjectId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var e = await _db.DeviceIPs.FirstOrDefaultAsync(x => x.DeviceIpId == id);
        if (e == null) return NotFound();
        var back = e.DokuObjectId;
        _db.DeviceIPs.Remove(e);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(ForObject), new { dokuObjectId = back });
    }
}

public static class NetValidators
{
    public static bool IsValidIp(string? ip)
        => !string.IsNullOrWhiteSpace(ip) && IPAddress.TryParse(ip, out _);

    public static bool TryParseCidr(string cidr, out IPNetworkV4 net)
    {
        net = default;
        var parts = cidr.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return false;
        if (!IPAddress.TryParse(parts[0], out var ip)) return false;
        if (!int.TryParse(parts[1], out var prefix) || prefix < 0 || prefix > 32) return false;
        if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
        net = IPNetworkV4.From(ip, prefix);
        return true;
    }
}

public readonly record struct IPNetworkV4(IPAddress Network, int Prefix, IPAddress Netmask)
{
    public static IPNetworkV4 From(IPAddress ip, int prefix)
    {
        uint mask = prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);
        var maskBytes = BitConverter.GetBytes(mask).Reverse().ToArray();
        var ipBytes = ip.GetAddressBytes();
        var netBytes = new byte[4];
        for (int i = 0; i < 4; i++) netBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);
        return new IPNetworkV4(new IPAddress(netBytes), prefix, new IPAddress(maskBytes));
    }

    public bool Contains(IPAddress candidate)
    {
        var m = Netmask.GetAddressBytes();
        var n = Network.GetAddressBytes();
        var c = candidate.GetAddressBytes();
        for (int i = 0; i < 4; i++)
            if ((n[i] & m[i]) != (c[i] & m[i])) return false;
        return true;
    }
}

