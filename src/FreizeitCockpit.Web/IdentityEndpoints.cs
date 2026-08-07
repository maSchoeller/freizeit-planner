using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Identity.Contracts;

internal static class IdentityEndpoints
{
    private const string GenericCodeMessage =
        "Wenn die Adresse registriert ist, wurde ein Anmeldecode versendet.";

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth");
        group.MapPost("/code", RequestCodeAsync)
            .Produces<CodeRequestResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .AllowAnonymous();
        group.MapPost("/verify", VerifyCodeAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .AllowAnonymous();
        group.MapGet("/antiforgery", GetAntiforgeryToken)
            .Produces<AntiforgeryResponse>()
            .AllowAnonymous();
        group.MapGet("/sessions", ListSessionsAsync)
            .Produces<IReadOnlyList<SessionView>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();
        group.MapDelete("/sessions/{sessionId:guid}", RevokeSessionAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireAuthorization();
        group.MapPost("/sessions/revoke-others", RevokeOtherSessionsAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireAuthorization();
        group.MapPost("/logout", LogoutAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> RequestCodeAsync(
        CodeRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        IPasswordlessLogin passwordlessLogin,
        CancellationToken cancellationToken)
    {
        if (await ValidateAntiforgeryAsync(context, antiforgery) is { } problem)
        {
            return problem;
        }
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        await passwordlessLogin.RequestCodeAsync(
            new LoginCodeRequest(request.Email ?? string.Empty, ipAddress),
            cancellationToken);
        return Results.Accepted(value: new CodeRequestResponse(GenericCodeMessage));
    }

    private static async Task<IResult> VerifyCodeAsync(
        CodeVerification request,
        HttpContext context,
        IAntiforgery antiforgery,
        IPasswordlessLogin passwordlessLogin,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (await ValidateAntiforgeryAsync(context, antiforgery) is { } problem)
        {
            return problem;
        }
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await passwordlessLogin.VerifyCodeAsync(
            new LoginCodeVerification(
                request.Email ?? string.Empty,
                request.Code ?? string.Empty,
                ipAddress,
                request.RememberMe),
            cancellationToken);
        if (result.Session is null)
        {
            return LoginProblem(result.Outcome);
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, result.Session.UserId.ToString()),
            new Claim(ClaimTypes.Name, result.Session.DisplayName),
            new Claim("session_id", result.Session.Id.ToString()),
            new Claim("auth_time", timeProvider.GetUtcNow().ToUnixTimeSeconds().ToString(
                System.Globalization.CultureInfo.InvariantCulture))
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                AllowRefresh = false,
                ExpiresUtc = result.Session.ExpiresAt,
                IsPersistent = request.RememberMe
            });
        return Results.NoContent();
    }

    private static IResult GetAntiforgeryToken(HttpContext context, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        return Results.Ok(new AntiforgeryResponse(tokens.RequestToken!));
    }

    private static async Task<IResult> ListSessionsAsync(
        ClaimsPrincipal principal,
        IPasswordlessLogin passwordlessLogin,
        CancellationToken cancellationToken)
    {
        if (!TryGetSession(principal, out var userId, out var sessionId))
        {
            return Results.Unauthorized();
        }

        var sessions = await passwordlessLogin.ListSessionsAsync(userId, sessionId, cancellationToken);
        return Results.Ok(sessions);
    }

    private static async Task<IResult> RevokeSessionAsync(
        Guid sessionId,
        HttpContext context,
        IAntiforgery antiforgery,
        IPasswordlessLogin passwordlessLogin,
        CancellationToken cancellationToken)
    {
        if (await ValidateAntiforgeryAsync(context, antiforgery) is { } problem)
        {
            return problem;
        }
        if (!TryGetSession(context.User, out var userId, out var currentSessionId))
        {
            return Results.Unauthorized();
        }

        await passwordlessLogin.RevokeSessionAsync(userId, sessionId, cancellationToken);
        if (sessionId == currentSessionId)
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> RevokeOtherSessionsAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        IPasswordlessLogin passwordlessLogin,
        CancellationToken cancellationToken)
    {
        if (await ValidateAntiforgeryAsync(context, antiforgery) is { } problem)
        {
            return problem;
        }
        if (!TryGetSession(context.User, out var userId, out var currentSessionId))
        {
            return Results.Unauthorized();
        }

        await passwordlessLogin.RevokeOtherSessionsAsync(userId, currentSessionId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        IPasswordlessLogin passwordlessLogin,
        CancellationToken cancellationToken)
    {
        if (await ValidateAntiforgeryAsync(context, antiforgery) is { } problem)
        {
            return problem;
        }
        if (TryGetSession(context.User, out var userId, out var sessionId))
        {
            await passwordlessLogin.RevokeSessionAsync(userId, sessionId, cancellationToken);
        }

        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.NoContent();
    }

    internal static bool TryGetSession(ClaimsPrincipal principal, out Guid userId, out Guid sessionId)
    {
        userId = Guid.Empty;
        sessionId = Guid.Empty;
        return Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId)
            && Guid.TryParse(principal.FindFirstValue("session_id"), out sessionId);
    }

    internal static async ValueTask<IResult?> ValidateAntiforgeryAsync(
        HttpContext context,
        IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return null;
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Sicherheitsprüfung fehlgeschlagen",
                detail: "Die Anfrage konnte nicht sicher bestätigt werden. Bitte lade die Seite neu.",
                extensions: new Dictionary<string, object?>
                {
                    ["errorCode"] = "antiforgery_invalid"
                });
        }
    }

    private static IResult LoginProblem(LoginOutcome outcome)
    {
        var (status, code, detail) = outcome switch
        {
            LoginOutcome.Expired => (StatusCodes.Status400BadRequest, "login_code_expired", "Der Anmeldecode ist abgelaufen."),
            LoginOutcome.AttemptsExceeded => (StatusCodes.Status400BadRequest, "login_code_locked", "Der Anmeldecode ist nach zu vielen Fehlversuchen ungültig."),
            LoginOutcome.RateLimited => (StatusCodes.Status429TooManyRequests, "login_rate_limited", "Zu viele Versuche. Bitte warte einige Minuten."),
            _ => (StatusCodes.Status400BadRequest, "login_code_invalid", "Der Anmeldecode ist ungültig.")
        };
        return Results.Problem(
            statusCode: status,
            title: "Anmeldung nicht möglich",
            detail: detail,
            extensions: new Dictionary<string, object?> { ["errorCode"] = code });
    }

    private sealed record CodeRequest(string? Email);

    private sealed record CodeVerification(string? Email, string? Code, bool RememberMe);

    private sealed record CodeRequestResponse(string Message);

    private sealed record AntiforgeryResponse(string Token);
}
