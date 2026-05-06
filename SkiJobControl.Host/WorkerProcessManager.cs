using System.Diagnostics;

namespace SkiJobControl.Host;

public class WorkerProcessManager
{
    private readonly string _workerPath;
    private readonly int _minPoolSize;
    private readonly string _jobFilePath;
    private readonly List<WorkerProcessInfo> _pool = new();
    private readonly object _lock = new();

    public event Action<int, string>? OnWorkerMessage;
    public event Action? OnPoolChanged;

    public WorkerProcessManager(string workerPath, int minPoolSize, string jobFilePath)
    {
        _workerPath = workerPath;
        _minPoolSize = minPoolSize;
        _jobFilePath = jobFilePath;
    }

    public void Initialize()
    {
        ReplenishPool();
    }

    private void ReplenishPool()
    {
        lock (_lock)
        {
            while (_pool.Count < _minPoolSize)
            {
                StartNewWorker();
            }
        }
        OnPoolChanged?.Invoke();
    }

    private void StartNewWorker()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _workerPath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.EnvironmentVariables["SKIJOBCONTROL_JOB_FILE_PATH"] = _jobFilePath;

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        
        if (process.Start())
        {
            var info = new WorkerProcessInfo(process);
            process.Exited += (s, e) => HandleWorkerExit(info);
            _pool.Add(info);
            Task.Run(() => ReadWorkerOutput(info));
        }
    }

    private void HandleWorkerExit(WorkerProcessInfo info)
    {
        lock (_lock)
        {
            _pool.Remove(info);
        }
        OnWorkerMessage?.Invoke(info.Process.Id, "EXITED");
        ReplenishPool();
    }

    private async Task ReadWorkerOutput(WorkerProcessInfo info)
    {
        try
        {
            while (!info.Process.HasExited)
            {
                var line = await info.Process.StandardOutput.ReadLineAsync();
                if (line != null)
                {
                    ParseWorkerMessage(info, line);
                    OnWorkerMessage?.Invoke(info.Process.Id, line);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading output from worker {info.Process.Id}: {ex.Message}");
        }
    }

    private void ParseWorkerMessage(WorkerProcessInfo info, string message)
    {
        if (message.StartsWith("STATUS:"))
        {
            info.Status = message.Substring(7);
        }
        else if (message.StartsWith("JOB_STARTED:"))
        {
            info.CurrentJobId = message.Substring(12);
            info.Status = "Running";
        }
        else if (message.StartsWith("JOB_FINISHED:") || message.StartsWith("JOB_FAILED:"))
        {
            info.CurrentJobId = null;
            info.Status = "Idle";
        }
    }

    public void SendCommand(int pid, string command)
    {
        lock (_lock)
        {
            var info = _pool.FirstOrDefault(w => w.Process.Id == pid);
            if (info != null)
            {
                info.Process.StandardInput.WriteLine(command);
            }
        }
    }

    public void KillWorker(int pid)
    {
        lock (_lock)
        {
            var info = _pool.FirstOrDefault(w => w.Process.Id == pid);
            if (info != null)
            {
                try
                {
                    info.Process.Kill(true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to kill process {pid}: {ex.Message}");
                }
            }
        }
    }

    public List<WorkerProcessInfo> GetPoolSnapshot()
    {
        lock (_lock)
        {
            return _pool.Select(w => new WorkerProcessInfo(w.Process) 
            { 
                CurrentJobId = w.CurrentJobId, 
                Status = w.Status 
            }).ToList();
        }
    }
}

public class WorkerProcessInfo
{
    public Process Process { get; }
    public string? CurrentJobId { get; set; }
    public string Status { get; set; } = "Idle";

    public WorkerProcessInfo(Process process)
    {
        Process = process;
    }
}
