namespace NetHub.Core.Models;

public class ComputeJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public JobStatus Status { get; set; } = JobStatus.Queued;
    public string JobType { get; set; } = "Simulation"; // Simple string for now
    public int DurationSeconds { get; set; } = 10; // How long the simulation should run
}

public enum JobStatus
{
    Queued,
    Running,
    Completed,
    Failed
}