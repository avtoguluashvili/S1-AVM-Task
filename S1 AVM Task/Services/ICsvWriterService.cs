using S1_AVM_Task.Models;

namespace S1_AVM_Task.Services;

/// <summary>
/// Interface for CSV logging operations
/// </summary>
public interface ICsvWriterService
{
    Task WriteVmInfoAsync(IEnumerable<VmInfo> vmInfos, CancellationToken cancellationToken = default);
}




