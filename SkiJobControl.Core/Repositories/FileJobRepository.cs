using System.Text.Json;
using SkiJobControl.Core.Models;

namespace SkiJobControl.Core.Repositories;

public class FileJobRepository : IJobRepository
{
    private readonly string _filePath;
    private static readonly object _fileLock = new();
    private const string SharedFileEnv = "SKIJOBCONTROL_JOB_FILE_PATH";

    public FileJobRepository(string filePath = "jobs_mock.json")
    {
        var sharedPath = Environment.GetEnvironmentVariable(SharedFileEnv);
        if (!string.IsNullOrWhiteSpace(sharedPath))
        {
            _filePath = Path.GetFullPath(sharedPath);
        }
        else if (Path.IsPathRooted(filePath))
        {
            _filePath = Path.GetFullPath(filePath);
        }
        else
        {
            _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filePath);
        }

        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        EnsureFileExists();
    }

    private void EnsureFileExists()
    {
        lock (_fileLock)
        {
            if (!File.Exists(_filePath))
            {
                var initialJobs = new List<Job>
                {
                    new Job { JobId = "JOB_001", Payload = "{\"type\":\"sync\", \"data\":\"user_records\"}", Status = 0, UpdateTime = DateTime.Now },
                    new Job { JobId = "JOB_002", Payload = "{\"type\":\"report\", \"data\":\"monthly_sales\"}", Status = 0, UpdateTime = DateTime.Now },
                    new Job { JobId = "JOB_003", Payload = "{\"type\":\"cleanup\", \"data\":\"temp_files\"}", Status = 0, UpdateTime = DateTime.Now },
                    new Job { JobId = "JOB_004", Payload = "{\"type\":\"email\", \"data\":\"welcome_sequence\"}", Status = 0, UpdateTime = DateTime.Now },
                    new Job { JobId = "JOB_005", Payload = "{\"type\":\"backup\", \"data\":\"db_logs\"}", Status = 0, UpdateTime = DateTime.Now }
                };
                File.WriteAllText(_filePath, JsonSerializer.Serialize(initialJobs, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
    }

    private List<Job> ReadJobs()
    {
        lock (_fileLock)
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<Job>>(json) ?? new List<Job>();
        }
    }

    private void SaveJobs(List<Job> jobs)
    {
        lock (_fileLock)
        {
            File.WriteAllText(_filePath, JsonSerializer.Serialize(jobs, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    public async Task<Job?> DequeueJobAsync(string workerId)
    {
        // Simulate SKIP LOCKED with a simple lock and status check
        lock (_fileLock)
        {
            var jobs = ReadJobs();
            var job = jobs.FirstOrDefault(j => j.Status == 0);
            if (job != null)
            {
                job.Status = 1;
                job.WorkerId = workerId;
                job.UpdateTime = DateTime.Now;
                SaveJobs(jobs);
                return job;
            }
            return null;
        }
    }

    public async Task UpdateJobStatusAsync(string jobId, int status, string? workerId = null)
    {
        lock (_fileLock)
        {
            var jobs = ReadJobs();
            var job = jobs.FirstOrDefault(j => j.JobId == jobId);
            if (job != null)
            {
                job.Status = status;
                if (workerId != null) job.WorkerId = workerId;
                job.UpdateTime = DateTime.Now;
                SaveJobs(jobs);
            }
        }
    }

    public async Task<Job?> GetJobByIdAsync(string jobId)
    {
        lock (_fileLock)
        {
            var jobs = ReadJobs();
            return jobs.FirstOrDefault(j => j.JobId == jobId);
        }
    }

    public async Task<int> EnqueueJobsAsync(IEnumerable<Job> jobs)
    {
        lock (_fileLock)
        {
            var currentJobs = ReadJobs();
            var appendJobs = jobs.ToList();
            if (appendJobs.Count == 0)
            {
                return 0;
            }

            currentJobs.AddRange(appendJobs);
            SaveJobs(currentJobs);
            return appendJobs.Count;
        }
    }
}
