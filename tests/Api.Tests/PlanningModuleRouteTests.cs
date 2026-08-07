using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Api.Tests;

public sealed class PlanningModuleRouteTests
{
    [Theory]
    [InlineData("/api/v1/organizations/20000000-0000-0000-0000-000000000001/catering/ingredients?query=reis")]
    [InlineData("/api/v1/organizations/20000000-0000-0000-0000-000000000001/camps/30000000-0000-0000-0000-000000000001/devotions")]
    [InlineData("/api/v1/organizations/20000000-0000-0000-0000-000000000001/camps/30000000-0000-0000-0000-000000000001/notes")]
    public async Task PlanningRoutesRequireAuthentication(string path)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseEnvironment("Testing"));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
