using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using S1_AVM_Task.Models;
using S1_AVM_Task.Services;

namespace S1_AVM_Task.Workers;

/// <summary>
/// Background service that polls Azure VMs at regular intervals
/// </summary>
public class VmMonitoringWorker : BackgroundService
{
    private readonly IAzureVmService _azureVmService;
    private readonly ICsvWriterService _csvWriterService;
    private readonly IVmPowerManagementService _powerManagementService;
    private readonly ILogger<VmMonitoringWorker> _logger;
    private readonly AppSettings _settings;

    public VmMonitoringWorker(
        IAzureVmService azureVmService,
        ICsvWriterService csvWriterService,
        IVmPowerManagementService powerManagementService,
        ILogger<VmMonitoringWorker> logger,
        Microsoft.Extensions.Options.IOptions<AppSettings> settings)
    {
        _azureVmService = azureVmService;
        _csvWriterService = csvWriterService;
        _powerManagementService = powerManagementService;
        _logger = logger;
        _settings = settings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("VM Monitoring Worker started. Polling interval: {Interval} minutes", 
            _settings.PollingIntervalMinutes);

        // Wait a bit before starting to allow services to initialize
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting VM polling cycle at {Time}", DateTime.UtcNow);
                var cycleStartTime = DateTime.UtcNow;

                // 1. Collect VM information
                var vmInfos = await _azureVmService.GetAllVmsAsync(stoppingToken);

                if (vmInfos.Any())
                {
                    // 2. Write to CSV
                    await _csvWriterService.WriteVmInfoAsync(vmInfos, stoppingToken);

                    // 3. Process power management rules
                    await _powerManagementService.ProcessVmPowerManagementAsync(vmInfos, stoppingToken);
                }
                else
                {
                    _logger.LogWarning("No VMs found in any subscription");
                }

                var cycleDuration = DateTime.UtcNow - cycleStartTime;
                _logger.LogInformation("VM polling cycle completed in {Duration:F2} seconds", 
                    cycleDuration.TotalSeconds);

                // Wait for the next polling interval
                var delayTime = TimeSpan.FromMinutes(_settings.PollingIntervalMinutes);
                _logger.LogInformation("Next polling cycle in {Minutes} minutes", 
                    _settings.PollingIntervalMinutes);
                
                await Task.Delay(delayTime, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("VM Monitoring Worker cancellation requested");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during VM polling cycle. Will retry in next cycle.");
                
                // Even on error, wait before retrying
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(_settings.PollingIntervalMinutes), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("VM Monitoring Worker cancellation requested during error recovery");
                    break;
                }
            }
        }

        _logger.LogInformation("VM Monitoring Worker stopped");
    }
}

