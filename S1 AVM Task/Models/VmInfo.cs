namespace S1_AVM_Task.Models;

/// <summary>
/// Represents information about an Azure Virtual Machine
/// </summary>
public class VmInfo
{
    public DateTime Timestamp { get; set; }
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string ComputerName { get; set; } = string.Empty;
    public string PowerState { get; set; } = string.Empty;
    public bool HasAutoShutdownTag { get; set; }
    public string VmId { get; set; } = string.Empty;
    public DateTime? StartTime { get; set; }
}