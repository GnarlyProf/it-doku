using ITDoku.Models;
using System.ComponentModel.DataAnnotations;

public class DeviceIpEditVm
{
    public int? DeviceIpId { get; set; } // null = Create
    [Required] public Guid DokuObjectId { get; set; }

    [Required, MaxLength(50)] public string IpAddress { get; set; } = string.Empty;
    [MaxLength(50)] public string? SubnetMask { get; set; }
    [MaxLength(50)] public string? Gateway { get; set; }
    [Required] public IpAssignmentType AssignmentType { get; set; }
    public int? VlanId { get; set; }
    [MaxLength(50)] public string? InterfaceName { get; set; }

    // Anzeigezwecke
    public string? DokuObjectName { get; set; }
}