using StackExchange.Redis;
using NetHub.Core.Models;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Polly;
using Polly.Retry;

public interface IJobQueue
{
    Task EnqueueJob(ComputeJob job);
    Task<ComputeJob> DequeueJob();
    Task UpdateJobStatus(Guid jobId, JobStatus status);
    Task<ComputeJob> GetJob(Guid jobId);
    Task<IEnumerable<ComputeJob>> GetAllJobs();
}

public class RedisJobQueue : IJobQueue
{
    private readonly ConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly AsyncRetryPolicy _retryPolicy;
    
    private const string JOB_QUEUE_KEY = "jobs:queue";
    private const string JOB_DATA_KEY = "jobs:data:";
    private const string JOB_PROCESSING_KEY = "jobs:processing";
    private const string JOB_FAILED_KEY = "jobs:failed";
    
    public RedisJobQueue(string connectionString)
    {
        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;
        
        _redis = ConnectionMultiplexer.Connect(options);
        _db = _redis.GetDatabase();
        
        // Configure retry policy with exponential backoff
        _retryPolicy = Policy
            .Handle<RedisConnectionException>()
            .Or<RedisTimeoutException>()
            .WaitAndRetryAsync(
                3, // Retry 3 times
                attempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt)),
                (ex, timeSpan, retryCount, context) => 
                {
                    Console.WriteLine($"Redis operation failed: {ex.Message}. Retrying in {timeSpan.TotalMilliseconds}ms. Attempt {retryCount}/3");
                }
            );
            
        // Test connection
        bool isConnected = _redis.IsConnected;
        Console.WriteLine($"Redis connected: {isConnected}");
    }
    
    public async Task EnqueueJob(ComputeJob job)
    {
        try 
        {
            await _retryPolicy.ExecuteAsync(async () => 
            {
                string jobJson = JsonSerializer.Serialize(job);
                string jobKey = $"{JOB_DATA_KEY}{job.Id}";
                
                await _db.StringSetAsync(jobKey, jobJson);
                await _db.ListLeftPushAsync(JOB_QUEUE_KEY, job.Id.ToString());
                
                Console.WriteLine($"Enqueued job {job.Id} of type {job.JobType}");
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to enqueue job {job.Id}: {ex.Message}");
            throw new JobQueueException($"Failed to enqueue job {job.Id}", ex);
        }
    }
    
    public async Task<ComputeJob> DequeueJob()
    {
        try
        {
            return await _retryPolicy.ExecuteAsync(async () => 
            {
                RedisValue jobIdValue = await _db.ListRightPopAsync(JOB_QUEUE_KEY);
                
                if (jobIdValue.IsNullOrEmpty)
                {
                    Console.WriteLine("No jobs in queue");
                    return null;
                }
                
                string jobId = jobIdValue.ToString();
                Console.WriteLine($"Dequeued job ID: {jobId}");
                
                // Mark as processing
                await _db.SetAddAsync(JOB_PROCESSING_KEY, jobId);
                
                string jobKey = $"{JOB_DATA_KEY}{jobId}";
                RedisValue jobJson = await _db.StringGetAsync(jobKey);
                
                if (jobJson.IsNullOrEmpty)
                {
                    Console.WriteLine($"Warning: Job data not found for ID: {jobId}");
                    return null;
                }
                
                var job = JsonSerializer.Deserialize<ComputeJob>(jobJson);
                
                // Update job status to Running
                if (job != null)
                {
                    job.Status = JobStatus.Running;
                    await UpdateJobStatus(job.Id, JobStatus.Running);
                }
                
                return job;
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to dequeue job: {ex.Message}");
            throw new JobQueueException("Failed to dequeue job", ex);
        }
    }
    
    public async Task UpdateJobStatus(Guid jobId, JobStatus status)
    {
        try
        {
            await _retryPolicy.ExecuteAsync(async () => 
            {
                string jobKey = $"{JOB_DATA_KEY}{jobId}";
                RedisValue jobJson = await _db.StringGetAsync(jobKey);

                if (jobJson.IsNullOrEmpty)
                {
                    throw new KeyNotFoundException($"Job with ID {jobId} not found");
                }

                ComputeJob job = JsonSerializer.Deserialize<ComputeJob>(jobJson);
                if (job == null)
                {
                    throw new JobQueueException($"Failed to deserialize job {jobId}");
                }
                
                job.Status = status;
                
                await _db.StringSetAsync(jobKey, JsonSerializer.Serialize(job));
                
                // Remove from processing set if completed or failed
                if (status == JobStatus.Completed || status == JobStatus.Failed)
                {
                    await _db.SetRemoveAsync(JOB_PROCESSING_KEY, jobId.ToString());
                    
                    // Add to failed set if failed
                    if (status == JobStatus.Failed)
                    {
                        await _db.SetAddAsync(JOB_FAILED_KEY, jobId.ToString());
                    }
                }
                
                Console.WriteLine($"Updated job {jobId} status to {status}");
            });
        }
        catch (KeyNotFoundException)
        {
            throw; // Rethrow key not found exceptions
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to update job {jobId}: {ex.Message}");
            throw new JobQueueException($"Failed to update job {jobId}", ex);
        }
    }
    
    public async Task<ComputeJob> GetJob(Guid jobId) 
    {
        try
        {
            return await _retryPolicy.ExecuteAsync(async () => 
            {
                string jobKey = $"{JOB_DATA_KEY}{jobId}";
                RedisValue jobJson = await _db.StringGetAsync(jobKey);
                
                if (jobJson.IsNullOrEmpty)
                {
                    throw new KeyNotFoundException($"Job with ID {jobId} not found");
                }
                
                var job = JsonSerializer.Deserialize<ComputeJob>(jobJson);
                if (job == null)
                {
                    throw new JobQueueException($"Failed to deserialize job {jobId}");
                }
                
                return job;
            });
        }
        catch (KeyNotFoundException)
        {
            throw; // Rethrow key not found exceptions
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to get job {jobId}: {ex.Message}");
            throw new JobQueueException($"Failed to get job {jobId}", ex);
        }
    }

    public async Task<IEnumerable<ComputeJob>> GetAllJobs()
    {
        try
        {
            return await _retryPolicy.ExecuteAsync(async () => 
            {
                var jobs = new List<ComputeJob>();
                
                // Get job IDs from the queue
                RedisValue[] jobIdValues = await _db.ListRangeAsync(JOB_QUEUE_KEY);
                
                foreach (var jobIdValue in jobIdValues)
                {
                    // Convert job ID to string and construct the key
                    string jobId = jobIdValue.ToString();
                    string jobKey = $"{JOB_DATA_KEY}{jobId}";
                    
                    // Get the job data
                    RedisValue jobJson = await _db.StringGetAsync(jobKey);
                    
                    if (!jobJson.IsNullOrEmpty)
                    {
                        var job = JsonSerializer.Deserialize<ComputeJob>(jobJson);
                        if (job != null)
                        {
                            jobs.Add(job);
                        }
                    }
                }
                
                return jobs;
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to get all jobs: {ex.Message}");
            throw new JobQueueException("Failed to get all jobs", ex);
        }
    }
}

public class JobQueueException : Exception
{
    public JobQueueException(string message) : base(message) { }
    public JobQueueException(string message, Exception innerException) : base(message, innerException) { }
}
