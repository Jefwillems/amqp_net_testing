using Messaging.Amqp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MessagingTest.Worker;

public class SenderService : BackgroundService
{
    private readonly ILogger<SenderService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private Timer? _timer = null;

    public SenderService(ILogger<SenderService> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Timed Hosted Service running");

        _timer = new Timer(DoWork, null, TimeSpan.Zero,
            TimeSpan.FromSeconds(15));
        return Task.CompletedTask;
    }

    private async void DoWork(object? state)
    {
        using var scope = _scopeFactory.CreateScope();
        using var publisher = scope.ServiceProvider.GetRequiredService<MessagePublisher>();
        _logger.LogInformation("start sending message");
        await publisher.SendMessage(new Payload() { Name = "Hello world" }, "litequeue");
    }

    public override Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Timed Hosted Service is stopping");

        _timer?.Change(Timeout.Infinite, 0);

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _timer?.Dispose();
        base.Dispose();
    }
}