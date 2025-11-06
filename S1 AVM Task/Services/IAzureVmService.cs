using S1_AVM_Task.Models;

namespace S1_AVM_Task.Services;

/// <summary>
/// Interface for Azure VM operations
/// </summary>
public interface IAzureVmService
{
    Task<List<VmInfo>> GetAllVmsAsync(CancellationToken cancellationToken = default);
    Task ShutdownVmAsync(string subscriptionId, string resourceGroup, string vmName, CancellationToken cancellationToken = default);
    Task DeallocateVmAsync(string subscriptionId, string resourceGroup, string vmName, CancellationToken cancellationToken = default);
}




