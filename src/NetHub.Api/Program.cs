using NetHub.Core;
using NetHub.Core.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

// Register the JobQueue
string redisConnection = builder.Configuration.GetValue<string>("Redis:ConnectionString") ?? "localhost:6379";
builder.Services.AddSingleton<IJobQueue>(provider => new RedisJobQueue(redisConnection));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();

// Job API endpoints
app.MapPost("/api/jobs", async (IJobQueue jobQueue, ComputeJobRequest request) =>
{
    var job = new ComputeJob
    {
        Id = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
        Status = JobStatus.Queued,
        JobType = request.JobType,
        DurationSeconds = request.DurationSeconds
    };

    await jobQueue.EnqueueJob(job);
    return Results.Created($"/api/jobs/{job.Id}", job);
})
.WithName("CreateJob")
.WithOpenApi();

app.MapGet("/api/jobs/{id}", async (IJobQueue jobQueue, Guid id) =>
{
    try
    {
        var job = await jobQueue.GetJob(id);
        return Results.Ok(job);
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
})
.WithName("GetJob")
.WithOpenApi();

app.MapGet("/api/jobs", async (IJobQueue jobQueue) =>
{
    var jobs = await jobQueue.GetAllJobs();
    return Results.Ok(jobs);
})
.WithName("GetAllJobs")
.WithOpenApi();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

// Request DTO
public class ComputeJobRequest
{
    public string JobType { get; set; } = "Simulation";
    public int DurationSeconds { get; set; } = 10;
}
