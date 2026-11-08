using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;
using Xunit;

namespace Sugarista.Domain.UnitTests;

public class CustomerSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_BuildsActiveSessionWithHashedTokenAndConfiguredExpiry()
    {
        var token = CustomerSession.NewToken();
        var session = new CustomerSession(Guid.NewGuid(), token, Now, lifetimeDays: 60);

        Assert.Equal(Now.AddDays(60), session.ExpiresAtUtc);
        Assert.Equal(Now, session.LastSeenAtUtc);
        Assert.Null(session.RevokedAtUtc);
        Assert.True(session.IsActiveAt(Now));
        // The raw token must never be persisted — only its SHA-256 hash.
        Assert.NotEqual(token, session.TokenHash);
        Assert.Equal(64, session.TokenHash.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Constructor_RejectsNonPositiveLifetime(int lifetimeDays)
    {
        Assert.Throws<DomainException>(
            () => new CustomerSession(Guid.NewGuid(), CustomerSession.NewToken(), Now, lifetimeDays));
    }

    [Fact]
    public void Constructor_RejectsEmptyCustomerIdAndToken()
    {
        Assert.Throws<DomainException>(() => new CustomerSession(Guid.Empty, CustomerSession.NewToken(), Now, 60));
        Assert.Throws<DomainException>(() => new CustomerSession(Guid.NewGuid(), "  ", Now, 60));
    }

    [Fact]
    public void IsActiveAt_IsFalseAfterExpiryAndAfterRevocation()
    {
        var session = new CustomerSession(Guid.NewGuid(), CustomerSession.NewToken(), Now, lifetimeDays: 30);

        Assert.False(session.IsActiveAt(Now.AddDays(30)));
        Assert.True(session.IsActiveAt(Now.AddDays(29).AddHours(23)));

        session.Revoke(Now.AddDays(1));
        Assert.NotNull(session.RevokedAtUtc);
        Assert.False(session.IsActiveAt(Now.AddDays(1).AddSeconds(1)));
        // Idempotent revocation keeps the first moment.
        session.Revoke(Now.AddDays(5));
        Assert.Equal(Now.AddDays(1), session.RevokedAtUtc);
    }

    [Fact]
    public void Touch_RecordsActivityAtMostOncePerHour()
    {
        var session = new CustomerSession(Guid.NewGuid(), CustomerSession.NewToken(), Now, lifetimeDays: 30);

        session.Touch(Now.AddMinutes(30));
        Assert.Equal(Now, session.LastSeenAtUtc);

        session.Touch(Now.AddHours(1));
        Assert.Equal(Now.AddHours(1), session.LastSeenAtUtc);
    }

    [Fact]
    public void HashToken_IsDeterministicHexAndDifferentPerToken()
    {
        var first = CustomerSession.HashToken("token-a");
        var second = CustomerSession.HashToken("token-a");

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
        Assert.All(first, c => Assert.True(Uri.IsHexDigit(c)));
        Assert.NotEqual(first, CustomerSession.HashToken("token-b"));
        Assert.Throws<DomainException>(() => CustomerSession.HashToken(" "));
    }

    [Fact]
    public void NewToken_GeneratesRecordableRandomTokens()
    {
        var tokens = Enumerable.Range(0, 50).Select(_ => CustomerSession.NewToken()).ToHashSet();
        Assert.Equal(50, tokens.Count);
        Assert.All(tokens, token => Assert.NotEmpty(token));
    }
}
