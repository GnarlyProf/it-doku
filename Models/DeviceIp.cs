using ITDoku.Models;

public class DeviceIp
{
    public int DeviceIpId { get; set; }

    public Guid DokuObjectId { get; set; }              // <— Guid!
    public DokuObject DokuObject { get; set; } = default!;

    public string IpAddress { get; set; } = default!;
    public string? SubnetMask { get; set; }
    public string? Gateway { get; set; }
    public IpAssignmentType AssignmentType { get; set; }
    public int? VlanId { get; set; }
    public string? InterfaceName { get; set; }
}
