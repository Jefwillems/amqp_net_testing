using System.ComponentModel.DataAnnotations;

namespace Messaging.Amqp.Configuration;

public class MessagingOptions
{
    [Required]
    public string Uri { get; set; }

    [Required]
    public string Username { get; set; }

    [Required]
    public string Password { get; set; }
}