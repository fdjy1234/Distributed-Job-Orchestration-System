using Microsoft.AspNetCore.Mvc;
using SkiJobControl.Console.Services;
using SkiJobControl.Protos;

namespace SkiJobControl.Console.Controllers;

[ApiController]
[Route("api/control")]
public class ControlController : ControllerBase
{
    private readonly NodeSessionManager _sessionManager;

    public ControlController(NodeSessionManager sessionManager)
    {
        _sessionManager = sessionManager;
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
        return success ? Ok() : NotFound("Node not found or session closed.");
    }
}

public class TerminateRequest
{
    public string NodeId { get; set; } = "";
    public int ProcessId { get; set; }
    public string? JobId { get; set; }
}
