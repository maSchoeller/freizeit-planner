using Identity.Contracts;
using Identity.Implementation;
using FreizeitCockpit.TestSupport;
using Xunit;

namespace Identity.Tests;

public sealed class PasswordlessLoginServiceTests
{
    private static readonly Guid UserId = Guid.Parse("20000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task CodeIsHashedSingleUseAndCreatesTwelveHourSession()
    {
        var fixture = CreateFixture();
        var cancellationToken = TestContext.Current.CancellationToken;

        await fixture.Service.RequestCodeAsync(
            new LoginCodeRequest("team@example.test", "192.0.2.10"),
            cancellationToken);

        var sent = Assert.Single(fixture.Sender.Messages);
        Assert.Matches("^[0-9]{6}$", sent.Code);
        var challenge = await fixture.State.FindCurrentChallengeAsync(
            "TEAM@EXAMPLE.TEST",
            cancellationToken);
        Assert.NotNull(challenge);
        Assert.NotEqual(sent.Code, challenge.CodeHash);
        Assert.Equal(64, challenge.CodeHash.Length);

        var success = await fixture.Service.VerifyCodeAsync(
            new LoginCodeVerification("team@example.test", sent.Code, "192.0.2.10", false),
            cancellationToken);
        var reused = await fixture.Service.VerifyCodeAsync(
            new LoginCodeVerification("team@example.test", sent.Code, "192.0.2.10", false),
            cancellationToken);

        Assert.Equal(LoginOutcome.Succeeded, success.Outcome);
        Assert.Equal(fixture.Clock.GetUtcNow().AddHours(12), success.Session?.ExpiresAt);
        Assert.Equal(LoginOutcome.InvalidCode, reused.Outcome);
    }

    [Fact]
    public async Task ExpiredCodeAndFiveFailuresCannotAuthenticate()
    {
        var fixture = CreateFixture();
        var cancellationToken = TestContext.Current.CancellationToken;
        await fixture.Service.RequestCodeAsync(
            new LoginCodeRequest("team@example.test", "192.0.2.11"),
            cancellationToken);
        var code = Assert.Single(fixture.Sender.Messages).Code;

        fixture.Clock.Advance(TimeSpan.FromMinutes(10));
        var expired = await fixture.Service.VerifyCodeAsync(
            new LoginCodeVerification("team@example.test", code, "192.0.2.11", false),
            cancellationToken);
        Assert.Equal(LoginOutcome.Expired, expired.Outcome);

        fixture.Clock.Advance(TimeSpan.FromMinutes(16));
        await fixture.Service.RequestCodeAsync(
            new LoginCodeRequest("team@example.test", "192.0.2.11"),
            cancellationToken);
        var freshCode = fixture.Sender.Messages[^1].Code;
        LoginResult? fifthFailure = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            fifthFailure = await fixture.Service.VerifyCodeAsync(
                new LoginCodeVerification("team@example.test", "000000", "192.0.2.11", false),
                cancellationToken);
        }

        var afterLock = await fixture.Service.VerifyCodeAsync(
            new LoginCodeVerification("team@example.test", freshCode, "192.0.2.11", false),
            cancellationToken);
        Assert.Equal(LoginOutcome.AttemptsExceeded, fifthFailure?.Outcome);
        Assert.Equal(LoginOutcome.AttemptsExceeded, afterLock.Outcome);
    }

    [Fact]
    public async Task RememberedSessionLastsThirtyDaysAndCanBeRevokedImmediately()
    {
        var fixture = CreateFixture();
        var cancellationToken = TestContext.Current.CancellationToken;
        await fixture.Service.RequestCodeAsync(
            new LoginCodeRequest("team@example.test", "192.0.2.12"),
            cancellationToken);
        var code = Assert.Single(fixture.Sender.Messages).Code;
        var result = await fixture.Service.VerifyCodeAsync(
            new LoginCodeVerification("team@example.test", code, "192.0.2.12", true),
            cancellationToken);

        Assert.Equal(fixture.Clock.GetUtcNow().AddDays(30), result.Session?.ExpiresAt);
        Assert.True(await fixture.Service.IsSessionActiveAsync(result.Session!.Id, cancellationToken));

        await fixture.Service.RevokeSessionAsync(UserId, result.Session.Id, cancellationToken);
        Assert.False(await fixture.Service.IsSessionActiveAsync(result.Session.Id, cancellationToken));
    }

    [Fact]
    public async Task RequestLimitAppliesToEmailAndIpWithoutLeakingUnknownUsers()
    {
        var fixture = CreateFixture();
        var cancellationToken = TestContext.Current.CancellationToken;
        for (var request = 0; request < 6; request++)
        {
            await fixture.Service.RequestCodeAsync(
                new LoginCodeRequest("team@example.test", "192.0.2.13"),
                cancellationToken);
        }

        await fixture.Service.RequestCodeAsync(
            new LoginCodeRequest("unknown@example.test", "192.0.2.14"),
            cancellationToken);

        Assert.Equal(5, fixture.Sender.Messages.Count);
    }

    private static Fixture CreateFixture()
    {
        var state = new PasswordlessTestState(new[]
        {
            new KnownUser(UserId, "TEAM@EXAMPLE.TEST", "Teammitglied")
        });
        var sender = new CapturingSender();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 8, 7, 8, 0, 0, TimeSpan.Zero));
        var service = new PasswordlessLoginService(state, sender, clock, Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());
        return new Fixture(state, sender, clock, service);
    }

    private sealed record Fixture(
        PasswordlessTestState State,
        CapturingSender Sender,
        ManualTimeProvider Clock,
        PasswordlessLoginService Service);

    private sealed class CapturingSender : ILoginCodeSender
    {
        public List<SentCode> Messages { get; } = [];

        public Task SendAsync(
            string email,
            string code,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Messages.Add(new SentCode(email, code, expiresAt));
            return Task.CompletedTask;
        }
    }

    private sealed record SentCode(string Email, string Code, DateTimeOffset ExpiresAt);

    private sealed class ManualTimeProvider(DateTimeOffset initial) : TimeProvider
    {
        private DateTimeOffset current = initial;

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan duration) => current = current.Add(duration);
    }
}
