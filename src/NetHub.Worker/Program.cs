using NetHub.Core;
using NetHub.Worker;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        // Register the JobQueue
        string redisConnection = hostContext.Configuration.GetValue<string>("Redis:ConnectionString") ?? "localhost:6379";
        services.AddSingleton<IJobQueue>(provider => new RedisJobQueue(redisConnection));
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
