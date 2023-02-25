using Amqp;
using Amqp.Framing;
using Amqp.Listener;
using Amqp.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MessagingLib.Lite;

public class Test : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<Test> _logger;
    private readonly ILoggerFactory _loggerProvider;
    private ContainerHost? _containerhost;


    public Test(IServiceScopeFactory scopeFactory, ILogger<Test> logger, ILoggerFactory loggerProvider)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _loggerProvider = loggerProvider;
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var address = new Address("0.0.0.0", 5672, "artemisuser", "artemispassword", "litequeue", "amqp");
            var connection = await Connection.Factory.CreateAsync(address);
            var session = new Session(connection);
            var sender = new SenderLink(session, "producer.litequeue", "litequeue");
            for (var i = 5; i < 6; i++)
            {
                var msg = new Message() { BodySection = new AmqpValue<string>($"Hello world {i}") };
                _logger.LogInformation("sending message");
                await sender.SendAsync(msg);
            }

            await sender.CloseAsync();
            await session.CloseAsync();
            await connection.CloseAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "someting went wrong while sending message");
            return;
        }

        try
        {
            const string address = "amqp://artemisuser:artemispassword@0.0.0.0:5672";
            var connection = await Connection.Factory.CreateAsync(new Address(address));
            var session = new Session(connection);
            var receiver = new ReceiverLink(session, "consumer.litequeue", "litequeue");
            stoppingToken.Register(async () =>
            {
                await receiver.CloseAsync();
                await session.CloseAsync();
                await connection.CloseAsync();
            });
            _logger.LogInformation("start receiving messages");
            while (!stoppingToken.IsCancellationRequested)
            {
                var message = await receiver.ReceiveAsync();
                if (message == null)
                {
                    _logger.LogWarning("client exiting");
                    break;
                }

                var content = message.GetBody<string>();
                _logger.LogInformation("message received: {Content}", content);
                if (content.Contains('5'))
                {
                    _logger.LogInformation("rejecting messsage");
                    receiver.Modify(message, true, false);
                    //receiver.Reject(message, new Error(new Symbol("message should not contain '5'")));
                }
                else
                {
                    _logger.LogInformation("accepting message");
                    receiver.Accept(message);
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "someting went wrong while receiving message");
            return;
        }
    }

    public class MessageProcessor : IMessageProcessor
    {
        private readonly ILogger<MessageProcessor> _logger;

        public MessageProcessor(ILogger<MessageProcessor> logger)
        {
            _logger = logger;
        }

        public void Process(MessageContext messageContext)
        {
            var msgContent = messageContext.Message.GetBody<string>();
            _logger.LogInformation("message received: {Message}", msgContent);
            if (msgContent.Contains('5'))
            {
                messageContext.Complete(new Error());
            }
            else
            {
                messageContext.Complete();
            }
        }

        public int Credit { get; }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("stop called");
        _containerhost?.Close();
        await base.StopAsync(cancellationToken);
    }
}