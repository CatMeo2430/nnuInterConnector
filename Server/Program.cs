using Microsoft.AspNetCore.SignalR;
using Server.Hubs;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Level:u3}][{Timestamp:yy-MM-dd HH:mm:ss}] {Message:lj}{NewLine}{Exception}")
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
    var ipAddress = System.Net.IPAddress.Parse(serverConfig["IpAddress"] ?? throw new InvalidOperationException("ServerConfig:IpAddress not configured"));
    var httpPort = int.Parse(serverConfig["HttpPort"] ?? "8080");
    var webSocketPort = int.Parse(serverConfig["WebSocketPort"] ?? "8081");
    
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Listen(System.Net.IPAddress.Any, httpPort, listenOptions =>
        {
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1;
        });
        
        options.Listen(System.Net.IPAddress.Any, webSocketPort, listenOptions =>
        {
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
        });
    });
    
    var app = builder.Build();
    
    app.UseCors("AllowAll");
    app.UseRouting();
    
    app.MapControllers();
    app.MapHub<InterconnectionHub>("/interconnectionHub");
    
    const int CleanupIntervalMinutes = 1;
    const int InitialDelayMinutes = 1;
    
    using var cleanupTimer = new System.Threading.Timer(_ =>
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<InterconnectionHub>>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<InterconnectionHub>>();
            
            var hub = new InterconnectionHub(logger, hubContext);
            hub.CleanupInactiveClients(TimeSpan.FromMinutes(InterconnectionHub.HEARTBEAT_TIMEOUT_MINUTES)).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "清理非活动客户端时发生错误");
        }
    }, null, TimeSpan.FromMinutes(InitialDelayMinutes), TimeSpan.FromMinutes(CleanupIntervalMinutes));
    
    Log.Information("Server listening on HTTP: http://{IpAddress}:{HttpPort}", ipAddress, httpPort);
    Log.Information("Server listening on WebSocket: ws://{IpAddress}:{WebSocketPort}", ipAddress, webSocketPort);
    
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