using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceBusMcp.Services;
using ServiceBusMcp.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

builder.Logging.AddConsole();

builder.Services
    .Configure<ServiceBusConfiguration>(builder.Configuration.GetSection("ServiceBus"))
    .AddSingleton<IAzureServiceBusService, AzureServiceBusService>();

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly(typeof(ServiceBusTools).Assembly);

var app = builder.Build();
app.MapMcp("/mcp");

if (builder.Configuration.GetValue<bool>("TryConnectOnStartup"))
{
    var service = app.Services.GetRequiredService<IAzureServiceBusService>();

    if (!await service.TryConnect())
    {
        Console.WriteLine("Unable to connect to Service Bus namespace.");
        throw new Exception("Unable to connect to Service Bus namespace.");
    }
}

await app.RunAsync();
