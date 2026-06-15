using MyProxy.Domain.Rules;
using Xunit;

namespace MyProxy.Tests.DomainRules;

public class ScopeValidatorTests
{
    [Theory]
    [InlineData("read:flights")]
    [InlineData("write:airports")]
    [InlineData("manage:clients")]
    public void IsValid_returns_true_for_valid_scope(string scope)
    {
        Assert.True(ScopeValidator.IsValid(scope));
    }

    [Theory]
    [InlineData("")]
    [InlineData("readflights")]
    [InlineData("read:")]
    [InlineData(":flights")]
    [InlineData("read:flight data")]
    [InlineData("READ:flights")]
    public void IsValid_returns_false_for_invalid_scope(string scope)
    {
        Assert.False(ScopeValidator.IsValid(scope));
    }

    [Theory]
    [InlineData(" read:flights ", "read:flights")]
    [InlineData("write:airports", "write:airports")]
    public void Normalize_trims_valid_scope(string scope, string expected)
    {
        Assert.Equal(expected, ScopeValidator.Normalize(scope));
    }

    [Fact]
    public void Normalize_throws_for_invalid_scope()
    {
        Assert.Throws<ArgumentException>(() => ScopeValidator.Normalize("readflights"));
    }
}