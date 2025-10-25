using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceBusMcp.Services;
using System.Reflection;

Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location!)!);

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});


builder.Services
    .Configure<ServiceBusConfiguration>(builder.Configuration.GetSection("ServiceBus"))
    .AddSingleton<IAzureServiceBusService, AzureServiceBusService>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

var service = app.Services.GetRequiredService<IAzureServiceBusService>();

if (builder.Configuration.GetValue<bool>("TryConnectOnStartup"))
{
    if (!await service.TryConnect())
    {
        Console.WriteLine("Unable to connect to Service Bus namespace.");
        throw new Exception("Unable to connect to Service Bus namespace.");
    }
}

await app.RunAsync();