using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using Identity.Contracts;
using Identity.Implementation;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var configuredConnectionString = builder.Configuration.GetConnectionString("freizeit");

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi("v1");
builder.Services.AddHealthChecks();
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "freizeit_csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsProduction()
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
    options.HeaderName = "X-CSRF-TOKEN";
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "freizeit_session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = builder.Environment.IsProduction()
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = false;
        options.Events.OnValidatePrincipal = async context =>
        {
            var sessionValue = context.Principal?.FindFirstValue("session_id");
            var passwordlessLogin = context.HttpContext.RequestServices
                .GetRequiredService<IPasswordlessLogin>();
            if (!Guid.TryParse(sessionValue, out var sessionId)
                || !await passwordlessLogin.IsSessionActiveAsync(
                    sessionId,
                    context.HttpContext.RequestAborted))
            {
                context.RejectPrincipal();
            }
        };
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton(TimeProvider.System);
if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddSingleton<IPasswordlessState>(_ =>
        throw new InvalidOperationException("Tests that use authentication must supply IPasswordlessState."));
    builder.Services.AddSingleton<ILoginCodeSender>(_ =>
        throw new InvalidOperationException("Tests that request codes must supply ILoginCodeSender."));
    builder.Services.AddSingleton<IEmailChangeCodeSender>(_ =>
        throw new InvalidOperationException("Tests that send email-change codes must supply IEmailChangeCodeSender."));
    builder.Services.AddSingleton<IInvitationSender>(_ =>
        throw new InvalidOperationException("Tests that send invitations must supply IInvitationSender."));
    builder.Services.AddSingleton<IPasswordlessLogin>(services =>
        CreatePasswordlessLogin(services, builder.Configuration, builder.Environment));
    builder.Services.AddScoped<IInvitationLifecycle>(_ =>
        throw new InvalidOperationException("Tests that use invitation endpoints must supply IInvitationLifecycle."));
    builder.Services.AddScoped<IAccountLifecycle>(_ =>
        throw new InvalidOperationException("Tests that use account endpoints must supply IAccountLifecycle."));
    builder.Services.AddScoped<IEmailChangeLifecycle>(_ =>
        throw new InvalidOperationException("Tests that use email-change endpoints must supply IEmailChangeLifecycle."));
}
else
{
    var connectionString = configuredConnectionString
        ?? "Host=configuration-required.invalid;Database=freizeit;Username=invalid";
    builder.Services.AddDbContext<IdentityDbContext>(options =>
        options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure()));
    builder.Services.AddIdentityCore<ApplicationUser>()
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<IdentityDbContext>();
    builder.Services.AddScoped<IPasswordlessState, EfPasswordlessState>();
    builder.Services.AddScoped<IIdentityLifecycleState, EfIdentityLifecycleState>();
    builder.Services.AddScoped<IEmailChangeState, EfEmailChangeState>();
    builder.Services.AddSingleton<ILoginCodeSender, SmtpLoginCodeSender>();
    builder.Services.AddSingleton<IEmailChangeCodeSender, SmtpEmailChangeCodeSender>();
    builder.Services.AddSingleton<IInvitationSender, SmtpInvitationSender>();
    builder.Services.AddScoped<IPasswordlessLogin>(services =>
        CreatePasswordlessLogin(services, builder.Configuration, builder.Environment));
    builder.Services.AddScoped<IdentityLifecycleService>(services =>
        CreateIdentityLifecycle(services, builder.Configuration, builder.Environment));
    builder.Services.AddScoped<IInvitationLifecycle>(services =>
        services.GetRequiredService<IdentityLifecycleService>());
    builder.Services.AddScoped<IAccountLifecycle>(services =>
        services.GetRequiredService<IdentityLifecycleService>());
    builder.Services.AddScoped<IEmailChangeLifecycle>(services =>
        CreateEmailChangeLifecycle(services, builder.Configuration, builder.Environment));
}

var app = builder.Build();

app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers.ContentSecurityPolicy =
        "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; " +
        "script-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    await next();
});
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/v1", () => Results.Ok(new
{
    name = "Freizeit-Cockpit API",
    version = "v1",
    language = "de-DE"
}));
app.MapHealthChecks("/health");
app.MapHealthChecks("/ready");
app.MapOpenApi("/api/v1/openapi.json");
app.MapIdentityEndpoints();
app.MapLifecycleEndpoints();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

static PasswordlessLoginService CreatePasswordlessLogin(
    IServiceProvider services,
    IConfiguration configuration,
    IWebHostEnvironment environment)
{
    var configuredPepper = configuration["Authentication:LoginCodePepper"];
    if (environment.IsProduction() && string.IsNullOrWhiteSpace(configuredPepper))
    {
        throw new InvalidOperationException("Authentication:LoginCodePepper must be configured in production.");
    }

    var pepper = SHA256.HashData(Encoding.UTF8.GetBytes(
        configuredPepper ?? "development-only-login-code-pepper-do-not-use-in-production"));
    return new PasswordlessLoginService(
        services.GetRequiredService<IPasswordlessState>(),
        services.GetRequiredService<ILoginCodeSender>(),
        services.GetRequiredService<TimeProvider>(),
        pepper);
}

static IdentityLifecycleService CreateIdentityLifecycle(
    IServiceProvider services,
    IConfiguration configuration,
    IWebHostEnvironment environment)
{
    var configuredPepper = configuration["Authentication:InvitationTokenPepper"];
    if (environment.IsProduction() && string.IsNullOrWhiteSpace(configuredPepper))
    {
        throw new InvalidOperationException("Authentication:InvitationTokenPepper must be configured in production.");
    }

    var pepper = SHA256.HashData(Encoding.UTF8.GetBytes(
        configuredPepper ?? "development-only-invitation-token-pepper-do-not-use-in-production"));
    return new IdentityLifecycleService(
        services.GetRequiredService<IIdentityLifecycleState>(),
        services.GetRequiredService<TimeProvider>(),
        pepper);
}

static EmailChangeService CreateEmailChangeLifecycle(
    IServiceProvider services,
    IConfiguration configuration,
    IWebHostEnvironment environment)
{
    var configuredPepper = configuration["Authentication:LoginCodePepper"];
    if (environment.IsProduction() && string.IsNullOrWhiteSpace(configuredPepper))
    {
        throw new InvalidOperationException("Authentication:LoginCodePepper must be configured in production.");
    }
    var pepper = SHA256.HashData(Encoding.UTF8.GetBytes(
        configuredPepper ?? "development-only-login-code-pepper-do-not-use-in-production"));
    return new EmailChangeService(
        services.GetRequiredService<IEmailChangeState>(),
        services.GetRequiredService<IEmailChangeCodeSender>(),
        services.GetRequiredService<TimeProvider>(),
        pepper);
}

app.Run();

public partial class Program;
