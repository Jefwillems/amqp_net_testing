using Amqp;
using Amqp.Framing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Messaging.Amqp;

public class QueueListener<TMessageType> : BackgroundService
{
    private readonly ILogger<QueueListener<TMessageType>> _logger;
    private readonly List<Address> _addresses;
    private Session? _session;
    private Connection? _connection;
    private ReceiverLink? _receiver;
    private readonly IMessageProcessor<TMessageType> _processor;
    private int _aIndex;
    private readonly ManualResetEvent _connected = new(false);

    public QueueListener(
        ILogger<QueueListener<TMessageType>> logger,
        List<Address> addresses,
        IMessageProcessor<TMessageType> processor)
    {
        _logger = logger;
        _addresses = addresses;
        _processor = processor;
        _aIndex = 0;
    }

    /// <summary>
    /// AMQP session has opened
    /// This is where you should initiate a link 
    /// </summary>
    /// <param name="_">Which session (ignored).</param>
    /// <param name="__">Peer AMQP Begin (ignored).</param>
    private void OnBegin(ISession _, Begin __)
    {
        _logger.LogDebug("Event: {Event}", nameof(OnBegin));
        var sourceName = _addresses[_aIndex].Path;
        var source = new Source() { Address = sourceName };
        _receiver = new ReceiverLink(_session, "receiver-link", source, OnAttached);
    }


    private async Task OpenConnection()
    {
        try
        {
            _connection = await Connection.Factory.CreateAsync(_addresses[_aIndex], null, OnOpened);
            _connection.AddClosedCallback(ConnectionClosed);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "error while creating connection for {Scheme}://{Host}:{Port}",
                _addresses[_aIndex].Scheme, _addresses[_aIndex].Host, _addresses[_aIndex].Port);
        }
    }

    /// <summary>
    /// Connection closed event handler
    /// 
    /// This function provides information only. Calling Reconnect is redundant with
    /// calls from the Run loop.
    /// </summary>
    /// <param name="_">Connection that closed. There is only one connection so this is ignored.</param>
    /// <param name="error">Error object associated with connection close.</param>
    private void ConnectionClosed(IAmqpObject _, Error? error)
    {
        if (error == null)
        {
            _logger.LogWarning("connection closed with no error");
        }
        else
        {
            _logger.LogError("connection closed with error: {Error}", error.ToString());
        }
    }

    /// <summary>
    /// Select the next host in the Address list and start it
    /// </summary>
    private async Task Reconnect()
    {
        _logger.LogDebug("Event: {Event}", nameof(Reconnect));

        _connected.Reset();
        if (++_aIndex >= _addresses.Count) _aIndex = 0;
        await OpenConnection();
    }

    /// <summary>
    /// AMQP connection has opened. This callback may be called before
    /// ConnectAsync returns a value to the _connection_ variable.
    /// </summary>
    /// <param name="conn">Which connection. </param>
    /// <param name="__">Peer AMQP Open (ignored).</param>
    private void OnOpened(IConnection conn, Open __)
    {
        _logger.LogDebug("Event: {Event}", nameof(OnOpened));
        _connection = (Connection)conn;

        var begin = new Begin()
        {
            IncomingWindow = 2048,
            OutgoingWindow = 2048,
            NextOutgoingId = 0
        };

        _session = new Session(_connection, begin, OnBegin);
    }


    /// <summary>
    /// AMQP Link has attached. Signal that protocol stack is ready to send.
    /// </summary>
    /// <param name="_">Which link (ignored).</param>
    /// <param name="__">Peer AMQP Attach (ignored).</param>
    protected void OnAttached(ILink _, Attach __)
    {
        _logger.LogDebug("Event: {Event}", nameof(OnAttached));
        _connected.Set();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await OpenConnection();

        stoppingToken.Register(() =>
        {
            _receiver?.Close();
            _session?.Close();
            _connection?.Close();
        });

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_connected.WaitOne(TimeSpan.FromSeconds(10)))
            {
                Message? message = null;

                try
                {
                    message = await _receiver!.ReceiveAsync();
                    if (message == null)
                    {
                        _logger.LogWarning("client exiting due to timeout");
                        if (stoppingToken.IsCancellationRequested)
                        {
                            break;
                        }

                        await Reconnect();
                        continue;
                    }

                    var content = message.GetBody<TMessageType>();
                    var result = await _processor.ProcessMessage(content);
                    if (!result.ProcessingFailed)
                    {
                        _logger.LogDebug("accepting message");
                        _receiver.Accept(message);
                    }
                    else
                    {
                        _logger.LogDebug("not accepting meessage");
                        _receiver.Modify(message, result.ProcessingFailed, result.UndeliverableHere);
                    }
                }
                catch (AmqpException e)
                {
                    _logger.LogError(e, "amqp exception while receiving message, reconnecting");
                    await Reconnect();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "error while processing message");
                    if (message != null)
                        _receiver?.Modify(message, true, true);
                }
            }
            else
            {
                _logger.LogWarning("connection timed out, reconnecting");
                await Reconnect();
            }
        }
    }


    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("stop called");

        if (_receiver?.IsClosed == false)
        {
            _receiver?.Close();
        }

        if (_session?.IsClosed == false)
        {
            _session?.Close();
        }

        if (_connection?.IsClosed == false)
        {
            _connection?.Close();
        }

        return base.StopAsync(cancellationToken);
    }
}