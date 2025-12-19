using Microsoft.AspNetCore.SignalR;
using Server.Hubs;
using Serilog;
using System.Net;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting NNU InterConnector SignalR Server");
    
    var builder = WebApplication.CreateBuilder(args);
    
    builder.Host.UseSerilog();
    
    builder.Services.AddSignalR();
    builder.Services.AddControllers();
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });
    
    var serverConfig = builder.Configuration.GetSection("ServerConfig");
    var ipAddress = IPAddress.Parse(serverConfig["IpAddress"] ?? "10.20.214.145");
    var httpPort = int.Parse(serverConfig["HttpPort"] ?? "8080");
    var webSocketPort = int.Parse(serverConfig["WebSocketPort"] ?? "8081");
    
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Listen(IPAddress.Any, httpPort, listenOptions =>
        {
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1;
        });
        
        options.Listen(IPAddress.Any, webSocketPort, listenOptions =>
        {
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
        });
    });
    
    var app = builder.Build();
    
    app.UseCors("AllowAll");
    app.UseRouting();
    
    app.MapControllers();
    app.MapHub<InterconnectionHub>("/interconnectionHub");
    
    var cleanupTimer = new System.Threading.Timer(async _ =>
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<InterconnectionHub>>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<InterconnectionHub>>();
            
            var hub = new InterconnectionHub(logger, hubContext);
            await hub.CleanupInactiveClients(TimeSpan.FromMinutes(2));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "清理非活动客户端时发生错误");
        }
    }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    
    Log.Information($"Server listening on HTTP: http://{ipAddress}:{httpPort}");
    Log.Information($"Server listening on WebSocket: ws://{ipAddress}:{webSocketPort}");
    
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}