using Identity.Contracts;
using Identity.Implementation;
using Xunit;

namespace Identity.Tests;

public sealed class EmailChangeTests
{
    [Fact]
    public async Task NewEmailIsAppliedOnlyAfterAValidSingleUseLoginCode()
    {
        var userId = Guid.Parse("51000000-0000-0000-0000-000000000001");
        var state = new EmailChangeTestState(userId, "old@example.test");
        var sender = new CapturingEmailChangeCodeSender();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 8, 7, 8, 0, 0, TimeSpan.Zero));
        var service = new EmailChangeService(
            state,
            sender,
            clock,
            Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());
        var cancellationToken = TestContext.Current.CancellationToken;

        await service.RequestAsync(
            new EmailChangeRequest(userId, "new@example.test", "192.0.2.50"),
            cancellationToken);

        Assert.Equal("old@example.test", state.User.Email);
        Assert.NotNull(sender.Code);
        Assert.DoesNotContain(sender.Code!, state.Challenge!.CodeHash, StringComparison.Ordinal);

        var changed = await service.ConfirmAsync(
            new ConfirmEmailChangeRequest(userId, "new@example.test", sender.Code!),
            cancellationToken);
        var reused = await service.ConfirmAsync(
            new ConfirmEmailChangeRequest(userId, "new@example.test", sender.Code!),
            cancellationToken);

        Assert.Equal(EmailChangeOutcome.Changed, changed.Outcome);
        Assert.Equal("new@example.test", changed.Email);
        Assert.Equal("new@example.test", state.User.Email);
        Assert.Equal(EmailChangeOutcome.Invalid, reused.Outcome);
    }

    [Fact]
    public async Task EmailChangeCodeExpiresAfterTenMinutesAndLocksAfterFiveFailures()
    {
        var userId = Guid.Parse("51000000-0000-0000-0000-000000000002");
        var state = new EmailChangeTestState(userId, "old@example.test");
        var sender = new CapturingEmailChangeCodeSender();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 8, 7, 8, 0, 0, TimeSpan.Zero));
        var service = new EmailChangeService(state, sender, clock, new byte[32]);
        var cancellationToken = TestContext.Current.CancellationToken;

        await service.RequestAsync(
            new EmailChangeRequest(userId, "new@example.test", "192.0.2.51"),
            cancellationToken);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            _ = await service.ConfirmAsync(
                new ConfirmEmailChangeRequest(userId, "new@example.test", "000000"),
                cancellationToken);
        }
        var locked = await service.ConfirmAsync(
            new ConfirmEmailChangeRequest(userId, "new@example.test", sender.Code!),
            cancellationToken);

        await service.RequestAsync(
            new EmailChangeRequest(userId, "later@example.test", "192.0.2.52"),
            cancellationToken);
        clock.Advance(TimeSpan.FromMinutes(10));
        var expired = await service.ConfirmAsync(
            new ConfirmEmailChangeRequest(userId, "later@example.test", sender.Code!),
            cancellationToken);

        Assert.Equal(EmailChangeOutcome.Invalid, locked.Outcome);
        Assert.Equal(EmailChangeOutcome.Expired, expired.Outcome);
    }

    private sealed class CapturingEmailChangeCodeSender : IEmailChangeCodeSender
    {
        public string? Code { get; private set; }

        public Task SendAsync(
            string email,
            string code,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken)
        {
            Code = code;
            return Task.CompletedTask;
        }
    }

    private sealed class EmailChangeTestState(Guid userId, string email) : IEmailChangeState
    {
        public EmailChangeUser User { get; private set; } = new(userId, email, email.ToUpperInvariant());

        public EmailChangeChallenge? Challenge { get; private set; }

        public ValueTask<EmailChangeUser?> FindUserAsync(Guid id, CancellationToken cancellationToken) =>
            ValueTask.FromResult<EmailChangeUser?>(id == User.Id ? User : null);

        public ValueTask<bool> EmailExistsAsync(string normalizedEmail, Guid exceptUserId, CancellationToken cancellationToken) =>
            ValueTask.FromResult(false);

        public ValueTask<EmailChangeChallenge?> FindChallengeAsync(Guid id, string normalizedEmail, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Challenge is { } challenge
                && challenge.UserId == id
                && challenge.NormalizedEmail == normalizedEmail ? challenge : null);

        public ValueTask SaveChallengeAsync(EmailChangeChallenge challenge, CancellationToken cancellationToken)
        {
            Challenge = challenge;
            return ValueTask.CompletedTask;
        }

        public ValueTask SaveUserAndChallengeAsync(EmailChangeUser changedUser, EmailChangeChallenge challenge, CancellationToken cancellationToken)
        {
            User = changedUser;
            Challenge = challenge;
            return ValueTask.CompletedTask;
        }

        public ValueTask<int> CountRateEventsAsync(string partition, DateTimeOffset since, CancellationToken cancellationToken) =>
            ValueTask.FromResult(0);

        public ValueTask AddRateEventAsync(RateEvent rateEvent, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
    }

    private sealed class ManualTimeProvider(DateTimeOffset initial) : TimeProvider
    {
        private DateTimeOffset current = initial;

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan value) => current = current.Add(value);
    }
}
