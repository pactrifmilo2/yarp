using MyProxy.Domain.Rules;

namespace MyProxy.Domain.Entities;

public sealed class Scope
{
    private Scope()
    {
        Name = string.Empty;
    }

    private Scope(string name)
    {
        Id = Guid.NewGuid();
        Name = ScopeValidator.Normalize(name);
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public Guid? ClientId { get; private set; }

    public Client? Client { get; private set; }

    public Guid? RouteDefinitionId { get; private set; }

    public RouteDefinition? RouteDefinition { get; private set; }

    public static Scope Create(string name)
    {
        return new Scope(name);
    }
}
