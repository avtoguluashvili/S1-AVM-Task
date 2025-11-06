# Azure VM Autoscheduler

A production-ready console application for monitoring and managing Azure Virtual Machines across multiple subscriptions with automatic power management capabilities.

## Features

- **Multi-Subscription Support**: Automatically discovers and monitors VMs across all Azure subscriptions in a tenant
- **Automated Polling**: Collects VM information every 5 minutes (configurable)
- **CSV Logging**: Maintains detailed logs of VM states with timestamps
- **Power Management**:
  - Automatically shuts down VMs running more than 8 hours (configurable)
  - Deallocates VMs that are allocated but stopped
  - Tag-based control with `Autoshutdown=1` tag
- **Performance Optimized**: 
  - Parallel processing of subscriptions and VMs
  - Efficient Azure API usage
  - Asynchronous operations throughout
- **Production-Ready**:
  - Comprehensive error handling
  - Structured logging with Serilog
  - Dependency injection
  - Clean architecture with separation of concerns

## Architecture

The application follows best practices and clean architecture principles:

```
S1 AVM Task/
├── Models/              # Data models
│   ├── VmInfo.cs       # VM information model
│   └── AppSettings.cs  # Configuration model
├── Services/           # Business logic and infrastructure
│   ├── IAzureVmService.cs / AzureVmService.cs
│   ├── ICsvWriterService.cs / CsvWriterService.cs
│   └── IVmPowerManagementService.cs / VmPowerManagementService.cs
├── Workers/            # Background services
│   └── VmMonitoringWorker.cs
├── Program.cs          # Application entry point
└── appsettings.json    # Configuration
```

## Prerequisites

- .NET 8.0 SDK or later
- Azure subscription(s)
- Azure credentials (one of):
  - Azure CLI (`az login`)
  - Azure PowerShell (`Connect-AzAccount`)
  - Managed Identity (when deployed to Azure)
  - Environment variables
  - Visual Studio/VS Code Azure authentication

## Installation

1. Clone the repository
2. Restore NuGet packages:
   ```bash
   dotnet restore
   ```

## Configuration

Configure the application via `appsettings.json`:

```json
{
  "AppSettings": {
    "PollingIntervalMinutes": 5,     // How often to check VMs
    "MaxRunningHours": 8,             // Max runtime before shutdown
    "CsvFilePath": "vm_logs.csv",     // Output CSV file path
    "AutoShutdownTagName": "Autoshutdown",
    "AutoShutdownTagValue": "1"
  }
}
```

## Usage

### Running the Application

```bash
dotnet run
```

Or build and run the executable:

```bash
dotnet build -c Release
cd bin/Release/net8.0
./S1_AVM_Task
```

### Authentication

The application uses `DefaultAzureCredential`, which attempts authentication in this order:

1. Environment variables
2. Managed Identity (when running in Azure)
3. Visual Studio/VS Code credentials
4. Azure CLI credentials
5. Azure PowerShell credentials

**Recommended**: Authenticate using Azure CLI before running:
```bash
az login
az account set --subscription "Your-Subscription-Name"
```

### Tagging VMs for Auto-Shutdown

To enable auto-shutdown for a VM, add the tag via Azure Portal, CLI, or ARM:

**Azure CLI:**
```bash
az vm update --resource-group MyResourceGroup --name MyVM --set tags.Autoshutdown=1
```

**Azure Portal:**
1. Navigate to your VM
2. Go to "Tags"
3. Add tag: `Autoshutdown` = `1`

## Output

### CSV File Format

The application generates `vm_logs.csv` with the following columns:

```csv
Timestamp,SubscriptionId,ResourceGroup,ComputerName,PowerState
2025-11-06 10:15:00,12345678-1234-1234-1234-123456789abc,MyResourceGroup,VM-WebServer-01,Running
2025-11-06 10:15:01,12345678-1234-1234-1234-123456789abc,MyResourceGroup,VM-Database-01,stopped
```

### Log Files

Application logs are written to:
- **Console**: Real-time operational logs
- **File**: `logs/application-YYYYMMDD.log` (retained for 7 days)

## Power Management Rules

The application applies these rules **only to VMs with the `Autoshutdown=1` tag**:

1. **Long-Running VMs**: If a VM has been running for more than 8 hours (configurable), it will be shut down
2. **Allocated but Stopped**: If a VM is in "stopped" state (allocated but not running), it will be deallocated to save costs

## Performance Considerations

- **Parallel Processing**: Subscriptions and VMs are processed in parallel (max 10 concurrent operations)
- **Efficient API Calls**: Uses bulk operations and instance views to minimize round trips
- **Async/Await**: Fully asynchronous for optimal resource utilization
- **Batched Operations**: Collects all data before writing to CSV in a single operation

## Error Handling

- Graceful error handling at all levels
- Errors are logged but don't crash the application
- Failed operations are retried on the next polling cycle
- Subscription-level errors don't affect other subscriptions
- VM-level errors don't affect other VMs

## Development

### Building

```bash
dotnet build
```

### Running Tests (when implemented)

```bash
dotnet test
```

## Dependencies

- **Azure.Identity**: Azure authentication
- **Azure.ResourceManager**: Azure Resource Manager SDK
- **Azure.ResourceManager.Compute**: VM management
- **Azure.ResourceManager.Resources**: Subscription and resource group access
- **Microsoft.Extensions.Hosting**: Background service hosting
- **Serilog**: Structured logging

## Deployment

### As a Windows Service

```bash
dotnet publish -c Release
sc create "AzureVMAutoscheduler" binPath="path\to\S1_AVM_Task.exe"
sc start "AzureVMAutoscheduler"
```

### As a Docker Container

Create a `Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY bin/Release/net8.0/publish/ .
ENTRYPOINT ["dotnet", "S1_AVM_Task.dll"]
```

### As an Azure Container Instance

Deploy to Azure Container Instances with Managed Identity for seamless authentication.

## Monitoring

Monitor the application through:
- Console output (real-time)
- Log files in `logs/` directory
- CSV file for VM state history
- Azure Monitor (when deployed to Azure)

## Troubleshooting

### Authentication Issues

**Problem**: "DefaultAzureCredential failed to retrieve a token"

**Solution**: 
- Ensure you're logged in via Azure CLI: `az login`
- Check Azure subscription access: `az account show`
- Verify RBAC permissions (need at least "Reader" role)

### No VMs Found

**Problem**: Application reports 0 VMs

**Solution**:
- Verify subscription access
- Check that VMs exist in the subscriptions
- Ensure proper RBAC permissions

### Permission Errors

**Problem**: "Authorization failed" when shutting down VMs

**Solution**: The service principal or user needs "Virtual Machine Contributor" role or higher

## Best Practices Implemented

✅ **Clean Architecture**: Separation of concerns with models, services, and workers  
✅ **SOLID Principles**: Single responsibility, dependency injection, interface segregation  
✅ **Async/Await**: Full asynchronous programming for scalability  
✅ **Structured Logging**: Comprehensive logging with Serilog  
✅ **Configuration Management**: Externalized configuration via appsettings.json  
✅ **Error Handling**: Graceful error handling with detailed logging  
✅ **Performance**: Parallel processing and efficient API usage  
✅ **Security**: Uses DefaultAzureCredential for secure authentication  
✅ **Maintainability**: Clear code structure with XML documentation  
✅ **Testing-Ready**: Interfaces and dependency injection enable easy unit testing  

## License

This project is provided as-is for demonstration purposes.

## Author

Developed as a programming test for Azure VM automation and monitoring.




