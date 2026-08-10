using Identity.Implementation;
using Xunit;

namespace Identity.Tests;

public sealed class RuntimeRoleConnectionInterceptorTests
{
    [Theory]
    [InlineData("")]
    [InlineData("9runtime")]
    [InlineData("runtime-role")]
    [InlineData("runtime role")]
    [InlineData("rüntime")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void InvalidPostgresRoleIdentifiersAreRejected(string roleName)
        => Assert.Throws<ArgumentException>(() => new RuntimeRoleConnectionInterceptor(roleName));

    [Theory]
    [InlineData("freizeit_app")]
    [InlineData("_runtime2")]
    [InlineData("RuntimeRole")]
    public void SafePostgresRoleIdentifiersAreAccepted(string roleName)
        => Assert.NotNull(new RuntimeRoleConnectionInterceptor(roleName));
}
