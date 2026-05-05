using Grpc.Core;
using SkiJobControl.Protos;
using SkiJobControl.Core.Repositories;

namespace SkiJobControl.Host;

public class HostService : BackgroundService
{
    private readonly ILogger<HostService> _logger;
    private readonly WorkerProcessManager _processManager;
    private readonly IJobRepository _jobRepository;
    private readonly JobControlService.JobControlServiceClient _client;
    private readonly string _nodeId;
    private readonly object _healthStateLock = new();
    private bool _isSessionAlive;
    private DateTimeOffset? _sessionOpenedAt;
    private DateTimeOffset? _lastStatusSentAt;
    private DateTimeOffset? _lastCommandReceivedAt;
    private DateTimeOffset? _lastGrpcErrorAt;

    public HostService(ILogger<HostService> logger, WorkerProcessManager processManager, IJobRepository jobRepository, JobControlService.JobControlServiceClient client)
    {
        _logger = logger;
        _processManager = processManager;
        _jobRepository = jobRepository;
        _client = client;
        _nodeId = Environment.MachineName + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processManager.Initialize();

        var healthTask = RunHealthCheckLoopAsync(stoppingToken);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Connecting to Console...");
                    using var call = _client.OpenSession(cancellationToken: stoppingToken);
                    MarkSessionOpened();

                    // Task to send status updates
                    var sendTask = Task.Run(async () =>
                    {
                        try
                        {
                            while (!stoppingToken.IsCancellationRequested)
                            {
                                var pool = _processManager.GetPoolSnapshot();
                                var status = new NodeStatus
                                {
                                    NodeId = _nodeId,
                                    CpuUsage = GetCpuUsage(),
                                    RamUsageMb = GetRamUsage(),
                                    ActiveWorkers = pool.Count(w => w.Status != "Idle" && w.Status != "Exiting")
                                };

                                foreach (var w in pool)
                                {
                                    status.Workers.Add(new WorkerInfo
                                    {
                                        ProcessId = w.Process.Id,
                                        JobId = w.CurrentJobId ?? "",
                                        Status = w.Status
                                    });
                                }

                                await call.RequestStream.WriteAsync(status);
                                MarkStatusSent();
                                await Task.Delay(2000, stoppingToken);
                            }
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                        {
                            // Ignore cancellation during shutdown.
                        }
                        catch (Exception ex)
                        {
                            MarkGrpcError();
                            _logger.LogWarning("Send status error: {Message}", ex.Message);
                        }
                    }, stoppingToken);

                    // Task to receive commands
                    var receiveTask = Task.Run(async () =>
                    {
                        try
                        {
                            await foreach (var command in call.ResponseStream.ReadAllAsync(stoppingToken))
                            {
                                MarkCommandReceived();
                                HandleCommand(command);
                            }
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                        {
                            // Ignore cancellation during shutdown.
                        }
                        catch (Exception ex)
                        {
                            MarkGrpcError();
                            _logger.LogWarning("Receive command error: {Message}", ex.Message);
                        }
                    }, stoppingToken);

                    // Task to poll for jobs
                    var pollTask = Task.Run(async () =>
                    {
                        try
                        {
                            while (!stoppingToken.IsCancellationRequested)
                            {
                                var idleWorker = _processManager.GetPoolSnapshot().FirstOrDefault(w => w.Status == "Idle");
                                if (idleWorker != null)
                                {
                                    var job = await _jobRepository.DequeueJobAsync(_nodeId);
                                    if (job != null)
                                    {
                                        _logger.LogInformation("Assigned Job {JobId} to Worker {Pid}", job.JobId, idleWorker.Process.Id);
                                        _processManager.SendCommand(idleWorker.Process.Id, $"START_JOB:{job.JobId}");
                                    }
                                }

                                await Task.Delay(3000, stoppingToken);
                            }
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                        {
                            // Ignore cancellation during shutdown.
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Job polling error: {Message}", ex.Message);
                        }
                    }, stoppingToken);

                    await Task.WhenAny(sendTask, receiveTask, pollTask);
                    MarkSessionClosed();
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    MarkGrpcError();
                    MarkSessionClosed();
                    _logger.LogError(ex, "gRPC connection error. Retrying in 5s...");
                    if (!stoppingToken.IsCancellationRequested)
                    {
                        await Task.Delay(5000, stoppingToken);
                    }
                }
            }
        }
        finally
        {
            await healthTask;
        }
    }

