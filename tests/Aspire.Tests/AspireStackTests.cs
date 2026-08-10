using Aspire.Hosting.Testing;
using Xunit;

namespace Aspire.Tests;

public sealed class AspireStackTests
{
    [Fact(Timeout = 180_000)]
    [Trait("Category", "Aspire")]
    public async Task RealStackBecomesReadyAndExposesMailpit()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.FreizeitCockpit_AppHost>(
            TestContext.Current.CancellationToken);
        await using var application = await builder.BuildAsync(TestContext.Current.CancellationToken);
        await application.StartAsync(TestContext.Current.CancellationToken);

        await application.ResourceNotifications.WaitForResourceHealthyAsync(
            "web",
            TestContext.Current.CancellationToken);

        using var web = application.CreateHttpClient("web");
        Assert.Equal("Healthy", await web.GetStringAsync("/health", TestContext.Current.CancellationToken));
        Assert.Equal("Healthy", await web.GetStringAsync("/ready", TestContext.Current.CancellationToken));

        using var mailpit = application.CreateHttpClient("mailpit", "mail-ui");
        using var response = await mailpit.GetAsync("/api/v1/info", TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, $"Mailpit returned {(int)response.StatusCode}.");
    }
}
