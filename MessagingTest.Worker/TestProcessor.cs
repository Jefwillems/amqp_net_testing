using Messaging.Amqp;

namespace MessagingTest.Worker;

public class TestProcessor : IMessageProcessor<Payload>
{
    public Task<ProcessingResult> ProcessMessage(Payload message)
    {
        var result = new ProcessingResult(true, false);
        return Task.FromResult(result);
    }
}