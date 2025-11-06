using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using S1_AVM_Task.Models;
using S1_AVM_Task.Services;
using S1_AVM_Task.Workers;

namespace S1_AVM_Task;

/// <summary>
/// Azure VM Autoscheduler Application
/// Monitors Azure VMs across all subscriptions and manages power state according to business rules
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        // Configure Serilog early so we can log during startup
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        Log.Information("Starting Azure VM Autoscheduler...");

        try
        {
            var host = CreateHostBuilder(args).Build();
            
            Log.Information("Application configured successfully. Starting host...");
            
            await host.RunAsync();
            
            Log.Information("Application stopped gracefully");
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog((context, services, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext())
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory());
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddEnvironmentVariables();
                config.AddCommandLine(args);
            })
            .ConfigureServices((hostContext, services) =>
            {
                // Bind configuration
                services.Configure<AppSettings>(hostContext.Configuration.GetSection("AppSettings"));

                // Register services
                services.AddSingleton<IAzureVmService, AzureVmService>();
                services.AddSingleton<ICsvWriterService, CsvWriterService>();
                services.AddSingleton<IVmPowerManagementService, VmPowerManagementService>();

                // Register background worker
                services.AddHostedService<VmMonitoringWorker>();

                Log.Information("Services registered successfully");
            });
}
