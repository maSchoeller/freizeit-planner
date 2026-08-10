using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Identity.Implementation;
using Microsoft.IdentityModel.Tokens;

internal sealed class JwtSigningMaterial : IDisposable
{
    public JwtSigningMaterial(RSA rsa)
    {
        Rsa = rsa;
        var publicKey = rsa.ExportSubjectPublicKeyInfo();
        Key = new RsaSecurityKey(rsa)
        {
            KeyId = Convert.ToHexString(SHA256.HashData(publicKey))[..16]
        };
    }

    public RSA Rsa { get; }

    public RsaSecurityKey Key { get; }

    public void Dispose() => Rsa.Dispose();
}

internal sealed class JwtAuthenticationTokenIssuer(
    JwtSigningMaterial signingMaterial,
    IConfiguration configuration) : IAuthenticationTokenIssuer, IRefreshTokenReader
{
    private static readonly TimeSpan AccessLifetime = TimeSpan.FromMinutes(15);
    private readonly string issuer = configuration["Authentication:Jwt:Issuer"] ?? "freizeit-cockpit";
    private readonly string audience = configuration["Authentication:Jwt:Audience"] ?? "freizeit-cockpit-api";

    public AuthenticationTokenPair Issue(AuthenticationTokenRequest request)
    {
        var accessExpiresAt = request.IssuedAt.Add(AccessLifetime);
        var credentials = new SigningCredentials(signingMaterial.Key, SecurityAlgorithms.RsaSha256);
        var access = CreateToken(
            request,
            accessExpiresAt,
            "at+jwt",
            credentials,
            [new Claim(ClaimTypes.Name, request.DisplayName)]);
        var refresh = CreateToken(
            request,
            request.RefreshExpiresAt,
            "rt+jwt",
            credentials,
            []);
        return new AuthenticationTokenPair(access, accessExpiresAt, refresh);
    }

    public RefreshTokenIdentity? Read(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var principal = handler.ValidateToken(
                token,
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingMaterial.Key,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidTypes = ["rt+jwt"]
                },
                out var validatedToken);
            if (validatedToken is not JwtSecurityToken jwt
                || string.IsNullOrWhiteSpace(jwt.Id)
                || jwt.IssuedAt == DateTime.MinValue
                || !Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId)
                || !Guid.TryParse(principal.FindFirstValue("sid"), out var sessionId)
                || principal.FindFirstValue("sst") is not { Length: > 0 } securityStamp)
            {
                return null;
            }
            return new RefreshTokenIdentity(userId, sessionId, securityStamp);
        }
        catch (Exception exception) when (exception is SecurityTokenException or ArgumentException)
        {
            return null;
        }
    }

    private string CreateToken(
        AuthenticationTokenRequest request,
        DateTimeOffset expiresAt,
        string tokenType,
        SigningCredentials credentials,
        IReadOnlyCollection<Claim> additionalClaims)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, request.UserId.ToString("D")),
            new(ClaimTypes.NameIdentifier, request.UserId.ToString("D")),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new("sid", request.SessionId.ToString("D")),
            new("session_id", request.SessionId.ToString("D")),
            new("sst", request.SecurityStamp),
            new("auth_time", request.IssuedAt.ToUnixTimeSeconds().ToString(
                System.Globalization.CultureInfo.InvariantCulture))
        };
        claims.AddRange(additionalClaims);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = request.IssuedAt.UtcDateTime,
            NotBefore = request.IssuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = credentials,
            TokenType = tokenType
        };
        var handler = new JwtSecurityTokenHandler();
        handler.OutboundClaimTypeMap.Clear();
        return handler.CreateEncodedJwt(descriptor);
    }
}

internal static class JwtSigningMaterialFactory
{
    public static JwtSigningMaterial Create(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        bool allowDevelopmentFallback = false)
    {
        var configuredPem = configuration["Authentication:Jwt:PrivateKeyPem"];
        if (!string.IsNullOrWhiteSpace(configuredPem))
        {
            return Import(configuredPem);
        }
        if (environment.IsProduction() && !allowDevelopmentFallback)
        {
            throw new InvalidOperationException(
                "Authentication:Jwt:PrivateKeyPem must be configured in production.");
        }

        var repositoryRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", ".."));
        var keyPath = Path.Combine(repositoryRoot, ".artifacts", "identity", "jwt-signing-key.pem");
        if (File.Exists(keyPath))
        {
            return Import(File.ReadAllText(keyPath));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(keyPath)!);
        using var generated = RSA.Create(3072);
        var pem = generated.ExportPkcs8PrivateKeyPem();
        File.WriteAllText(keyPath, pem);
        return Import(pem);
    }

    private static JwtSigningMaterial Import(string pem)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        return new JwtSigningMaterial(rsa);
    }
}
