using Apache.NMS;
using Apache.NMS.AMQP;
using Apache.NMS.AMQP.Meta;
using Apache.NMS.Policies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MessagingLib.PR;

public class Test : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<Test> _logger;
    private IMessageConsumer? _consumer;

    private ISession? _session;
    private IConnection? _connection;

    public Test(IServiceScopeFactory scopeFactory, ILogger<Test> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    private void ReceiveMessage(IMessage message)
    {
        using var scope = _scopeFactory.CreateScope();
        var txt = message.Body<string>();

        _logger.LogInformation("message received: {Content}", txt);

        try
        {
            if (txt.Contains('5'))
            {
                throw new ArgumentNullException();
            }

            message.Acknowledge();
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "exception was thrown");
            _session?.Recover();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var x = new NmsConnectionFactory("artemisuser", "artemispassword", "amqp://0.0.0.0:5672");
        _connection = await x.CreateConnectionAsync();
        _connection.RedeliveryPolicy = new RedeliveryPolicy()
        {
            InitialRedeliveryDelay = 100,
            MaximumRedeliveries = 3,
            BackOffMultiplier = 2,
            UseExponentialBackOff = false
        };
        await _connection.StartAsync();
        _session = await _connection.CreateSessionAsync(AcknowledgementMode.IndividualAcknowledge);
        var queue = await _session.GetQueueAsync("pullrequestqueue");
        _logger.LogInformation("creating context");
        _consumer = await _session.CreateConsumerAsync(queue);
        _consumer.Listener += ReceiveMessage;
        _logger.LogInformation("creating producer");
        var producer = await _session.CreateProducerAsync();
        for (var i = 0; i < 10; i++)
        {
            var outMsg = await producer.CreateTextMessageAsync($"Hello world {i}");
            outMsg.NMSDeliveryTime = DateTime.Now.AddSeconds(1);
            await producer.SendAsync(queue, outMsg);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("stop called");


        if (_consumer != null)
        {
            await _consumer.CloseAsync();
        }

        if (_session != null)
        {
            await _session.CloseAsync();
        }

        if (_connection != null)
        {
            await _connection.CloseAsync();
        }


        await base.StopAsync(cancellationToken);
    }
}