using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.AspNetCore.Authentication;
using ServiceBusMcp.Services;
using ServiceBusMcp.Tools;

var builder = WebApplication.CreateBuilder(args);

var serverUrl = "http://localhost:5000";
var expectedAudience = "http://localhost:5000/mcp";
var oauthServerUrl = "https://localhost:7069/";

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

var mcpToolsPolicyName = "McpToolsAccess";
var requiredGroupId = builder.Configuration.GetValue<string>("Authorization:RequiredGroupId") ?? "00000000-0000-0000-0000-000000000000";

builder.Logging.AddConsole();

builder.Services
    .Configure<ServiceBusConfiguration>(builder.Configuration.GetSection("ServiceBus"))
    .AddSingleton<IAzureServiceBusService, AzureServiceBusService>();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.Authority = oauthServerUrl;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidAudience = expectedAudience,
            ValidIssuer = oauthServerUrl,
            NameClaimType = "name",
            RoleClaimType = "roles"
        };
    })
    .AddMcp(options =>
    {
        options.ResourceMetadata = new()
        {
            AuthorizationServers = { oauthServerUrl },
            ScopesSupported = [ "mcp:tools" ],
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(mcpToolsPolicyName, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("groups", requiredGroupId);
    });
});

builder.Services.AddHttpContextAccessor();
builder.Services
    .AddMcpServer()
    .WithToolsFromAssembly(typeof(ServiceBusTools).Assembly)
    .WithHttpTransport(options => {
        options.Stateless = true;
    });

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapMcp("/mcp")
    .RequireAuthorization(mcpToolsPolicyName);

if (builder.Configuration.GetValue<bool>("TryConnectOnStartup"))
{
    var service = app.Services.GetRequiredService<IAzureServiceBusService>();

    if (!await service.TryConnect())
    {
        Console.WriteLine("Unable to connect to Service Bus namespace.");
        throw new Exception("Unable to connect to Service Bus namespace.");
    }
}

await app.RunAsync(serverUrl);