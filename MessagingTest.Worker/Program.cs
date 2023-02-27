using Amqp;
using Messaging.Amqp;
using MessagingTest.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;


var buildr = Host.CreateDefaultBuilder()
    .ConfigureLogging((context, builder) =>
    {
        builder.SetMinimumLevel(LogLevel.Debug);
        builder.AddConfiguration(context.Configuration);
    })
    .ConfigureAppConfiguration(builder =>
        builder
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true))
    .ConfigureServices(collection =>
    {
        //collection.AddHostedService<MessagingLib.PR.Test>();
        //collection.AddHostedService<MessagingLib.Nuget.Test>();
        // collection.AddHostedService<MessagingLib.Lite.Test>();
        collection.AddSingleton<List<Address>>(provider => new List<Address>()
            { new("0.0.0.0", 5672, "artemisuser", "artemispassword", "litequeue", "amqp") });
        collection.AddScoped<IMessageProcessor<Payload>, TestProcessor>();
        collection.AddHostedService<QueueListener<Payload>>();
        collection.AddHostedService<SenderService>();
        collection.AddScoped<MessagePublisher>();
    });

var host = buildr.Build();

await host.RunAsync();