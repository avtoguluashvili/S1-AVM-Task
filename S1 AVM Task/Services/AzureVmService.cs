using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;
using S1_AVM_Task.Models;
using System.Collections.Concurrent;

namespace S1_AVM_Task.Services;

/// <summary>
/// Service for interacting with Azure VM API
/// </summary>
public class AzureVmService : IAzureVmService
{
    private readonly ArmClient _armClient;
    private readonly ILogger<AzureVmService> _logger;
    private readonly AppSettings _settings;
    private readonly ConcurrentDictionary<string, DateTime> _vmStartTimes = new();

    public AzureVmService(ILogger<AzureVmService> logger, Microsoft.Extensions.Options.IOptions<AppSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
        
        try
        {
            // Use DefaultAzureCredential with multiple fallback options
            _logger.LogInformation("Attempting to authenticate with Azure...");
            
            var credentialOptions = new DefaultAzureCredentialOptions
            {
                ExcludeEnvironmentCredential = false,
                ExcludeWorkloadIdentityCredential = true,
                ExcludeManagedIdentityCredential = true,
                ExcludeSharedTokenCacheCredential = false,
                ExcludeVisualStudioCodeCredential = false,
                ExcludeVisualStudioCredential = false,
                ExcludeAzureCliCredential = false,
                ExcludeAzurePowerShellCredential = false,
                ExcludeInteractiveBrowserCredential = true
            };
            
            var credential = new DefaultAzureCredential(credentialOptions);
            _armClient = new ArmClient(credential);
            
            _logger.LogInformation("Azure ARM Client initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Azure ARM Client");
            _logger.LogWarning("Make sure you're authenticated with 'az login' or Connect-AzAccount");
            throw;
        }
    }

