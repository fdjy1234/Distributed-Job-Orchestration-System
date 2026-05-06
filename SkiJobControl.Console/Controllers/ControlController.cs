using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SkiJobControl.Console.Hubs;
using SkiJobControl.Console.Services;
using SkiJobControl.Core.Models;
using SkiJobControl.Core.Repositories;
using SkiJobControl.Protos;

namespace SkiJobControl.Console.Controllers;

[ApiController]
[Route("api/control")]
public class ControlController : ControllerBase
{
    private readonly NodeSessionManager _sessionManager;
    private readonly IJobRepository _jobRepository;
    private readonly IHubContext<NodeHub> _hubContext;
    private readonly ILogger<ControlController> _logger;

    public ControlController(
        NodeSessionManager sessionManager,
        IJobRepository jobRepository,
        IHubContext<NodeHub> hubContext,
        ILogger<ControlController> logger)
    {
        _sessionManager = sessionManager;
        _jobRepository = jobRepository;
        _hubContext = hubContext;
        _logger = logger;
    }

    [HttpPost("terminate")]
    public async Task<IActionResult> TerminateWorker([FromBody] TerminateRequest request)
    {
        var command = new ControlCommand
        {
            CommandId = Guid.NewGuid().ToString(),
            TerminateWorker = new TerminateWorkerRequest
            {
                ProcessId = request.ProcessId,
                JobId = request.JobId ?? ""
            }
        };

        bool success = await _sessionManager.SendCommandAsync(request.NodeId, command);

        var message = success
            ? $"Terminate command sent: Node={request.NodeId}, PID={request.ProcessId}, Job={request.JobId ?? "N/A"}"
            : $"Terminate command failed: Node={request.NodeId}, PID={request.ProcessId}";

        _logger.LogInformation("{Message}", message);
        await _hubContext.Clients.All.SendAsync("AppendSystemLog", new ConsoleLogEntry
        {
            Level = success ? "info" : "warn",
            Message = message
        });

        return success ? Ok() : NotFound("Node not found or session closed.");
    }

    [HttpPost("mock-jobs")]
    public async Task<IActionResult> CreateMockJobs([FromBody] CreateMockJobsRequest request)
    {
        int count = Math.Clamp(request.Count, 1, 200);
        string prefix = string.IsNullOrWhiteSpace(request.Prefix) ? "SIM" : request.Prefix.Trim().ToUpperInvariant();
        string jobType = string.IsNullOrWhiteSpace(request.JobType) ? "simulation" : request.JobType.Trim();

        var now = DateTimeOffset.UtcNow;
        var jobs = Enumerable.Range(1, count)
            .Select(i => new Job
            {
                JobId = $"{prefix}_{now:yyyyMMddHHmmss}_{i:D3}",
                Payload = $"{{\"type\":\"{jobType}\",\"batch\":\"{prefix}\",\"seq\":{i}}}",
                Status = 0,
                UpdateTime = DateTime.UtcNow
            })
            .ToList();

        int queued = await _jobRepository.EnqueueJobsAsync(jobs);
        var message = $"Mock jobs queued: {queued} (prefix={prefix}, type={jobType})";
        _logger.LogInformation("{Message}", message);

        await _hubContext.Clients.All.SendAsync("AppendSystemLog", new ConsoleLogEntry
        {
            Level = "info",
            Message = message
        });

        return Ok(new
        {
            queued,
            prefix,
            jobType,
            jobIds = jobs.Select(j => j.JobId).ToArray()
        });
    }
}

public class TerminateRequest
{
    public string NodeId { get; set; } = "";
    public int ProcessId { get; set; }
    public string? JobId { get; set; }
}

public class CreateMockJobsRequest
{
    public int Count { get; set; } = 10;
    public string? Prefix { get; set; } = "SIM";
    public string? JobType { get; set; } = "simulation";
}

public class ConsoleLogEntry
{
    public string Level { get; set; } = "info";
    public string Message { get; set; } = "";
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
}
