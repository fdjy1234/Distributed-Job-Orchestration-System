using Dapper;
using Oracle.ManagedDataAccess.Client;
using SkiJobControl.Core.Models;

namespace SkiJobControl.Core.Repositories;

public interface IJobRepository
{
    Task<Job?> DequeueJobAsync(string workerId);
    Task UpdateJobStatusAsync(string jobId, int status, string? workerId = null);
    Task<Job?> GetJobByIdAsync(string jobId);
    Task<int> EnqueueJobsAsync(IEnumerable<Job> jobs);
}

public class OracleJobRepository : IJobRepository
{
    private readonly string _connectionString;

    public OracleJobRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Job?> DequeueJobAsync(string workerId)
    {
        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            const string sql = @"
                SELECT job_id as JobId, payload, status, worker_id as WorkerId, update_time as UpdateTime
                FROM jobs 
                WHERE status = 0 AND ROWNUM <= 1 
                FOR UPDATE SKIP LOCKED";

            var job = await connection.QueryFirstOrDefaultAsync<Job>(sql, transaction: transaction);

            if (job != null)
            {
                const string updateSql = @"
                    UPDATE jobs 
                    SET status = 1, worker_id = :workerId, update_time = CURRENT_TIMESTAMP
                    WHERE job_id = :jobId";
                
                await connection.ExecuteAsync(updateSql, new { workerId, jobId = job.JobId }, transaction: transaction);
                await transaction.CommitAsync();
                
                job.Status = 1;
                job.WorkerId = workerId;
            }

            return job;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task UpdateJobStatusAsync(string jobId, int status, string? workerId = null)
    {
        using var connection = new OracleConnection(_connectionString);
        const string sql = @"
            UPDATE jobs 
            SET status = :status, 
                worker_id = COALESCE(:workerId, worker_id), 
                update_time = CURRENT_TIMESTAMP
            WHERE job_id = :jobId";
        
        await connection.ExecuteAsync(sql, new { status, workerId, jobId });
    }

    public async Task<Job?> GetJobByIdAsync(string jobId)
    {
        using var connection = new OracleConnection(_connectionString);
        const string sql = @"
            SELECT job_id as JobId, payload, status, worker_id as WorkerId, update_time as UpdateTime
            FROM jobs 
            WHERE job_id = :jobId";
        return await connection.QueryFirstOrDefaultAsync<Job>(sql, new { jobId });
    }

    public async Task<int> EnqueueJobsAsync(IEnumerable<Job> jobs)
    {
        var jobList = jobs.ToList();
        if (jobList.Count == 0)
        {
            return 0;
        }

        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        const string sql = @"
            INSERT INTO jobs (job_id, payload, status, worker_id, update_time)
            VALUES (:jobId, :payload, :status, :workerId, :updateTime)";

        foreach (var job in jobList)
        {
            await connection.ExecuteAsync(sql, new
            {
                jobId = job.JobId,
                payload = job.Payload,
                status = job.Status,
                workerId = job.WorkerId,
                updateTime = job.UpdateTime
            }, transaction: transaction);
        }

        await transaction.CommitAsync();
        return jobList.Count;
    }
}
