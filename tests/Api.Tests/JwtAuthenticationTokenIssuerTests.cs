using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Identity.Implementation;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Api.Tests;

public sealed class JwtAuthenticationTokenIssuerTests
{
    [Fact]
    public void IssuedRefreshTokenRoundTripsRequiredIdentity()
    {
        using var rsa = RSA.Create(2048);
        using var signingMaterial = new JwtSigningMaterial(rsa);
        var issuer = new JwtAuthenticationTokenIssuer(
            signingMaterial,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Jwt:Issuer"] = "test-issuer",
                ["Authentication:Jwt:Audience"] = "test-audience"
            }).Build());
        var now = DateTimeOffset.UtcNow;
        var request = new AuthenticationTokenRequest(
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Guid.Parse("20000000-0000-0000-0000-000000000001"),
            "Miriam König",
            "security-stamp",
            now,
            now.AddDays(30));

        var pair = issuer.Issue(request);
        var identity = issuer.Read(pair.RefreshToken);
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var accessPrincipal = handler.ValidateToken(
            pair.AccessToken,
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "test-issuer",
                ValidateAudience = true,
                ValidAudience = "test-audience",
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingMaterial.Key,
                ValidateLifetime = true,
                ValidTypes = ["at+jwt"]
            },
            out _);

        Assert.NotNull(identity);
        Assert.Equal(request.UserId, identity.UserId);
        Assert.Equal(request.SessionId, identity.SessionId);
        Assert.Equal(request.SecurityStamp, identity.SecurityStamp);
        Assert.Equal(now.AddMinutes(15), pair.AccessExpiresAt, TimeSpan.FromSeconds(1));
        Assert.Equal(
            request.UserId.ToString("D"),
            accessPrincipal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Null(issuer.Read(pair.AccessToken));
    }

    [Fact]
    public void ManipulatedOrExpiredRefreshTokenIsRejected()
    {
        using var rsa = RSA.Create(2048);
        using var signingMaterial = new JwtSigningMaterial(rsa);
        var issuer = new JwtAuthenticationTokenIssuer(
            signingMaterial,
            new ConfigurationBuilder().Build());
        var now = DateTimeOffset.UtcNow;
        var request = new AuthenticationTokenRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test",
            "stamp",
            now.Subtract(TimeSpan.FromDays(40)),
            now.Subtract(TimeSpan.FromDays(10)));
        var expired = issuer.Issue(request).RefreshToken;
        var manipulated = expired[..^1] + (expired[^1] == 'A' ? "B" : "A");

        Assert.Null(issuer.Read(expired));
        Assert.Null(issuer.Read(manipulated));
        Assert.Null(issuer.Read("not-a-jwt"));
    }
}
