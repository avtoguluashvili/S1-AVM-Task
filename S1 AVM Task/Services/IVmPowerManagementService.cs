using S1_AVM_Task.Models;

namespace S1_AVM_Task.Services;

/// <summary>
/// Interface for VM power management operations
/// </summary>
public interface IVmPowerManagementService
{
    Task ProcessVmPowerManagementAsync(List<VmInfo> vmInfos, CancellationToken cancellationToken = default);
}




