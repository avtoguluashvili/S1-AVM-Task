# Azure VM Autoscheduler - Setup Guide

Hi! Thanks for choosing this solution. I've put together this guide to help you get up and running quickly.

## What This Does

This application monitors your Azure VMs and helps control costs by:
- Checking all your VMs every 5 minutes
- Shutting down VMs that run too long
- Deallocating VMs that are stopped (to save money)
- Keeping a log of everything

The cool part? It only touches VMs you explicitly tag, so your production stuff stays safe.

## Before You Start

You'll need:
- .NET 8 installed on your machine (download from microsoft.com/net)
- An Azure account with some VMs
- Permission to view and manage those VMs

## First Time Setup

### Step 1: Get Your Azure Credentials Working

The easiest way is using Azure CLI:

1. Open PowerShell or Terminal
2. Type: `az login`
3. Sign in when your browser opens
4. Done!

If you prefer Visual Studio, just sign in there (File → Account Settings) and you're good.

### Step 2: Extract and Open the Project

1. Unzip the files somewhere convenient
2. Open PowerShell/Terminal in that folder
3. Type: `dotnet restore`

This downloads all the dependencies. Takes about 30 seconds.

### Step 3: Configure (Optional)

Open `appsettings.json` in any text editor. You'll see:

```json
{
  "AppSettings": {
    "PollingIntervalMinutes": 5,
    "MaxRunningHours": 8,
    "CsvFilePath": "vm_logs.csv",
    "AutoShutdownTagName": "Autoshutdown",
    "AutoShutdownTagValue": "1",
    "SubscriptionId": null,
    "TenantId": null
  }
}
```

Most of the time, the defaults work fine. But here's what each setting does:

**PollingIntervalMinutes**: How often to check (in minutes). Default is 5.

**MaxRunningHours**: Shut down VMs after this many hours. Default is 8.

**CsvFilePath**: Where to save the log file. Default is right next to the app.

**SubscriptionId**: Leave as `null` to monitor all your subscriptions, or put a specific subscription ID here to target just one.

**TenantId**: Leave as `null` unless you're in a multi-tenant setup. Your IT folks will know if you need this.

The tag stuff (AutoShutdownTagName and AutoShutdownTagValue) - that's how you tell the app which VMs to manage. More on that below.

### Step 4: Run It

Still in that PowerShell/Terminal window, just type:

```
dotnet run
```

You should see logs start appearing. If it says "Found X subscriptions" and "Collected information for Y VMs", you're in business!

Press Ctrl+C when you want to stop it.

## Tagging Your VMs

The app only touches VMs you tag. This is intentional - we don't want it messing with production by accident.

To enable auto-shutdown on a VM, add this tag:
- Name: `Autoshutdown`
- Value: `1`

You can do this in the Azure Portal:
1. Go to your VM
2. Click "Tags" in the left menu
3. Add the tag
4. Save

Or use Azure CLI:
```bash
az vm update --resource-group YourResourceGroup --name YourVMName --set tags.Autoshutdown=1
```

**Important**: VMs without this tag are completely ignored. They won't be shut down, won't be touched. The app just logs their status.

## What Happens When It Runs

Every 5 minutes (or whatever you configured), the app:

1. Checks all your VMs
2. Writes their status to a CSV file (great for tracking costs)
3. For VMs with the Autoshutdown tag:
   - If running more than 8 hours → shuts it down
   - If stopped but still allocated → deallocates it (saves more money)

The CSV file grows over time - each check adds new rows. You can open it in Excel to see what's been happening.

## Understanding the Logs

You'll see two types of logs:

**Console logs**: Real-time info about what's happening right now

**File logs**: Saved in the `logs` folder, kept for 7 days

The logs tell you everything - which VMs were found, what actions were taken, any errors, etc.

## Common Scenarios

### Testing First?

Good idea! Here's how:
1. Create a cheap test VM (like Standard_B1s)
2. Tag it with `Autoshutdown=1`
3. Run the app
4. Check the logs and CSV file

You should see the VM appear in the logs. If you let it run for 8 hours, it'll shut down automatically.

### Just Want to Monitor (No Auto-Shutdown)?

Set MaxRunningHours to something huge:
```json
"MaxRunningHours": 9999
```

Now it just logs everything but never takes action.

### Monitoring a Specific Subscription?

Put your subscription ID in the config:
```json
"SubscriptionId": "12345678-1234-1234-1234-123456789abc"
```

This is faster if you have lots of subscriptions but only care about one.

### Different Hours for Different VMs?

Right now, all tagged VMs use the same MaxRunningHours setting. If you need different thresholds, you'll need to run multiple copies of the app with different configs. Each copy can target a specific subscription.

## Troubleshooting

### "No subscriptions found"

Make sure you're logged into Azure:
- Try: `az account show`
- If that fails, run: `az login` again

### "Failed to authenticate"

Your Azure login might have expired. Run `az login` again.

### "No VMs found"

Either:
- You really don't have VMs in those subscriptions, or
- You need to set SubscriptionId in the config to target the right subscription

### "Failed to shutdown VM"

Check your permissions. You need "Virtual Machine Contributor" role (or higher) to shut down VMs.

## Running It 24/7

For production use, you probably want this running all the time. Here are your options:

### Option 1: Windows Service

```powershell
# Build for release
dotnet publish -c Release

# Create service (run PowerShell as Admin)
sc create "AzureVMAutoscheduler" binPath="C:\path\to\S1 AVM Task.exe"
sc start "AzureVMAutoscheduler"
```

Note: Services need special authentication setup (Managed Identity or service principal).

### Option 2: Docker Container

If you're into containers:

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY bin/Release/net8.0/publish/ .
ENTRYPOINT ["dotnet", "S1 AVM Task.dll"]
```

### Option 3: Azure Container Instances

Run it in Azure itself - it'll use Managed Identity for auth (no login needed).

### Option 4: Just Leave It Running

Honestly, for smaller setups, just running it in a PowerShell window works fine. Make sure your machine doesn't go to sleep though.

## Security Notes

A few things to know:

**No passwords stored**: The app uses Azure's built-in authentication. No credentials in config files.

**Read-only by default**: Without the Contributor role, it can only monitor. It can't actually shut things down. This is a good way to test.

**CSV files**: They contain VM names and states, but no sensitive data. Still, don't put them on public shares.

**Logs**: Same as CSV - operational data only. But they might show subscription IDs, so treat them as internal.

## Getting Help

If you run into issues:

1. Check the logs (both console and the files in the `logs` folder)
2. Make sure authentication is working: `az account show`
3. Verify VM permissions in Azure Portal
4. Check that VMs are properly tagged

The error messages are usually pretty descriptive about what went wrong.

## Questions?

Feel free to reach out if you need help getting this set up or have questions about how it works.

A few things I get asked a lot:

**Q: Will this shut down production VMs?**  
A: Only if you tag them with Autoshutdown=1. Without the tag, they're ignored.

**Q: Can I customize the tag name?**  
A: Yep! Change AutoShutdownTagName in the config.

**Q: What if a VM needs to run longer than 8 hours?**  
A: Either increase MaxRunningHours or don't tag that VM.

**Q: Can I see what happened while I was away?**  
A: Yes, check the CSV file and log files. They show everything.

**Q: Does this cost money to run?**  
A: The app itself is free. You just need a machine to run it on. If you run it in Azure, there's a small compute cost. But the money you save on VM costs usually covers that many times over.

---

That's pretty much it! The app is straightforward - authenticate, tag your VMs, run it, and it handles the rest.

Good luck, and happy VM managing!

