using Microsoft.Extensions.Logging;
using S1_AVM_Task.Models;
using System.Globalization;
using System.Text;

namespace S1_AVM_Task.Services;

/// <summary>
/// Service for writing VM information to CSV file
/// </summary>
public class CsvWriterService : ICsvWriterService
{
    private readonly string _csvFilePath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ILogger<CsvWriterService> _logger;

    public CsvWriterService(ILogger<CsvWriterService> logger, Microsoft.Extensions.Options.IOptions<AppSettings> settings)
    {
        _logger = logger;
        _csvFilePath = settings.Value.CsvFilePath;
        EnsureHeaderExists();
    }

    private void EnsureHeaderExists()
    {
        try
        {
            if (!File.Exists(_csvFilePath))
            {
                var header = "Timestamp,SubscriptionId,ResourceGroup,ComputerName,PowerState";
                File.WriteAllText(_csvFilePath, header + Environment.NewLine);
                _logger.LogInformation("Created CSV file at {FilePath}", _csvFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create CSV file header");
            throw;
        }
    }

    public async Task WriteVmInfoAsync(IEnumerable<VmInfo> vmInfos, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var sb = new StringBuilder();
            foreach (var vmInfo in vmInfos)
            {
                sb.AppendLine(FormatCsvLine(vmInfo));
            }

            if (sb.Length > 0)
            {
                await File.AppendAllTextAsync(_csvFilePath, sb.ToString(), cancellationToken);
                _logger.LogInformation("Written {Count} VM records to CSV", vmInfos.Count());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write VM information to CSV");
            throw;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static string FormatCsvLine(VmInfo vmInfo)
    {
        return $"{vmInfo.Timestamp:yyyy-MM-dd HH:mm:ss},{EscapeCsvField(vmInfo.SubscriptionId)},{EscapeCsvField(vmInfo.ResourceGroup)},{EscapeCsvField(vmInfo.ComputerName)},{EscapeCsvField(vmInfo.PowerState)}";
    }

    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
            return string.Empty;

        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }
}

