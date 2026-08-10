using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Identity.Contracts;

internal static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth");
        group.MapPost("/login", LoginAsync)
            .Produces<AccessTokenResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status423Locked)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .AllowAnonymous();
        group.MapGet("/first-login", FirstLoginAvailabilityAsync)
            .Produces<FirstLoginAvailability>()
            .AllowAnonymous();
        group.MapPost("/first-login", FirstLoginAsync)
            .Produces<AccessTokenResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .AllowAnonymous();
        group.MapPost("/refresh", RefreshAsync)
            .Produces<AccessTokenResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .AllowAnonymous();
        group.MapPost("/password-reset/request", RequestPasswordResetAsync)
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .AllowAnonymous();
        group.MapPost("/password-reset/confirm", ConfirmPasswordResetAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .AllowAnonymous();
        group.MapPost("/reauthenticate", ReauthenticateAsync)
            .Produces<ReauthenticationResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status423Locked)
            .RequireAuthorization();
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
        endpoints.MapPost("/api/v1/account/password", ChangePasswordAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .ProducesProblem(StatusCodes.Status423Locked)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> RequestPasswordResetAsync(
        PasswordResetRequestBody request,
        HttpContext context,
        IAntiforgery antiforgery,
        IPasswordMaintenance maintenance,
        CancellationToken cancellationToken)
    {
        if (await ValidateAntiforgeryAsync(context, antiforgery) is { } problem)
        {
            return problem;
        }
        await maintenance.RequestResetAsync(request.Email ?? string.Empty, cancellationToken);
        return Results.Accepted();
    }

    private static async Task<IResult> ConfirmPasswordResetAsync(
        PasswordResetConfirmBody request,
        HttpContext context,
        IAntiforgery antiforgery,
        IPasswordMaintenance maintenance,
        CancellationToken cancellationToken)
    {
        if (await ValidateAntiforgeryAsync(context, antiforgery) is { } problem)
        {
            return problem;
        }
        var outcome = await maintenance.ConfirmResetAsync(
            new PasswordResetConfirmation(
                request.Token ?? string.Empty,
                request.NewPassword ?? string.Empty),
            cancellationToken);
        return outcome == PasswordResetOutcome.Succeeded
            ? Results.NoContent()
            : Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Passwort konnte nicht gesetzt werden",
                detail: outcome == PasswordResetOutcome.InvalidPassword
                    ? "Das Passwort muss zwischen 15 und 128 Zeichen lang sein."
                    : "Der Link ist ungültig, abgelaufen oder wurde bereits verwendet.",
                extensions: new Dictionary<string, object?>
                {
                    ["errorCode"] = outcome == PasswordResetOutcome.InvalidPassword
                        ? "password_invalid"
                        : "password_reset_invalid"
                });
    }

    private static async Task<IResult> ChangePasswordAsync(
        PasswordChangeBody request,
        HttpContext context,
        IAntiforgery antiforgery,
        IPasswordMaintenance maintenance,
        CancellationToken cancellationToken)
    {
        if (await ValidateAntiforgeryAsync(context, antiforgery) is { } problem)
        {
            return problem;
        }
        if (!TryGetSession(context.User, out var userId, out var sessionId))
        {
            return Results.Unauthorized();
        }
        if (!PlanningEndpointSupport.TryReadVersion(context.Request, out var expectedVersion))
        {
            return PlanningEndpointSupport.PreconditionRequired();
        }
        var outcome = await maintenance.ChangePasswordAsync(
            new PasswordChangeRequest(
                userId,
                sessionId,
                expectedVersion,
                request.CurrentPassword ?? string.Empty,
                request.NewPassword ?? string.Empty),
            cancellationToken);
        if (outcome == PasswordChangeOutcome.Succeeded)
        {
            ClearRefreshCookie(context);
            return Results.NoContent();
        }
        return PasswordMaintenanceProblem(outcome);
    }

    private static async Task<IResult> ReauthenticateAsync(
        ReauthenticationBody request,
        HttpContext context,
        IAntiforgery antiforgery,
        IPasswordMaintenance maintenance,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (await ValidateAntiforgeryAsync(context, antiforgery) is { } problem)
        {
            return problem;
        }
        if (!TryGetSession(context.User, out var userId, out var sessionId))
        {
            return Results.Unauthorized();
        }
        var outcome = await maintenance.ReauthenticateAsync(
            new ReauthenticationRequest(userId, sessionId, request.Password ?? string.Empty),
            cancellationToken);
        return outcome switch
        {
            ReauthenticationOutcome.Succeeded => Results.Ok(
                new ReauthenticationResponse(timeProvider.GetUtcNow().AddMinutes(10))),
            ReauthenticationOutcome.Suspended => Results.Problem(
                statusCode: StatusCodes.Status423Locked,
                title: "Konto gesperrt",
                detail: "Dein Konto wurde gesperrt.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "account_suspended" }),
            _ => Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Bestätigung fehlgeschlagen",
                detail: "Das Passwort ist nicht korrekt.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "invalid_credentials" })
        };
    }

    private static async Task<IResult> LoginAsync(
        PasswordLoginBody request,
        HttpContext context,
        IAntiforgery antiforgery,
        IPasswordAuthentication authentication,
        CancellationToken cancellationToken)
    {
        if (await ValidateAntiforgeryAsync(context, antiforgery) is { } problem)
        {
            return problem;
        }

        var result = await authentication.LoginAsync(
            new PasswordLoginRequest(
                request.Email ?? string.Empty,
                request.Password ?? string.Empty,
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                request.RememberMe),
            cancellationToken);
        if (result.Authentication is not { } issued)
        {
            return PasswordLoginProblem(result.Outcome);
        }

        SetRefreshCookie(context, issued);
        return Results.Ok(issued.Access);
    }

    private static async Task<IResult> FirstLoginAvailabilityAsync(
        IInitialSuperAdminRegistration registration,
        CancellationToken cancellationToken) =>
        Results.Ok(new FirstLoginAvailability(
            await registration.IsAvailableAsync(cancellationToken)));

    private static async Task<IResult> FirstLoginAsync(
        FirstLoginBody request,
        HttpContext context,
        IAntiforgery antiforgery,
        IInitialSuperAdminRegistration registration,
        CancellationToken cancellationToken)
    {
        if (await ValidateAntiforgeryAsync(context, antiforgery) is { } problem)
        {
            return problem;
        }
        var result = await registration.RegisterAsync(
            new InitialSuperAdminRequest(
                request.Email ?? string.Empty,
                request.Password ?? string.Empty,
                request.FirstName ?? string.Empty,
                request.LastName ?? string.Empty,
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
            cancellationToken);
        if (result.Authentication is { } issued)
        {
            SetRefreshCookie(context, issued);
            return Results.Ok(issued.Access);
        }
        return result.Outcome == InitialSuperAdminOutcome.AlreadyInitialized
            ? Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Ersteinrichtung abgeschlossen",
                detail: "Der erste Superadmin wurde bereits angelegt.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "first_login_unavailable" })
            : Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Angaben nicht gültig",
                detail: "Prüfe E-Mail-Adresse, Vorname, Nachname und Passwort.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "first_login_invalid" });
    }

    private static void SetRefreshCookie(HttpContext context, IssuedAuthentication issued) =>
        context.Response.Cookies.Append(
            "freizeit_refresh",
            issued.RefreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/api/v1/auth",
                IsEssential = true,
                Expires = issued.RememberMe ? issued.RefreshExpiresAt : null
            });

    private static async Task<IResult> RefreshAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        IAuthenticationSessionManagement sessions,
        CancellationToken cancellationToken)
    {
        if (await ValidateAntiforgeryAsync(context, antiforgery) is { } problem)
        {
            return problem;
        }
        if (!context.Request.Cookies.TryGetValue("freizeit_refresh", out var refreshToken)
            || string.IsNullOrWhiteSpace(refreshToken))
        {
            ClearRefreshCookie(context);
            return Results.Unauthorized();
        }
        var result = await sessions.RefreshAsync(
            new RefreshAuthenticationRequest(
                refreshToken,
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
            cancellationToken);
        if (result.Authentication is not { } issued)
        {
            ClearRefreshCookie(context);
            return Results.Unauthorized();
        }
        SetRefreshCookie(context, issued);
        return Results.Ok(issued.Access);
    }

    private static void ClearRefreshCookie(HttpContext context) =>
        context.Response.Cookies.Delete(
            "freizeit_refresh",
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/api/v1/auth",
                IsEssential = true
            });

    private static IResult GetAntiforgeryToken(HttpContext context, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        return Results.Ok(new AntiforgeryResponse(tokens.RequestToken!));
    }

    private static async Task<IResult> ListSessionsAsync(
        ClaimsPrincipal principal,
        IAuthenticationSessionManagement sessions,
        CancellationToken cancellationToken)
    {
        if (!TryGetSession(principal, out var userId, out var sessionId))
        {
            return Results.Unauthorized();
        }

        var views = await sessions.ListSessionsAsync(userId, sessionId, cancellationToken);
        return Results.Ok(views);
    }

    private static async Task<IResult> RevokeSessionAsync(
        Guid sessionId,
        HttpContext context,
        IAntiforgery antiforgery,
        IAuthenticationSessionManagement sessions,
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

        await sessions.RevokeSessionAsync(userId, sessionId, cancellationToken);
        if (sessionId == currentSessionId)
        {
            ClearRefreshCookie(context);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> RevokeOtherSessionsAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        IAuthenticationSessionManagement sessions,
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

        await sessions.RevokeOtherSessionsAsync(userId, currentSessionId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        IAuthenticationSessionManagement sessions,
        CancellationToken cancellationToken)
    {
        if (await ValidateAntiforgeryAsync(context, antiforgery) is { } problem)
        {
            return problem;
        }
        if (TryGetSession(context.User, out var userId, out var sessionId))
        {
            await sessions.RevokeSessionAsync(userId, sessionId, cancellationToken);
        }
        ClearRefreshCookie(context);
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

    private static IResult PasswordLoginProblem(PasswordAuthenticationOutcome outcome)
    {
        var (status, code, detail) = outcome switch
        {
            PasswordAuthenticationOutcome.LockedOut =>
                (StatusCodes.Status423Locked, "account_locked", "Das Konto ist nach zu vielen Fehlversuchen für 15 Minuten gesperrt."),
            PasswordAuthenticationOutcome.RateLimited =>
                (StatusCodes.Status429TooManyRequests, "login_rate_limited", "Bitte warte, bevor du es erneut versuchst."),
            PasswordAuthenticationOutcome.Suspended =>
                (StatusCodes.Status423Locked, "account_suspended", "Dein Konto wurde gesperrt."),
            _ =>
                (StatusCodes.Status401Unauthorized, "invalid_credentials", "E-Mail-Adresse oder Passwort ist nicht korrekt.")
        };
        return Results.Problem(
            statusCode: status,
            title: "Anmeldung nicht möglich",
            detail: detail,
            extensions: new Dictionary<string, object?> { ["errorCode"] = code });
    }

    private static IResult PasswordMaintenanceProblem(PasswordChangeOutcome outcome)
    {
        var (status, code, detail) = outcome switch
        {
            PasswordChangeOutcome.InvalidPassword =>
                (StatusCodes.Status400BadRequest, "password_invalid", "Das neue Passwort muss zwischen 15 und 128 Zeichen lang sein."),
            PasswordChangeOutcome.Suspended =>
                (StatusCodes.Status423Locked, "account_suspended", "Dein Konto wurde gesperrt."),
            PasswordChangeOutcome.VersionConflict =>
                (StatusCodes.Status412PreconditionFailed, "version_conflict", "Das Konto wurde zwischenzeitlich geändert."),
            _ =>
                (StatusCodes.Status401Unauthorized, "invalid_credentials", "Das aktuelle Passwort ist nicht korrekt.")
        };
        return Results.Problem(
            statusCode: status,
            title: "Passwort konnte nicht geändert werden",
            detail: detail,
            extensions: new Dictionary<string, object?> { ["errorCode"] = code });
    }

    private sealed record PasswordLoginBody(string? Email, string? Password, bool RememberMe);

    private sealed record FirstLoginBody(
        string? Email,
        string? Password,
        string? FirstName,
        string? LastName);

    private sealed record PasswordResetRequestBody(string? Email);

    private sealed record PasswordResetConfirmBody(string? Token, string? NewPassword);

    private sealed record PasswordChangeBody(string? CurrentPassword, string? NewPassword);

    private sealed record ReauthenticationBody(string? Password);

    private sealed record AntiforgeryResponse(string Token);
}
