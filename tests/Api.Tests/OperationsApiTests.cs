using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Api.Tests;

public sealed class OperationsApiTests
{
    [Theory]
    [InlineData("AZURE_CLIENT_ID")]
    [InlineData("Storage:BlobServiceUri")]
    [InlineData("DataProtection:BlobContainer")]
    [InlineData("DataProtection:KeyIdentifier")]
    public void ProductionRequiresExternalDataProtectionConfiguration(string missingKey)
    {
        var settings = new Dictionary<string, string>
        {
            ["AZURE_CLIENT_ID"] = "00000000-0000-0000-0000-000000000001",
            ["Storage:BlobServiceUri"] = "https://storage.example.test/",
            ["DataProtection:BlobContainer"] = "data-protection",
            ["DataProtection:KeyIdentifier"] = "https://vault.example.test/keys/data-protection"
        };
        _ = settings.Remove(missingKey);

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            foreach (var setting in settings)
            {
                builder.UseSetting(setting.Key, setting.Value);
            }
        });

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains($"{missingKey} must be configured in production.", exception.ToString());
    }

    [Fact]
    public async Task LivenessStaysHealthyWhenReadinessDependencyFails()
    {
        using var factory = CreateFactory(services =>
            services.AddHealthChecks().AddCheck(
                "unavailable-dependency",
                () => HealthCheckResult.Unhealthy(),
                tags: ["ready"]));
        using var client = factory.CreateClient();

        using var liveness = await client.GetAsync(
            "/health",
            TestContext.Current.CancellationToken);
        using var readiness = await client.GetAsync(
            "/ready",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, liveness.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, readiness.StatusCode);
    }

    [Fact]
    public async Task ApiReturnsAValidatedCorrelationId()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        const string correlationId = "0123456789abcdef0123456789abcdef";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1");
        request.Headers.Add("X-Correlation-ID", correlationId);

        using var response = await client.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(correlationId, response.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Fact]
    public async Task ApiDoesNotEchoAnUnsafeCorrelationId()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1");
        _ = request.Headers.TryAddWithoutValidation(
            "X-Correlation-ID",
            "not-a-w3c-trace-id");

        using var response = await client.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var returned = response.Headers.GetValues("X-Correlation-ID").Single();
        Assert.Matches("^[0-9a-f]{32}$", returned);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        Action<IServiceCollection>? configureServices = null)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            if (configureServices is not null)
            {
                builder.ConfigureTestServices(configureServices);
            }
        });
    }
}
