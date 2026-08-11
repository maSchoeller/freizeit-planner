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
