using System.Net;
using System.Net.Mail;
using Identity.Contracts;

internal sealed class SmtpInvitationSender(IConfiguration configuration) : IInvitationSender
{
    public async Task SendAsync(IssuedInvitation invitation, CancellationToken cancellationToken)
    {
        var (host, port) = SmtpEndpoint.Resolve(configuration);
        var from = configuration["Smtp:From"] ?? "einladung@freizeit-cockpit.local";
        var publicBaseUrl = configuration["PublicBaseUrl"]?.TrimEnd('/') ?? "http://localhost:5041";
        var link = $"{publicBaseUrl}/einladung?token={Uri.EscapeDataString(invitation.Token)}";
        using var message = new MailMessage(from, invitation.Email.ToLowerInvariant())
        {
            Subject = "Einladung zum Freizeit-Cockpit",
            Body = $"Du wurdest zum Freizeit-Cockpit eingeladen.\n\nEinladung annehmen: {link}\n\n" +
                $"Die Einladung ist bis {invitation.ExpiresAt:dd.MM.yyyy HH:mm} Uhr gültig.",
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

internal static class SmtpEndpoint
{
    public static (string Host, int Port) Resolve(IConfiguration configuration)
    {
        var discoveredEndpoint = configuration
            .GetSection("services:mailpit:smtp")
            .GetChildren()
            .Select(item => item.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (Uri.TryCreate(discoveredEndpoint, UriKind.Absolute, out var endpoint))
        {
            return (endpoint.Host, endpoint.Port);
        }

        return (
            configuration["Smtp:Host"] ?? "localhost",
            configuration.GetValue("Smtp:Port", 1025));
    }
}
