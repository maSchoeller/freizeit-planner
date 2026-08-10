using System.Net;
using System.Net.Mail;
using Identity.Contracts;

internal sealed class SmtpLoginCodeSender(IConfiguration configuration) : ILoginCodeSender
{
    public async Task SendAsync(
        string email,
        string code,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        var (host, port) = SmtpEndpoint.Resolve(configuration);
        var from = configuration["Smtp:From"] ?? "anmeldung@freizeit-cockpit.local";
        using var message = new MailMessage(from, email)
        {
            Subject = "Dein Anmeldecode für das Freizeit-Cockpit",
            Body = $"Dein Anmeldecode lautet: {code}\n\nEr ist bis {expiresAt:HH:mm} Uhr gültig.",
            IsBodyHtml = false
        };
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = configuration.GetValue("Smtp:UseTls", false),
            DeliveryMethod = SmtpDeliveryMethod.Network
        };
        var userName = configuration["Smtp:UserName"];
        if (!string.IsNullOrEmpty(userName))
        {
            client.Credentials = new NetworkCredential(userName, configuration["Smtp:Password"]);
        }

        await client.SendMailAsync(message, cancellationToken);
    }
}

internal sealed class SmtpEmailChangeCodeSender(IConfiguration configuration) : IEmailChangeCodeSender
{
    public async Task SendAsync(
        string email,
        string code,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        var (host, port) = SmtpEndpoint.Resolve(configuration);
        var from = configuration["Smtp:From"] ?? "anmeldung@freizeit-cockpit.local";
        using var message = new MailMessage(from, email)
        {
            Subject = "Bestätige deine neue E-Mail-Adresse",
            Body = $"Dein Einmalcode lautet: {code}\n\nEr ist bis {expiresAt:HH:mm} Uhr gültig.",
            IsBodyHtml = false
        };
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = configuration.GetValue("Smtp:UseTls", false),
            DeliveryMethod = SmtpDeliveryMethod.Network
        };
        var userName = configuration["Smtp:UserName"];
        if (!string.IsNullOrEmpty(userName))
        {
            client.Credentials = new NetworkCredential(userName, configuration["Smtp:Password"]);
        }
        await client.SendMailAsync(message, cancellationToken);
    }
}

internal sealed class SmtpPasswordResetSender(IConfiguration configuration) : IPasswordResetSender
{
    public async Task SendAsync(
        string email,
        string token,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        var (host, port) = SmtpEndpoint.Resolve(configuration);
        var from = configuration["Smtp:From"] ?? "anmeldung@freizeit-cockpit.local";
        var publicBaseUrl = configuration["Web:PublicBaseUrl"]?.TrimEnd('/')
            ?? "http://localhost:5041";
        var link = $"{publicBaseUrl}/passwort-zuruecksetzen?token={Uri.EscapeDataString(token)}";
        using var message = new MailMessage(from, email)
        {
            Subject = "Setze dein Passwort im Freizeit-Cockpit zurück",
            Body = $"Öffne diesen Link, um dein Passwort zurückzusetzen:\n\n{link}\n\nDer Link ist bis {expiresAt:HH:mm} Uhr gültig.",
            IsBodyHtml = false
        };
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = configuration.GetValue("Smtp:UseTls", false),
            DeliveryMethod = SmtpDeliveryMethod.Network
        };
        var userName = configuration["Smtp:UserName"];
        if (!string.IsNullOrEmpty(userName))
        {
            client.Credentials = new NetworkCredential(userName, configuration["Smtp:Password"]);
        }
        await client.SendMailAsync(message, cancellationToken);
    }
}

internal sealed class SmtpInvitationConfirmationSender(IConfiguration configuration)
    : IInvitationConfirmationSender
{
    public async Task SendAsync(
        string email,
        string token,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        var (host, port) = SmtpEndpoint.Resolve(configuration);
        var from = configuration["Smtp:From"] ?? "anmeldung@freizeit-cockpit.local";
        var publicBaseUrl = configuration["Web:PublicBaseUrl"]?.TrimEnd('/')
            ?? "http://localhost:5041";
        var link = $"{publicBaseUrl}/einladung-bestaetigen?token={Uri.EscapeDataString(token)}";
        using var message = new MailMessage(from, email)
        {
            Subject = "Bestätige deine Registrierung im Freizeit-Cockpit",
            Body = $"Öffne diesen Link, um deine E-Mail-Adresse zu bestätigen:\n\n{link}\n\nDer Link ist bis {expiresAt:HH:mm} Uhr gültig.",
            IsBodyHtml = false
        };
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = configuration.GetValue("Smtp:UseTls", false),
            DeliveryMethod = SmtpDeliveryMethod.Network
        };
        var userName = configuration["Smtp:UserName"];
        if (!string.IsNullOrEmpty(userName))
        {
            client.Credentials = new NetworkCredential(userName, configuration["Smtp:Password"]);
        }
        await client.SendMailAsync(message, cancellationToken);
    }
}
