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
    [InlineData("/api/v1/organizations/20000000-0000-0000-0000-000000000001/camps/30000000-0000-0000-0000-000000000001/logistics/material")]
    [InlineData("/api/v1/organizations/20000000-0000-0000-0000-000000000001/camps/30000000-0000-0000-0000-000000000001/files?ownerType=Note&ownerId=40000000-0000-0000-0000-000000000001")]
    [InlineData("/api/v1/organizations/20000000-0000-0000-0000-000000000001/camps/30000000-0000-0000-0000-000000000001/activity")]
    [InlineData("/api/v1/organizations/20000000-0000-0000-0000-000000000001/camps/30000000-0000-0000-0000-000000000001/search?query=wald")]
    [InlineData("/api/v1/organizations/20000000-0000-0000-0000-000000000001/camps/30000000-0000-0000-0000-000000000001/trash")]
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
