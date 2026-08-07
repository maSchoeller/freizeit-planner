using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Identity.Contracts;

namespace Identity.Implementation;

public sealed class EmailChangeService(
    IEmailChangeState state,
    IEmailChangeCodeSender sender,
    TimeProvider timeProvider,
    byte[] codePepper) : IEmailChangeLifecycle
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(15);
    private const int MaxFailedAttempts = 5;
    private const int MaxRequestsPerWindow = 5;
    private readonly byte[] codePepper = codePepper.Length >= 32
        ? codePepper.ToArray()
        : throw new ArgumentException("The email-change pepper must contain at least 32 bytes.", nameof(codePepper));

    public async Task RequestAsync(EmailChangeRequest request, CancellationToken cancellationToken)
    {
        var user = await state.FindUserAsync(request.UserId, cancellationToken)
            ?? throw Rule("user_not_found", "Das Konto wurde nicht gefunden.");
        var (email, normalizedEmail) = NormalizeEmail(request.Email);
        if (normalizedEmail == user.NormalizedEmail)
        {
            throw Rule("email_unchanged", "Die neue E-Mail-Adresse entspricht der bisherigen Adresse.");
        }
        if (await state.EmailExistsAsync(normalizedEmail, user.Id, cancellationToken))
        {
            throw Rule("email_conflict", "Diese E-Mail-Adresse kann nicht übernommen werden.");
        }

        var now = timeProvider.GetUtcNow();
        await EnforceRateAsync(normalizedEmail, request.IpAddress, now, cancellationToken);
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var challenge = new EmailChangeChallenge(
            user.Id,
            email,
            normalizedEmail,
            HashCode(user.Id, normalizedEmail, code),
            now.Add(CodeLifetime));
        await state.SaveChallengeAsync(challenge, cancellationToken);
        await sender.SendAsync(email, code, challenge.ExpiresAt, cancellationToken);
    }

    public async Task<EmailChangeResult> ConfirmAsync(
        ConfirmEmailChangeRequest request,
        CancellationToken cancellationToken)
    {
        var user = await state.FindUserAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return new EmailChangeResult(EmailChangeOutcome.Invalid, null);
        }
        var (_, normalizedEmail) = NormalizeEmail(request.Email);
        var challenge = await state.FindChallengeAsync(user.Id, normalizedEmail, cancellationToken);
        if (challenge is null || challenge.UsedAt is not null || challenge.FailedAttempts >= MaxFailedAttempts)
        {
            return new EmailChangeResult(EmailChangeOutcome.Invalid, null);
        }

        var now = timeProvider.GetUtcNow();
        if (challenge.ExpiresAt <= now)
        {
            return new EmailChangeResult(EmailChangeOutcome.Expired, null);
        }
        var expected = Convert.FromHexString(challenge.CodeHash);
        var actual = Convert.FromHexString(HashCode(user.Id, normalizedEmail, request.Code));
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            challenge.RecordFailure();
            await state.SaveChallengeAsync(challenge, cancellationToken);
            return new EmailChangeResult(EmailChangeOutcome.Invalid, null);
        }
        if (await state.EmailExistsAsync(normalizedEmail, user.Id, cancellationToken))
        {
            throw Rule("email_conflict", "Diese E-Mail-Adresse kann nicht übernommen werden.");
        }

        challenge.MarkUsed(now);
        user.ChangeEmail(challenge.Email, challenge.NormalizedEmail);
        await state.SaveUserAndChallengeAsync(user, challenge, cancellationToken);
        return new EmailChangeResult(EmailChangeOutcome.Changed, user.Email);
    }

    private async Task EnforceRateAsync(
        string normalizedEmail,
        string ipAddress,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var partitions = new[]
        {
            "email-change:email:" + HashPartition(normalizedEmail),
            "email-change:ip:" + HashPartition(ipAddress.Trim())
        };
        foreach (var partition in partitions)
        {
            if (await state.CountRateEventsAsync(partition, now.Subtract(RateWindow), cancellationToken)
                >= MaxRequestsPerWindow)
            {
                throw Rule("email_change_rate_limited", "Zu viele Versuche. Bitte warte einige Minuten.");
            }
        }
        foreach (var partition in partitions)
        {
            await state.AddRateEventAsync(new RateEvent(partition, now), cancellationToken);
        }
    }

    private string HashCode(Guid userId, string normalizedEmail, string code) =>
        Convert.ToHexString(HMACSHA256.HashData(
            codePepper,
            Encoding.UTF8.GetBytes($"{userId:N}|{normalizedEmail}|{code}")));

    private string HashPartition(string value) => Convert.ToHexString(
        HMACSHA256.HashData(codePepper, Encoding.UTF8.GetBytes(value)));

    private static (string Email, string NormalizedEmail) NormalizeEmail(string email)
    {
        var value = email.Trim();
        if (value.Length is 0 or > 320
            || !value.Contains('@', StringComparison.Ordinal)
            || value.Any(char.IsWhiteSpace))
        {
            throw Rule("email_invalid", "Die E-Mail-Adresse ist ungültig.");
        }
        return (value.ToLowerInvariant(), value.ToUpperInvariant());
    }

    private static IdentityRuleException Rule(string code, string message) => new(code, message);
}
