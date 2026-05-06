using SkiJobControl.Host;
using SkiJobControl.Protos;
using SkiJobControl.Core.Repositories;

// This switch is required for gRPC over insecure HTTP in some .NET versions, 
// though often not needed in .NET 6+ if configured correctly. 
// Adding it for maximum compatibility in this environment.
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = Host.CreateApplicationBuilder(args);

// Configuration
string workerPath = builder.Configuration["WorkerPath"] ?? "SkiJobControl.Worker.exe";
int minPoolSize = builder.Configuration.GetValue<int>("MinPoolSize", 2);
string jobFilePath = builder.Configuration["JobFilePath"]
    ?? Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "shared", "jobs_mock.json"));
string consoleUrl = builder.Configuration["ConsoleUrl"] ?? "http://localhost:5250";

// Console gRPC endpoint must use the HTTP/2 port (default: 5250).
if (Uri.TryCreate(consoleUrl, UriKind.Absolute, out var parsedConsoleUri) && parsedConsoleUri.Port == 5249)
{
    consoleUrl = $"{parsedConsoleUri.Scheme}://{parsedConsoleUri.Host}:5250";
    Console.WriteLine("[Host] ConsoleUrl pointed to HTTP/1 port 5249. Auto-corrected to gRPC port 5250.");
}

Console.WriteLine($"[Host] Using Console gRPC endpoint: {consoleUrl}");
Console.WriteLine($"[Host] Using shared job file: {jobFilePath}");

builder.Services.AddSingleton<IJobRepository>(new FileJobRepository(jobFilePath));
builder.Services.AddSingleton(new WorkerProcessManager(workerPath, minPoolSize, jobFilePath));

builder.Services.AddGrpcClient<JobControlService.JobControlServiceClient>(o =>
{
    o.Address = new Uri(consoleUrl);
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    EnableMultipleHttp2Connections = true,
    UseProxy = false
});

builder.Services.AddHostedService<HostService>();

var host = builder.Build();
host.Run();
