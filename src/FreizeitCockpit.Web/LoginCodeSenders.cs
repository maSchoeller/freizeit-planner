using System.Net;
using System.Net.Mail;
using Identity.Contracts;

internal sealed class TestingLoginCodeSender : ILoginCodeSender
{
    public Task SendAsync(
        string email,
        string code,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class SmtpLoginCodeSender(IConfiguration configuration) : ILoginCodeSender
{
    public async Task SendAsync(
        string email,
        string code,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        var (host, port) = ResolveEndpoint(configuration);
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

    private static (string Host, int Port) ResolveEndpoint(IConfiguration configuration)
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
