using OmniProduct_CoreDomain.Abstractions;

namespace OmniProduct_CoreDomain.Events;

public class Notification : IDentifiable
{
    public Ulid Id { get; set; }

    public string Recipient { get; set; }

    public string Subject { get; set; }

    public string Body { get; set; }

    public DateTime SentAt { get; set; }

    public Ulid ProductId { get; set; }
}
