namespace MyProxy.Domain.Entities;

public sealed class RateLimit
{
    private RateLimit()
    {
    }

    private RateLimit(int requestLimit, TimeSpan window)
    {
        Id = Guid.NewGuid();
        SetValues(requestLimit, window);
    }

    public Guid Id { get; private set; }

    public Guid ClientId { get; private set; }

    public Client? Client { get; private set; }

    public int RequestLimit { get; private set; }

    public TimeSpan Window { get; private set; }

    public static RateLimit Create(int requestLimit, TimeSpan window)
    {
        return new RateLimit(requestLimit, window);
    }

    public void Update(int requestLimit, TimeSpan window)
    {
        SetValues(requestLimit, window);
    }

    private void SetValues(int requestLimit, TimeSpan window)
    {
        if (requestLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestLimit), "Request limit must be greater than zero.");
        }

        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window), "Rate-limit window must be greater than zero.");
        }

        RequestLimit = requestLimit;
        Window = window;
    }
}
