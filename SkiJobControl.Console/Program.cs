using Microsoft.AspNetCore.Server.Kestrel.Core;
using SkiJobControl.Console.Hubs;
using SkiJobControl.Console.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.WebHost.ConfigureKestrel(options =>
{
    // HTTP/1 endpoint for Web UI and SignalR
    options.ListenAnyIP(5249, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1;
    });

    // HTTP/2 endpoint for gRPC (No TLS)
    options.ListenAnyIP(5250, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});

builder.Services.AddGrpc();
builder.Services.AddSignalR();
builder.Services.AddSingleton<NodeSessionManager>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseDefaultFiles();
app.UseAuthorization();

app.MapControllers();
app.MapGrpcService<JobControlServiceImplementation>();
app.MapHub<NodeHub>("/hubs/node");

app.Run();
