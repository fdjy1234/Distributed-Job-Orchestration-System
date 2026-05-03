using SkiJobControl.Core.Repositories;

namespace SkiJobControl.Worker;

class Program
{
    static async Task Main(string[] args)
    {
        var repository = new FileJobRepository();
        // Set up graceful shutdown handling
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        Console.WriteLine("STATUS:Idle");

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var command = await Console.In.ReadLineAsync();
                if (command == null) break;

                if (command.StartsWith("START_JOB:"))
                {
                    var jobId = command.Substring(10);
                    await ExecuteJob(repository, jobId, cts.Token);
                }
                else if (command == "TERMINATE")
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        finally
        {
            Console.WriteLine("STATUS:Exiting");
        }
    }

    static async Task ExecuteJob(IJobRepository repository, string jobId, CancellationToken ct)
    {
        Console.WriteLine($"JOB_STARTED:{jobId}");
        var job = await repository.GetJobByIdAsync(jobId);
        string payload = job?.Payload ?? "{}";
        
        try
        {
            // Simulate work
            for (int i = 0; i < 5; i++)
            {
                if (ct.IsCancellationRequested) break;
                await Task.Delay(1000, ct);
                Console.WriteLine($"STATUS:Running (Job {jobId}, {i+1}/5)");
            }
            
            await repository.UpdateJobStatusAsync(jobId, 2); // Success
            Console.WriteLine($"JOB_FINISHED:{jobId}");
        }
        catch (Exception ex)
        {
            await repository.UpdateJobStatusAsync(jobId, 9); // Failed
            Console.WriteLine($"JOB_FAILED:{jobId} Error: {ex.Message}");
        }
        finally
        {
            Console.WriteLine("STATUS:Idle");
        }
    }
}
