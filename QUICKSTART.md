# Quick Start Guide

## Prerequisites

1. **.NET 8.0 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
2. **Azure Account** with access to subscriptions
3. **Azure Authentication** - Choose one:
   - Azure CLI: `az login`
   - Visual Studio/VS Code authentication
   - Managed Identity (when running in Azure)

## First Time Setup

### 1. Install Azure CLI (Recommended for Development)

```powershell
# Windows
winget install -e --id Microsoft.AzureCLI

# Or download from: https://aka.ms/installazurecliwindows
```

### 2. Authenticate with Azure

```powershell
# Login to Azure
az login

# (Optional) Set default subscription
az account set --subscription "Your-Subscription-Name"

# Verify your subscriptions
az account list --output table
```

### 3. Verify Permissions

Your account needs the following RBAC roles:
- **Reader** role on subscriptions (to read VM information)
- **Virtual Machine Contributor** role (to shutdown/deallocate VMs)

Check your permissions:
```powershell
az role assignment list --assignee your-email@domain.com --output table
```

## Running the Application

### Option 1: Using dotnet run (Development)

```powershell
cd "S1 AVM Task\S1 AVM Task"
dotnet run
```

### Option 2: Using the compiled executable

```powershell
cd "S1 AVM Task\S1 AVM Task"
dotnet build -c Release
cd bin\Release\net8.0
.\S1_AVM_Task.exe
```

## Testing the Application

### 1. Tag a Test VM

```powershell
# Tag a VM for auto-shutdown
az vm update `
  --resource-group "YourResourceGroup" `
  --name "YourVMName" `
  --set tags.Autoshutdown=1

# Verify the tag
az vm show `
  --resource-group "YourResourceGroup" `
  --name "YourVMName" `
  --query tags
```

### 2. Monitor the Application

The application will:
- Log to console in real-time
- Create `vm_logs.csv` with VM data
- Create `logs/application-YYYYMMDD.log` files

### 3. Check the CSV Output

```powershell
# View the CSV file
cat vm_logs.csv

# Or open in Excel
start vm_logs.csv
```

## Configuration

Edit `appsettings.json` to customize behavior:

```json
{
  "AppSettings": {
    "PollingIntervalMinutes": 5,     // Change polling frequency
    "MaxRunningHours": 8,             // Change max runtime threshold
    "CsvFilePath": "vm_logs.csv",     // Change CSV output path
    "AutoShutdownTagName": "Autoshutdown",
    "AutoShutdownTagValue": "1"
  }
}
```

## Troubleshooting

### Issue: "DefaultAzureCredential failed to retrieve a token"

**Solution:**
```powershell
# Re-login to Azure
az login

# Check which account is active
az account show
```

### Issue: "Authorization failed"

**Solution:** Ensure you have the required permissions:
```powershell
# Add Virtual Machine Contributor role
az role assignment create `
  --assignee your-email@domain.com `
  --role "Virtual Machine Contributor" `
  --scope /subscriptions/YOUR-SUBSCRIPTION-ID
```

### Issue: No VMs found

**Checklist:**
- [ ] Are you authenticated to Azure? (`az account show`)
- [ ] Do you have Reader access to subscriptions?
- [ ] Do VMs exist in your subscriptions?
- [ ] Check application logs for detailed errors

### Issue: Application crashes

**Solution:**
1. Check `logs/application-YYYYMMDD.log` for detailed error messages
2. Verify all NuGet packages are restored: `dotnet restore`
3. Rebuild the application: `dotnet build`

## Stopping the Application

Press `Ctrl+C` to gracefully stop the application. It will:
- Complete the current polling cycle
- Flush all logs
- Close file handles properly

## Running as a Service

### Windows Service

```powershell
# Publish self-contained
dotnet publish -c Release -r win-x64 --self-contained

# Create Windows Service (run as Administrator)
sc create "AzureVMAutoscheduler" binPath="C:\path\to\S1_AVM_Task.exe"

# Start the service
sc start "AzureVMAutoscheduler"

# Check status
sc query "AzureVMAutoscheduler"

# Stop the service
sc stop "AzureVMAutoscheduler"

# Delete the service
sc delete "AzureVMAutoscheduler"
```

### Task Scheduler (Alternative)

1. Open Task Scheduler
2. Create Basic Task
3. Set trigger: "At startup" or "Daily"
4. Set action: Start a program
5. Program: `C:\path\to\S1_AVM_Task.exe`
6. Finish and test

## Monitoring in Production

### Log Files

- **Location**: `logs/application-YYYYMMDD.log`
- **Retention**: 7 days
- **Format**: Structured logging with timestamps

### CSV Data

- **Location**: `vm_logs.csv`
- **Format**: Append-only
- **Columns**: Timestamp, SubscriptionId, ResourceGroup, ComputerName, PowerState

### Best Practices

1. **Monitor log files** for errors
2. **Review CSV data** periodically for VM state history
3. **Set up alerts** on log file errors
4. **Rotate log files** if disk space is limited
5. **Backup CSV data** regularly

## Example: Full Workflow

```powershell
# 1. Login to Azure
az login

# 2. Navigate to project
cd "C:\Users\...\s1\S1 AVM Task\S1 AVM Task"

# 3. Restore and build
dotnet restore
dotnet build

# 4. Tag a test VM
az vm update --resource-group "MyRG" --name "TestVM" --set tags.Autoshutdown=1

# 5. Run the application
dotnet run

# 6. In another terminal, monitor the CSV
Get-Content vm_logs.csv -Wait

# 7. Check logs
Get-Content logs\application-20251106.log -Wait

# 8. Stop with Ctrl+C when done
```

## Next Steps

1. **Test with one VM** first
2. **Monitor for a few cycles** (30-60 minutes)
3. **Verify CSV data** is being written correctly
4. **Check that shutdown/deallocate** works as expected
5. **Scale to more VMs** by adding tags

## Support

For issues or questions:
1. Check the main README.md
2. Review application logs
3. Verify Azure authentication and permissions
4. Check Azure VM tags are correct

## Security Notes

- Never commit `appsettings.json` with sensitive data
- Use Managed Identity in production
- Rotate credentials regularly
- Monitor application logs for unauthorized access attempts
- Use Azure Key Vault for production secrets