    public async Task<List<VmInfo>> GetAllVmsAsync(CancellationToken cancellationToken = default)
    {
        var vmInfoList = new ConcurrentBag<VmInfo>();
        var timestamp = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Starting to collect VM information across all subscriptions");

            List<SubscriptionResource> subscriptionList = new();

            // If a specific subscription is configured, use it directly
            if (!string.IsNullOrEmpty(_settings.SubscriptionId))
            {
                _logger.LogInformation("Using configured subscription ID: {SubscriptionId}", _settings.SubscriptionId);
                try
                {
                    var subscriptionId = new ResourceIdentifier($"/subscriptions/{_settings.SubscriptionId}");
                    var subscription = await _armClient.GetSubscriptionResource(subscriptionId).GetAsync(cancellationToken);
                    subscriptionList.Add(subscription.Value);
                    _logger.LogInformation("Successfully accessed subscription: {Name}", subscription.Value.Data.DisplayName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to access configured subscription {SubscriptionId}", _settings.SubscriptionId);
                    throw;
                }
            }
            else
            {
                // Get all subscriptions
                _logger.LogInformation("Enumerating all subscriptions...");
                var subscriptions = _armClient.GetSubscriptions();
                
                await foreach (var subscription in subscriptions.GetAllAsync(cancellationToken: cancellationToken))
                {
                    _logger.LogInformation("Found subscription: {SubscriptionId} - {SubscriptionName}", 
                        subscription.Data.SubscriptionId, subscription.Data.DisplayName);
                    subscriptionList.Add(subscription);
                }
                
                _logger.LogInformation("Total subscriptions enumerated: {Count}", subscriptionList.Count);
            }
            
            if (subscriptionList.Count == 0)
            {
                _logger.LogWarning("No subscriptions found. This might be a permissions issue.");
                _logger.LogWarning("Make sure the authenticated account has Reader access to subscriptions.");
                _logger.LogWarning("You can also specify a SubscriptionId in appsettings.json");
                return vmInfoList.ToList();
            }

            _logger.LogInformation("Found {Count} subscriptions", subscriptionList.Count);

            // Process subscriptions in parallel for better performance
            await Parallel.ForEachAsync(subscriptionList, 
                new ParallelOptions 
                { 
                    MaxDegreeOfParallelism = 10, 
                    CancellationToken = cancellationToken 
                },
                async (subscription, ct) =>
                {
                    try
                    {
                        await ProcessSubscriptionAsync(subscription, vmInfoList, timestamp, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing subscription {SubscriptionId}", subscription.Data.SubscriptionId);
                    }
                });

            _logger.LogInformation("Collected information for {Count} VMs", vmInfoList.Count);
            return vmInfoList.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all VMs");
            throw;
        }
    }

    private async Task ProcessSubscriptionAsync(
        SubscriptionResource subscription, 
        ConcurrentBag<VmInfo> vmInfoList, 
        DateTime timestamp,
        CancellationToken cancellationToken)
    {
        var subscriptionId = subscription.Data.SubscriptionId;
        _logger.LogDebug("Processing subscription: {SubscriptionId}", subscriptionId);

        // Get all VMs in the subscription using a single API call
        var vmResources = subscription.GetVirtualMachinesAsync(cancellationToken: cancellationToken);

        await foreach (var vm in vmResources)
        {
            try
            {
                var vmInfo = await CreateVmInfoAsync(vm, subscriptionId, timestamp, cancellationToken);
                vmInfoList.Add(vmInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing VM {VmName} in subscription {SubscriptionId}", 
                    vm.Data.Name, subscriptionId);
            }
        }
    }

    private async Task<VmInfo> CreateVmInfoAsync(
        VirtualMachineResource vm, 
        string subscriptionId, 
        DateTime timestamp,
        CancellationToken cancellationToken)
    {
        var vmData = vm.Data;
        var resourceGroup = GetResourceGroupFromId(vm.Id);
        var powerState = "Unknown";
        
        // Get instance view to retrieve power state
        try
        {
            var instanceView = await vm.InstanceViewAsync(cancellationToken);
            powerState = GetPowerState(instanceView.Value);
            
            // Track VM start times for VMs that are running
            var vmKey = $"{subscriptionId}/{resourceGroup}/{vmData.Name}";
            if (powerState == "Running")
            {
                _vmStartTimes.TryAdd(vmKey, timestamp);
            }
            else
            {
                _vmStartTimes.TryRemove(vmKey, out _);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not get instance view for VM {VmName}", vmData.Name);
        }

        var hasAutoShutdownTag = HasAutoShutdownTag(vmData.Tags);

        var vmInfo = new VmInfo
        {
            Timestamp = timestamp,
            SubscriptionId = subscriptionId,
            ResourceGroup = resourceGroup,
            ComputerName = vmData.Name,
            PowerState = powerState,
            HasAutoShutdownTag = hasAutoShutdownTag,
            VmId = vm.Id.ToString(),
            StartTime = _vmStartTimes.TryGetValue($"{subscriptionId}/{resourceGroup}/{vmData.Name}", out var startTime) 
                ? startTime 
                : null
        };

        return vmInfo;
    }

    private static string GetPowerState(VirtualMachineInstanceView instanceView)
    {
        var powerState = instanceView.Statuses?
            .FirstOrDefault(s => s.Code?.StartsWith("PowerState/") == true)?
            .Code?
            .Replace("PowerState/", "");

        return powerState ?? "Unknown";
    }

    private bool HasAutoShutdownTag(IDictionary<string, string>? tags)
    {
        if (tags == null || !tags.Any())
            return false;

        return tags.TryGetValue(_settings.AutoShutdownTagName, out var value) 
            && value == _settings.AutoShutdownTagValue;
    }

    private static string GetResourceGroupFromId(ResourceIdentifier resourceId)
    {
        // Resource ID format: /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/...
        var segments = resourceId.ToString().Split('/');
        var rgIndex = Array.IndexOf(segments, "resourceGroups");
        return rgIndex >= 0 && rgIndex + 1 < segments.Length ? segments[rgIndex + 1] : string.Empty;
    }

    public async Task ShutdownVmAsync(string subscriptionId, string resourceGroup, string vmName, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Shutting down VM: {VmName} in resource group {ResourceGroup}", vmName, resourceGroup);
            
            var subscription = await _armClient.GetSubscriptionResource(
                new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync(cancellationToken);
            
            var resourceGroupResource = await subscription.Value.GetResourceGroupAsync(resourceGroup, cancellationToken);
            var vmCollection = resourceGroupResource.Value.GetVirtualMachines();
            var vm = await vmCollection.GetAsync(vmName, null, cancellationToken);
            
            await vm.Value.PowerOffAsync(WaitUntil.Started, skipShutdown: null, cancellationToken);
            
            _logger.LogInformation("Successfully initiated shutdown for VM: {VmName}", vmName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to shutdown VM {VmName}", vmName);
            throw;
        }
    }

    public async Task DeallocateVmAsync(string subscriptionId, string resourceGroup, string vmName, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Deallocating VM: {VmName} in resource group {ResourceGroup}", vmName, resourceGroup);
            
            var subscription = await _armClient.GetSubscriptionResource(
                new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync(cancellationToken);
            
            var resourceGroupResource = await subscription.Value.GetResourceGroupAsync(resourceGroup, cancellationToken);
            var vmCollection = resourceGroupResource.Value.GetVirtualMachines();
            var vm = await vmCollection.GetAsync(vmName, null, cancellationToken);
            
            await vm.Value.DeallocateAsync(WaitUntil.Started, hibernate: null, cancellationToken);
            
            _logger.LogInformation("Successfully initiated deallocation for VM: {VmName}", vmName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deallocate VM {VmName}", vmName);
            throw;
        }
    }
}

