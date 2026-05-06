using Microsoft.AspNetCore.Server.Kestrel.Core;
using SkiJobControl.Console.Hubs;
using SkiJobControl.Console.Services;
using SkiJobControl.Core.Repositories;

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
string jobFilePath = builder.Configuration["JobFilePath"]
    ?? Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "shared", "jobs_mock.json"));
builder.Services.AddSingleton<IJobRepository>(new FileJobRepository(jobFilePath));

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
// Do not force HTTPS redirection here.
// This app intentionally serves HTTP/1 (Web UI) and h2c HTTP/2 (gRPC) on fixed local ports.

// Serve default files (e.g., index.html) before static files so the default-file middleware
// can rewrite requests to the appropriate static file.
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthorization();

app.MapControllers();
app.MapGrpcService<JobControlServiceImplementation>();
app.MapHub<NodeHub>("/hubs/node");

app.Run();
