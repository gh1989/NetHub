using StackExchange.Redis;
using NetHub.Core.Models;
using System.Text.Json;

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
    private const string JOB_QUEUE_KEY = "jobs:queue";
    private const string JOB_DATA_KEY = "jobs:data:";
    
    public RedisJobQueue(string connectionString)
    {
        _redis = ConnectionMultiplexer.Connect(connectionString);
        _db = _redis.GetDatabase();
        
        // Test connection
        bool isConnected = _redis.IsConnected;
        Console.WriteLine($"Redis connected: {isConnected}");
    }
    
    public async Task EnqueueJob(ComputeJob job)
    {
        string jobJson = System.Text.Json.JsonSerializer.Serialize(job);
        string jobKey = $"{JOB_DATA_KEY}{job.Id}";
        await _db.StringSetAsync(jobKey, jobJson); 
        await _db.ListLeftPushAsync(JOB_QUEUE_KEY, job.Id.ToString());
        Console.WriteLine($"Enqueued job {job.Id} of type {job.JobType}");
    }
    
    public async Task<ComputeJob> DequeueJob()
    {
        RedisValue jobIdValue = await _db.ListRightPopAsync(JOB_QUEUE_KEY);
        
        if (jobIdValue.IsNullOrEmpty)
        {
            Console.WriteLine("No jobs in queue");
            return null;
        }
        
        string jobId = jobIdValue.ToString();
        Console.WriteLine($"Dequeued job ID: {jobId}");
        string jobKey = $"{JOB_DATA_KEY}{jobId}";
        RedisValue jobJson = await _db.StringGetAsync(jobKey);
        
        if (jobJson.IsNullOrEmpty)
        {
            Console.WriteLine($"Warning: Job data not found for ID: {jobId}");
            return null;
        }
        
        return JsonSerializer.Deserialize<ComputeJob>(jobJson);
    }
    
    public async Task UpdateJobStatus(Guid jobId, JobStatus status)
    {
        string jobKey = $"{JOB_DATA_KEY}{jobId}";
        RedisValue jobJson = await _db.StringGetAsync(jobKey);

        if (jobJson.IsNullOrEmpty)
        {
            throw new KeyNotFoundException($"Job with ID {jobId} not found");
        }

        ComputeJob job = JsonSerializer.Deserialize<ComputeJob>(jobJson);
        job.Status = status;
        await _db.StringSetAsync(jobKey, JsonSerializer.Serialize(job));
        Console.WriteLine($"Updated job {jobId} status to {status}");
    }
    
    public async Task<ComputeJob> GetJob(Guid jobId) 
    {
        string jobKey = $"{JOB_DATA_KEY}{jobId}";
        RedisValue jobJson = await _db.StringGetAsync(jobKey);
        return JsonSerializer.Deserialize<ComputeJob>(jobJson);
    }

    public async Task<IEnumerable<ComputeJob>> GetAllJobs()
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
    }
}
