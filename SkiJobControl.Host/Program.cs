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
string consoleUrl = builder.Configuration["ConsoleUrl"] ?? "http://localhost:5249";

builder.Services.AddSingleton<IJobRepository>(new FileJobRepository());
builder.Services.AddSingleton(new WorkerProcessManager(workerPath, minPoolSize));

builder.Services.AddGrpcClient<JobControlService.JobControlServiceClient>(o =>
{
    o.Address = new Uri(consoleUrl);
});

builder.Services.AddHostedService<HostService>();

var host = builder.Build();
host.Run();
