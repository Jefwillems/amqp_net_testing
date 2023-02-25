using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


var host = Host.CreateDefaultBuilder()
    .ConfigureAppConfiguration(builder =>
        builder
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true))
    .ConfigureServices(collection =>
    {
        //collection.AddHostedService<MessagingLib.PR.Test>();
        //collection.AddHostedService<MessagingLib.Nuget.Test>();
        collection.AddHostedService<MessagingLib.Lite.Test>();
    }).Build();

await host.RunAsync();