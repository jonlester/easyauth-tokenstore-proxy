using EasyAuthTokenstoreProxy.Configuration;
using EasyAuthTokenstoreProxy.Extensions;

const string listenUrl = "https://localhost:8081";

// Load .env files in development
if (args.Contains("--environment=Development") || Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
{
    // Load .env first (if exists)
    if (File.Exists(".env"))
        DotNetEnv.Env.Load(".env");
    
    // Load .env.local second (overrides .env values if exists)
    if (File.Exists(".env.local"))
        DotNetEnv.Env.Load(".env.local");
}

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel for HTTP (sidecar internal communication)
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    // HTTP port for sidecar communication
    options.ListenAnyIP(8081);
});

builder.WebHost.UseUrls(listenUrl);



// Add services to the container.
builder.Services.AddHttpClient("BlobProxy", client =>
{
    // Configure for optimal connection pooling and performance
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    // Optimize for connection pooling - reuse connections aggressively
    PooledConnectionLifetime = TimeSpan.FromMinutes(10),
    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
    MaxConnectionsPerServer = 20,
    
    // Enable HTTP/2 for better multiplexing
    EnableMultipleHttp2Connections = true,
    
    // Optimize for blob storage workloads
    UseProxy = false,
    UseCookies = false
});

// Configure blob storage from environment variables
var blobConfig = BlobStorageConfiguration.FromEnvironment();
builder.Services.AddSingleton(blobConfig);

var app = builder.Build();

app.MapBlobProxyEndpoints();

app.Run();


