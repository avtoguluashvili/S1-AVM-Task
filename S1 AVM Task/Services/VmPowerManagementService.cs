using Microsoft.Extensions.Logging;
using S1_AVM_Task.Models;

namespace S1_AVM_Task.Services;

/// <summary>
/// Service for managing VM power state according to business rules
/// </summary>
public class VmPowerManagementService : IVmPowerManagementService
{
    private readonly IAzureVmService _azureVmService;
    private readonly ILogger<VmPowerManagementService> _logger;
    private readonly AppSettings _settings;

    public VmPowerManagementService(
        IAzureVmService azureVmService,
        ILogger<VmPowerManagementService> logger,
        Microsoft.Extensions.Options.IOptions<AppSettings> settings)
    {
        _azureVmService = azureVmService;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task ProcessVmPowerManagementAsync(List<VmInfo> vmInfos, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting power management processing for {Count} VMs", vmInfos.Count);

        // Filter VMs with AutoShutdown tag
        var autoShutdownVms = vmInfos.Where(vm => vm.HasAutoShutdownTag).ToList();
        
        _logger.LogInformation("Found {Count} VMs with AutoShutdown tag", autoShutdownVms.Count);

        if (!autoShutdownVms.Any())
        {
            return;
        }

        // Process VMs in parallel
        var tasks = autoShutdownVms.Select(vm => ProcessSingleVmAsync(vm, cancellationToken));
        await Task.WhenAll(tasks);

        _logger.LogInformation("Completed power management processing");
    }

    private async Task ProcessSingleVmAsync(VmInfo vmInfo, CancellationToken cancellationToken)
    {
        try
        {
            // Rule 1: If VM has been running for more than 8 hours, shut it down
            if (vmInfo.PowerState.Equals("Running", StringComparison.OrdinalIgnoreCase))
            {
                await HandleRunningVmAsync(vmInfo, cancellationToken);
            }
            // Rule 2: If VM is allocated but Windows is shutdown (stopped), deallocate it
            else if (IsAllocatedButStopped(vmInfo.PowerState))
            {
                await HandleStoppedVmAsync(vmInfo, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing power management for VM {VmName}", vmInfo.ComputerName);
        }
    }

    private async Task HandleRunningVmAsync(VmInfo vmInfo, CancellationToken cancellationToken)
    {
        if (vmInfo.StartTime.HasValue)
        {
            var runningDuration = vmInfo.Timestamp - vmInfo.StartTime.Value;
            var maxRunningHours = TimeSpan.FromHours(_settings.MaxRunningHours);

            if (runningDuration >= maxRunningHours)
            {
                _logger.LogWarning(
                    "VM {VmName} has been running for {Hours:F2} hours (threshold: {MaxHours} hours). Initiating shutdown.",
                    vmInfo.ComputerName,
                    runningDuration.TotalHours,
                    _settings.MaxRunningHours);

                await _azureVmService.ShutdownVmAsync(
                    vmInfo.SubscriptionId,
                    vmInfo.ResourceGroup,
                    vmInfo.ComputerName,
                    cancellationToken);
            }
            else
            {
                _logger.LogDebug(
                    "VM {VmName} has been running for {Hours:F2} hours (threshold: {MaxHours} hours). No action needed.",
                    vmInfo.ComputerName,
                    runningDuration.TotalHours,
                    _settings.MaxRunningHours);
            }
        }
    }

    private async Task HandleStoppedVmAsync(VmInfo vmInfo, CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "VM {VmName} is in state '{PowerState}' (allocated but stopped). Initiating deallocation.",
            vmInfo.ComputerName,
            vmInfo.PowerState);

        await _azureVmService.DeallocateVmAsync(
            vmInfo.SubscriptionId,
            vmInfo.ResourceGroup,
            vmInfo.ComputerName,
            cancellationToken);
    }

    private static bool IsAllocatedButStopped(string powerState)
    {
        // VM is allocated but not running (Windows is shutdown but resources are still allocated)
        // This typically shows as "stopped" state (not "deallocated")
        return powerState.Equals("stopped", StringComparison.OrdinalIgnoreCase);
    }
}