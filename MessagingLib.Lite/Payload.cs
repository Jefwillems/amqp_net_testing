using Amqp.Serialization;

namespace MessagingLib.Lite;

[AmqpContract(Name = "custom-payload", Encoding = EncodingType.SimpleMap)]
public class Payload
{
    [AmqpMember]
    public string Name { get; set; }

    [AmqpMember]
    public DateTime CreationDate { get; set; }

    [AmqpMember]
    public Dictionary<string, string> Tags { get; set; }
}