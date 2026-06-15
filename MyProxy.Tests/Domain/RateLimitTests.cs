using MyProxy.Domain.Entities;

namespace MyProxy.Tests.DomainRules;

public class RateLimitTests
{
    [Fact]
    public void Create_accepts_positive_request_limit_and_window()
    {
        var rateLimit = RateLimit.Create(requestLimit: 120, window: TimeSpan.FromMinutes(1));

        Assert.Equal(120, rateLimit.RequestLimit);
        Assert.Equal(TimeSpan.FromMinutes(1), rateLimit.Window);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_rejects_non_positive_request_limit(int requestLimit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RateLimit.Create(requestLimit, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void Create_rejects_non_positive_window()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RateLimit.Create(60, TimeSpan.Zero));
    }
}