    private async Task RunHealthCheckLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                LogHealthSnapshot();
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private void LogHealthSnapshot()
    {
        bool isSessionAlive;
        DateTimeOffset? sessionOpenedAt;
        DateTimeOffset? lastStatusSentAt;
        DateTimeOffset? lastCommandReceivedAt;
        DateTimeOffset? lastGrpcErrorAt;

        lock (_healthStateLock)
        {
            isSessionAlive = _isSessionAlive;
            sessionOpenedAt = _sessionOpenedAt;
            lastStatusSentAt = _lastStatusSentAt;
            lastCommandReceivedAt = _lastCommandReceivedAt;
            lastGrpcErrorAt = _lastGrpcErrorAt;
        }

        var now = DateTimeOffset.UtcNow;
        var sessionUptimeSec = sessionOpenedAt.HasValue ? Math.Round((now - sessionOpenedAt.Value).TotalSeconds, 1) : -1;
        var lastStatusSentSecAgo = lastStatusSentAt.HasValue ? Math.Round((now - lastStatusSentAt.Value).TotalSeconds, 1) : -1;
        var lastCommandReceivedSecAgo = lastCommandReceivedAt.HasValue ? Math.Round((now - lastCommandReceivedAt.Value).TotalSeconds, 1) : -1;
        var lastGrpcErrorSecAgo = lastGrpcErrorAt.HasValue ? Math.Round((now - lastGrpcErrorAt.Value).TotalSeconds, 1) : -1;

        var pool = _processManager.GetPoolSnapshot();
        var activeWorkers = pool.Count(w => w.Status != "Idle" && w.Status != "Exiting");

        _logger.LogInformation(
            "HealthCheck | SessionAlive={SessionAlive} | SessionUptimeSec={SessionUptimeSec} | LastStatusSentSecAgo={LastStatusSentSecAgo} | LastCommandSecAgo={LastCommandSecAgo} | LastGrpcErrorSecAgo={LastGrpcErrorSecAgo} | WorkerPool={WorkerPool} | ActiveWorkers={ActiveWorkers}",
            isSessionAlive,
            sessionUptimeSec,
            lastStatusSentSecAgo,
            lastCommandReceivedSecAgo,
            lastGrpcErrorSecAgo,
            pool.Count,
            activeWorkers);
    }

    private void MarkSessionOpened()
    {
        lock (_healthStateLock)
        {
            _isSessionAlive = true;
            _sessionOpenedAt = DateTimeOffset.UtcNow;
            _lastStatusSentAt = null;
        }
    }

    private void MarkSessionClosed()
    {
        lock (_healthStateLock)
        {
            _isSessionAlive = false;
            _sessionOpenedAt = null;
        }
    }

    private void MarkStatusSent()
    {
        lock (_healthStateLock)
        {
            _lastStatusSentAt = DateTimeOffset.UtcNow;
        }
    }

    private void MarkCommandReceived()
    {
        lock (_healthStateLock)
        {
            _lastCommandReceivedAt = DateTimeOffset.UtcNow;
        }
    }

    private void MarkGrpcError()
    {
        lock (_healthStateLock)
        {
            _lastGrpcErrorAt = DateTimeOffset.UtcNow;
        }
    }

    private void HandleCommand(ControlCommand command)
    {
        _logger.LogInformation("Received command: {CommandId} ({Type})", command.CommandId, command.PayloadCase);
        
        switch (command.PayloadCase)
        {
            case ControlCommand.PayloadOneofCase.TerminateWorker:
                _processManager.KillWorker(command.TerminateWorker.ProcessId);
                break;
        }
    }

    private double GetCpuUsage()
    {
        // Placeholder for real CPU metrics
        return Random.Shared.NextDouble() * 100;
    }

    private double GetRamUsage()
    {
        return GC.GetGCMemoryInfo().HeapSizeBytes / 1024.0 / 1024.0;
    }
}
