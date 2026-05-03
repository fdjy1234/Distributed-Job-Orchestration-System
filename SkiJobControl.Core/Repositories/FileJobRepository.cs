using System.Text.Json;
using SkiJobControl.Core.Models;

namespace SkiJobControl.Core.Repositories;

public class FileJobRepository : IJobRepository
{
    private readonly string _filePath;
    private static readonly object _fileLock = new();

    public FileJobRepository(string filePath = "jobs_mock.json")
    {
        _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filePath);
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
}
