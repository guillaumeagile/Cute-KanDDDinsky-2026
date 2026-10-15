using OmniProduct_CoreDomain.Abstractions;

namespace OmniProduct_CoreDomain.Models;

public class Supplier: IDentifiable
{
    public Ulid Id { get; set; }

    public string Name { get; set; }

    public string Email { get; set; }

    public string Region { get; set; }

    public List<Ulid> ProductIds { get; set; } = new();
}
