namespace S1_AVM_Task.Models;

/// <summary>
/// Application configuration settings
/// </summary>
public class AppSettings
{
    public int PollingIntervalMinutes { get; set; } = 5;
    public int MaxRunningHours { get; set; } = 8;
    public string CsvFilePath { get; set; } = "vm_logs.csv";
    public string AutoShutdownTagName { get; set; } = "Autoshutdown";
    public string AutoShutdownTagValue { get; set; } = "1";
    public string? SubscriptionId { get; set; }
}




