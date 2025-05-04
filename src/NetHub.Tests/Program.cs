using NetHub.Core;
using NetHub.Core.Models;
using System;
using System.Threading.Tasks;

namespace NetHub.Tests
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Testing Redis Job Queue...");
            
            try
            {
                string redisConnection = "localhost:6379";
                var jobQueue = new RedisJobQueue(redisConnection);
                
                var job = new ComputeJob
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow,
                    Status = JobStatus.Queued,
                    JobType = "Test",
                    DurationSeconds = 5
                };
                
                Console.WriteLine($"Created job: {job.Id}");
                await jobQueue.EnqueueJob(job);
                Console.WriteLine("Job successfully enqueued!");
                Console.WriteLine($"Check Redis for job ID: {job.Id}");

                Console.WriteLine("\nTrying to dequeue a job...");
                var dequeuedJob = await jobQueue.DequeueJob();

                if (dequeuedJob != null)
                {
                    Console.WriteLine($"Dequeued job: {dequeuedJob.Id}, Type: {dequeuedJob.JobType}");
                }
                else
                {
                    Console.WriteLine("No job was dequeued");
                }

                await jobQueue.UpdateJobStatus(dequeuedJob.Id, JobStatus.Completed);
                Console.WriteLine($"redis-cli> GET jobs:data:{dequeuedJob.Id}");

                var someJob = await jobQueue.GetJob(dequeuedJob.Id);
                Console.WriteLine($"Job ID: {someJob.Id}, Type: {someJob.JobType}, Status: {someJob.Status}");

                var allJobs = await jobQueue.GetAllJobs();
                Console.WriteLine("All jobs:");
                foreach (var thisJob in allJobs)
                {
                    Console.WriteLine($"Job ID: {thisJob.Id}, Type: {thisJob.JobType}, Status: {thisJob.Status}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
