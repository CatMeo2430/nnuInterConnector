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
    
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Listen(IPAddress.Any, 8080, listenOptions =>
        {
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1;
        });
        
        options.Listen(IPAddress.Any, 8081, listenOptions =>
        {
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1;
        });
    });
    
    var app = builder.Build();
    
    app.UseCors("AllowAll");
    app.UseRouting();
    
    app.MapControllers();
    app.MapHub<InterconnectionHub>("/interconnectionHub");
    
    var cleanupTimer = new System.Threading.Timer(_ =>
    {
        InterconnectionHub.CleanupInactiveClients(TimeSpan.FromMinutes(2));
    }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    
    Log.Information("Server listening on HTTP: http://120.55.67.157:8080");
    Log.Information("Server listening on WebSocket: ws://120.55.67.157:8081");
    
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