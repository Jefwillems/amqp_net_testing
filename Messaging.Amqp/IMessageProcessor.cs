namespace Messaging.Amqp;

public interface IMessageProcessor<in TMessageType>
{
    Task<ProcessingResult> ProcessMessage(TMessageType message);
}