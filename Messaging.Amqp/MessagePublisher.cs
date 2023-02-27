using Amqp;
using Amqp.Framing;
using Microsoft.Extensions.Logging;

namespace Messaging.Amqp;

public class MessagePublisher : IDisposable
{
    // AMQP connection selection
    private readonly ILogger<MessagePublisher> _logger;
    private readonly List<Address> _addresses;
    private int _aIndex = 0;

    // Protocol objects
    private Connection? _connection;
    private Session? _session;
    private SenderLink? _sender;

    // Sender is ready to send messages 
    private readonly ManualResetEvent _sendable = new(false);


    /// <summary>
    /// Application constructor
    /// </summary>
    /// <param name="logger">logger</param>
    /// <param name="addresses">Address objects that define the host, port, and target for messages.</param>
    public MessagePublisher(ILogger<MessagePublisher> logger, List<Address> addresses)
    {
        _logger = logger;
        _addresses = addresses;
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
            _logger.LogWarning("Connection closed with no error");
        else
            _logger.LogError("Connection closed with error: {Error}", error.ToString());
    }

    /// <summary>
    /// Select the next host in the Address list and start it
    /// </summary>
    private void Reconnect()
    {
        _logger.LogDebug("Event: Entering Reconnect()");

        _sendable.Reset();

        if (++_aIndex >= _addresses.Count) _aIndex = 0;
        OpenConnection();
    }


    /// <summary>
    /// Start the current host in the address list
    /// </summary>
    private async Task OpenConnection()
    {
        try
        {
            _logger.LogDebug(
                "Attempting connection to  {Host}:{Port}",
                _addresses[_aIndex].Host, _addresses[_aIndex].Port);

            _connection = await Connection.Factory.CreateAsync(_addresses[_aIndex], null, OnOpened);

            _logger.LogDebug(
                "Success: connecting to {Host}:{Port}",
                _addresses[_aIndex].Host, _addresses[_aIndex].Port);

            _connection.AddClosedCallback(ConnectionClosed);
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "Failure: exception connecting to '{Host}:{Port}': {Message}",
                _addresses[_aIndex].Host, _addresses[_aIndex].Port, e.Message);
        }
    }

    /// <summary>
    /// AMQP connection has opened. This callback may be called before
    /// ConnectAsync returns a value to the _connection_ variable.
    /// </summary>
    /// <param name="conn">Which connection. </param>
    /// <param name="__">Peer AMQP Open (ignored).</param>
    private void OnOpened(IConnection conn, Open __)
    {
        _logger.LogDebug("Event: OnOpened");

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
    /// AMQP session has opened
    /// </summary>
    /// <param name="_">Which session (ignored).</param>
    /// <param name="__">Peer AMQP Begin (ignored).</param>
    private void OnBegin(ISession _, Begin __)
    {
        _logger.LogDebug("Event: OnBegin");

        var targetName = _addresses[_aIndex].Path; // no leading '/'
        var target = new Target { Address = targetName };
        _sender = new SenderLink(_session, "senderLink", target, OnAttached);
    }

    /// <summary>
    /// AMQP Link has attached. Signal that protocol stack is ready to send.
    /// </summary>
    /// <param name="_">Which link (ignored).</param>
    /// <param name="__">Peer AMQP Attach (ignored).</param>
    private void OnAttached(ILink _, Attach __)
    {
        _logger.LogDebug("Event: OnAttached");

        _sendable.Set();
    }

    public async Task SendMessage<TMessageType>(TMessageType msg, string address)
    {
        await OpenConnection();
        if (_sendable.WaitOne(TimeSpan.FromSeconds(10)))
        {
            try
            {
                _logger.LogDebug("Sending message {Message}", msg);

                var message = new Message() { BodySection = new AmqpValue<TMessageType>(msg) };
                message.Properties = new Properties();
                message.Properties.SetMessageId(Guid.NewGuid().ToString("D"));
                await _sender.SendAsync(message);

                _logger.LogDebug("Sent message");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception sending message {Message}", e.Message);
                Reconnect();
            }
        }
        else
        {
            _logger.LogWarning("timed out waiting for connection");
            Reconnect();
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _sender?.Close();
        _session?.Close();
        _connection?.Close();
    }
}