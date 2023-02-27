using Amqp.Serialization;

namespace MessagingTest.Worker;

[AmqpContract(Encoding = EncodingType.SimpleMap)]
public class Payload
{
    [AmqpMember]
    public string Name { get; set; }
}