using SkiJobControl.Core.Repositories;
using SkiJobControl.Core.Models;
using System.Text.Json;

namespace SkiJobControl.Tests;

public class FileJobRepositoryTests : IDisposable
{
    private readonly string _testFile = "test_jobs.json";

    public FileJobRepositoryTests()
    {
        if (File.Exists(_testFile)) File.Delete(_testFile);
    }

    [Fact]
    public async Task DequeueJobAsync_ShouldReturnFirstAvailableJob()
    {
        // Arrange
        var initialJobs = new List<Job>
        {
            new Job { JobId = "TEST_001", Status = 0 },
            new Job { JobId = "TEST_002", Status = 0 }
        };
        File.WriteAllText(_testFile, JsonSerializer.Serialize(initialJobs));
        
        // Use a relative path that we control
        var repo = new FileJobRepository(_testFile);

        // Act
        var job = await repo.DequeueJobAsync("worker_1");

        // Assert
        Assert.NotNull(job);
        Assert.Equal("TEST_001", job.JobId);
        Assert.Equal(1, job.Status);
        Assert.Equal("worker_1", job.WorkerId);
    }

    public void Dispose()
    {
        if (File.Exists(_testFile)) File.Delete(_testFile);
    }
}
