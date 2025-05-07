using NetHub.Core;
using NetHub.Core.Models;

namespace NetHub.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IJobQueue _jobQueue;
    private readonly IConfiguration _configuration;

    public Worker(ILogger<Worker> logger, IJobQueue jobQueue, IConfiguration configuration)
    {
        _logger = logger;
        _jobQueue = jobQueue;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker service started at: {time}", DateTimeOffset.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await _jobQueue.DequeueJob();

                if (job != null)
                {
                    _logger.LogInformation("Processing job {JobId} of type {JobType}", job.Id, job.JobType);
                    await ProcessJob(job, stoppingToken);
                    _logger.LogInformation("Completed job {JobId}", job.Id);
                }
                else
                {
                    // No jobs to process, wait a bit before checking again
                    await Task.Delay(5000, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing job");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }
    
    private async Task ProcessJob(ComputeJob job, CancellationToken stoppingToken)
    {
        try
        {
            // Simulate job processing time
            await Task.Delay(job.DurationSeconds * 1000, stoppingToken);
            
            // Update job status to completed
            await _jobQueue.UpdateJobStatus(job.Id, JobStatus.Completed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process job {JobId}", job.Id);
            
            // Update job status to failed
            await _jobQueue.UpdateJobStatus(job.Id, JobStatus.Failed);
            throw;
        }
    }
}
