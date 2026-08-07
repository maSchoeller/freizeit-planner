using System.Globalization;
using System.Security.Claims;
using Identity.Contracts;
using Microsoft.AspNetCore.Antiforgery;

internal static class LifecycleEndpoints
{
    public static IEndpointRouteBuilder MapLifecycleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var invitations = endpoints.MapGroup("/api/v1/invitations");
        invitations.MapGet("/organizations/{organizationId:guid}", ListInvitationsAsync)
            .Produces<IReadOnlyList<InvitationSummary>>()
            .RequireAuthorization();
        invitations.MapPost("/organizations", CreateOrganizationInvitationAsync)
            .Produces<InvitationView>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireAuthorization();
        invitations.MapPost("/organizations/{organizationId:guid}", CreateTeamInvitationAsync)
            .Produces<InvitationView>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization();
        invitations.MapPost("/{invitationId:guid}/rotate", RotateInvitationAsync)
            .Produces<InvitationView>()
            .RequireAuthorization();
        invitations.MapDelete("/{invitationId:guid}", RevokeInvitationAsync)
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization();
        invitations.MapPost("/accept", AcceptInvitationAsync)
            .Produces<InvitationAcceptance>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .AllowAnonymous();

        var account = endpoints.MapGroup("/api/v1/account").RequireAuthorization();
        account.MapGet("/", GetAccountAsync).Produces<AccountView>();
        account.MapGet("/memberships", ListMembershipsAsync)
            .Produces<IReadOnlyList<AccountMembershipView>>();
        account.MapPatch("/profile", UpdateProfileAsync).Produces<AccountView>();
        account.MapPost("/email-change", RequestEmailChangeAsync).Produces(StatusCodes.Status204NoContent);
        account.MapPost("/email-change/confirm", ConfirmEmailChangeAsync).Produces<EmailChangeResult>();
        account.MapPost("/deletion", ScheduleAccountDeletionAsync).Produces<DeletionSchedule>();
        account.MapDelete("/deletion", CancelAccountDeletionAsync).Produces(StatusCodes.Status204NoContent);
        account.MapPost("/organizations/{organizationId:guid}/leave", LeaveOrganizationAsync)
            .Produces(StatusCodes.Status204NoContent);

        var organizations = endpoints.MapGroup("/api/v1/organizations").RequireAuthorization();
        organizations.MapPost("/{organizationId:guid}/deletion", ScheduleOrganizationDeletionAsync)
            .Produces<DeletionSchedule>();
        organizations.MapDelete("/{organizationId:guid}/deletion", CancelOrganizationDeletionAsync)
            .Produces(StatusCodes.Status204NoContent);
        return endpoints;
    }

    private static async Task<IResult> ListInvitationsAsync(
        Guid organizationId,
        ClaimsPrincipal principal,
        IInvitationLifecycle lifecycle,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(principal, out var actorId))
        {
            return Results.Unauthorized();
        }
        return await ExecuteAsync(async () => Results.Ok(
            await lifecycle.ListInvitationsAsync(actorId, organizationId, cancellationToken)));
    }

    private static async Task<IResult> GetAccountAsync(
        ClaimsPrincipal principal,
        IAccountLifecycle lifecycle,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(principal, out var actorId))
        {
            return Results.Unauthorized();
        }
        return await ExecuteAsync(async () => Results.Ok(
            await lifecycle.GetAccountAsync(actorId, cancellationToken)));
    }

    private static async Task<IResult> ListMembershipsAsync(
        ClaimsPrincipal principal,
        IAccountLifecycle lifecycle,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(principal, out var actorId))
        {
            return Results.Unauthorized();
        }
        return await ExecuteAsync(async () => Results.Ok(
            await lifecycle.ListMembershipsAsync(actorId, cancellationToken)));
    }

    private static async Task<IResult> CreateOrganizationInvitationAsync(
        OrganizationInvitationBody body,
        HttpContext context,
        IAntiforgery antiforgery,
        IInvitationLifecycle lifecycle,
        IInvitationSender sender,
        CancellationToken cancellationToken)
    {
        if (await ValidateMutationAsync(context, antiforgery) is { } failure)
        {
            return failure;
        }
        if (!TryGetActor(context.User, out var actorId))
        {
            return Results.Unauthorized();
        }

        return await ExecuteAsync(async () =>
        {
            var issued = await lifecycle.CreateOrganizationInvitationAsync(
                new OrganizationInvitationRequest(
                    actorId,
                    body.Email ?? string.Empty,
                    body.OrganizationName ?? string.Empty,
                    body.OrganizationSlug ?? string.Empty,
                    ReadIpAddress(context)),
                cancellationToken);
            await sender.SendAsync(issued, cancellationToken);
            return Results.Created($"/api/v1/invitations/{issued.Id}", InvitationView.From(issued));
        });
    }

    private static async Task<IResult> CreateTeamInvitationAsync(
        Guid organizationId,
        TeamInvitationBody body,
        HttpContext context,
        IAntiforgery antiforgery,
        IInvitationLifecycle lifecycle,
        IInvitationSender sender,
        CancellationToken cancellationToken)
    {
        if (await ValidateMutationAsync(context, antiforgery) is { } failure)
        {
            return failure;
        }
        if (!TryGetActor(context.User, out var actorId))
        {
            return Results.Unauthorized();
        }

        return await ExecuteAsync(async () =>
        {
            var issued = await lifecycle.IssueTeamInvitationAsync(
                new TeamInvitationRequest(
                    actorId,
                    organizationId,
                    body.Email ?? string.Empty,
                    body.Role,
                    body.CampId,
                    ReadIpAddress(context)),
                cancellationToken);
            await sender.SendAsync(issued, cancellationToken);
            return Results.Created($"/api/v1/invitations/{issued.Id}", InvitationView.From(issued));
        });
    }

    private static async Task<IResult> RotateInvitationAsync(
        Guid invitationId,
        HttpContext context,
        IAntiforgery antiforgery,
        IInvitationLifecycle lifecycle,
        IInvitationSender sender,
        CancellationToken cancellationToken)
    {
        if (await ValidateMutationAsync(context, antiforgery) is { } failure)
        {
            return failure;
        }
        if (!TryGetActor(context.User, out var actorId))
        {
            return Results.Unauthorized();
        }

        return await ExecuteAsync(async () =>
        {
            var issued = await lifecycle.RotateInvitationAsync(actorId, invitationId, cancellationToken);
            await sender.SendAsync(issued, cancellationToken);
            return Results.Ok(InvitationView.From(issued));
        });
    }

    private static async Task<IResult> RevokeInvitationAsync(
        Guid invitationId,
        HttpContext context,
        IAntiforgery antiforgery,
        IInvitationLifecycle lifecycle,
        CancellationToken cancellationToken)
    {
        if (await ValidateMutationAsync(context, antiforgery) is { } failure)
        {
            return failure;
        }
        if (!TryGetActor(context.User, out var actorId))
        {
            return Results.Unauthorized();
        }
        return await ExecuteAsync(async () =>
        {
            await lifecycle.RevokeInvitationAsync(actorId, invitationId, cancellationToken);
            return Results.NoContent();
        });
    }

    private static async Task<IResult> AcceptInvitationAsync(
        AcceptInvitationBody body,
        HttpContext context,
        IAntiforgery antiforgery,
        IInvitationLifecycle lifecycle,
        CancellationToken cancellationToken)
    {
        if (await ValidateMutationAsync(context, antiforgery) is { } failure)
        {
            return failure;
        }
        var result = await lifecycle.AcceptInvitationAsync(
            new AcceptInvitationRequest(body.Token ?? string.Empty, body.DisplayName ?? string.Empty),
            cancellationToken);
        return result.Outcome == InvitationAcceptanceOutcome.Accepted
            ? Results.Ok(result)
            : InvitationProblem(result.Outcome);
    }

    private static async Task<IResult> UpdateProfileAsync(
        UpdateProfileBody body,
        HttpContext context,
        IAntiforgery antiforgery,
        IAccountLifecycle lifecycle,
        CancellationToken cancellationToken)
    {
        return await ExecuteAccountMutationAsync(
            context,
            antiforgery,
            userId => lifecycle.UpdateDisplayNameAsync(
                userId,
                body.DisplayName ?? string.Empty,
                cancellationToken));
    }

    private static async Task<IResult> ScheduleAccountDeletionAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        IAccountLifecycle lifecycle,
        CancellationToken cancellationToken)
    {
        return await ExecuteAccountMutationAsync(
            context,
            antiforgery,
            userId => lifecycle.ScheduleAccountDeletionAsync(userId, cancellationToken));
    }

    private static async Task<IResult> RequestEmailChangeAsync(
        RequestEmailChangeBody body,
        HttpContext context,
        IAntiforgery antiforgery,
        IEmailChangeLifecycle lifecycle,
        CancellationToken cancellationToken)
    {
        return await ExecuteAccountMutationAsync(context, antiforgery, async userId =>
        {
            await lifecycle.RequestAsync(
                new EmailChangeRequest(
                    userId,
                    body.Email ?? string.Empty,
                    ReadIpAddress(context)),
                cancellationToken);
            return Results.NoContent();
        });
    }

    private static async Task<IResult> ConfirmEmailChangeAsync(
        ConfirmEmailChangeBody body,
        HttpContext context,
        IAntiforgery antiforgery,
        IEmailChangeLifecycle lifecycle,
        CancellationToken cancellationToken)
    {
        return await ExecuteAccountMutationAsync(context, antiforgery, async userId =>
        {
            var result = await lifecycle.ConfirmAsync(
                new ConfirmEmailChangeRequest(
                    userId,
                    body.Email ?? string.Empty,
                    body.Code ?? string.Empty),
                cancellationToken);
            return result.Outcome == EmailChangeOutcome.Changed
                ? Results.Ok(result)
                : Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Code nicht gültig",
                    detail: result.Outcome == EmailChangeOutcome.Expired
                        ? "Der Einmalcode ist abgelaufen."
                        : "Der Einmalcode ist ungültig.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["errorCode"] = result.Outcome == EmailChangeOutcome.Expired
                            ? "email_change_expired"
                            : "email_change_invalid"
                    });
        });
    }

    private static async Task<IResult> CancelAccountDeletionAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        IAccountLifecycle lifecycle,
        CancellationToken cancellationToken)
    {
        return await ExecuteAccountMutationAsync(context, antiforgery, async userId =>
        {
            await lifecycle.CancelAccountDeletionAsync(userId, cancellationToken);
            return Results.NoContent();
        });
    }

    private static async Task<IResult> LeaveOrganizationAsync(
        Guid organizationId,
        HttpContext context,
        IAntiforgery antiforgery,
        IAccountLifecycle lifecycle,
        CancellationToken cancellationToken)
    {
        return await ExecuteAccountMutationAsync(context, antiforgery, async userId =>
        {
            await lifecycle.LeaveOrganizationAsync(userId, organizationId, cancellationToken);
            return Results.NoContent();
        });
    }

    private static async Task<IResult> ScheduleOrganizationDeletionAsync(
        Guid organizationId,
        OrganizationDeletionBody body,
        HttpContext context,
        IAntiforgery antiforgery,
        IAccountLifecycle lifecycle,
        CancellationToken cancellationToken)
    {
        return await ExecuteAccountMutationAsync(context, antiforgery, userId =>
        {
            if (!TryReadAuthenticationTime(context.User, out var authenticatedAt))
            {
                throw new IdentityRuleException(
                    "fresh_reauthentication_required",
                    "Bitte melde dich erneut mit einem Einmalcode an.");
            }
            return lifecycle.ScheduleOrganizationDeletionAsync(
                new OrganizationDeletionRequest(
                    userId,
                    organizationId,
                    body.ConfirmedSlug ?? string.Empty,
                    authenticatedAt),
                cancellationToken);
        });
    }

    private static async Task<IResult> CancelOrganizationDeletionAsync(
        Guid organizationId,
        HttpContext context,
        IAntiforgery antiforgery,
        IAccountLifecycle lifecycle,
        CancellationToken cancellationToken)
    {
        return await ExecuteAccountMutationAsync(context, antiforgery, async userId =>
        {
            await lifecycle.CancelOrganizationDeletionAsync(userId, organizationId, cancellationToken);
            return Results.NoContent();
        });
    }

    private static async Task<IResult> ExecuteAccountMutationAsync<T>(
        HttpContext context,
        IAntiforgery antiforgery,
        Func<Guid, Task<T>> action)
    {
        if (await ValidateMutationAsync(context, antiforgery) is { } failure)
        {
            return failure;
        }
        if (!TryGetActor(context.User, out var actorId))
        {
            return Results.Unauthorized();
        }
        return await ExecuteAsync(async () => ToResult(await action(actorId)));
    }

    private static IResult ToResult<T>(T value) => value is IResult result ? result : Results.Ok(value);

    private static async Task<IResult> ExecuteAsync(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (IdentityRuleException exception)
        {
            var status = exception.ErrorCode switch
            {
                "invitation_rate_limited" => StatusCodes.Status429TooManyRequests,
                "email_change_rate_limited" => StatusCodes.Status429TooManyRequests,
                "platform_admin_required" or "owner_required" or "role_escalation" or "membership_required" =>
                    StatusCodes.Status403Forbidden,
                "invitation_not_found" or "organization_not_found" or "user_not_found" =>
                    StatusCodes.Status404NotFound,
                "last_owner" or "organization_slug_conflict" or "email_conflict" => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest
            };
            return Results.Problem(
                statusCode: status,
                title: "Aktion nicht möglich",
                detail: exception.Message,
                extensions: new Dictionary<string, object?> { ["errorCode"] = exception.ErrorCode });
        }
    }

    private static async ValueTask<IResult?> ValidateMutationAsync(
        HttpContext context,
        IAntiforgery antiforgery) =>
        await IdentityEndpoints.ValidateAntiforgeryAsync(context, antiforgery);

    private static bool TryGetActor(ClaimsPrincipal principal, out Guid actorId)
    {
        actorId = Guid.Empty;
        return Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out actorId);
    }

    private static bool TryReadAuthenticationTime(ClaimsPrincipal principal, out DateTimeOffset authenticatedAt)
    {
        authenticatedAt = default;
        return long.TryParse(
                principal.FindFirstValue("auth_time"),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var unixSeconds)
            && TryFromUnixTimeSeconds(unixSeconds, out authenticatedAt);
    }

    private static bool TryFromUnixTimeSeconds(long seconds, out DateTimeOffset value)
    {
        try
        {
            value = DateTimeOffset.FromUnixTimeSeconds(seconds);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            value = default;
            return false;
        }
    }

    private static string ReadIpAddress(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static IResult InvitationProblem(InvitationAcceptanceOutcome outcome)
    {
        var (code, detail) = outcome switch
        {
            InvitationAcceptanceOutcome.Expired => ("invitation_expired", "Die Einladung ist abgelaufen."),
            InvitationAcceptanceOutcome.Revoked => ("invitation_revoked", "Die Einladung wurde widerrufen."),
            InvitationAcceptanceOutcome.Used => ("invitation_used", "Die Einladung wurde bereits angenommen."),
            _ => ("invitation_invalid", "Die Einladung ist ungültig.")
        };
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Einladung nicht gültig",
            detail: detail,
            extensions: new Dictionary<string, object?> { ["errorCode"] = code });
    }

    private sealed record OrganizationInvitationBody(string? Email, string? OrganizationName, string? OrganizationSlug);

    private sealed record TeamInvitationBody(string? Email, TenantRole Role, Guid? CampId);

    private sealed record AcceptInvitationBody(string? Token, string? DisplayName);

    private sealed record UpdateProfileBody(string? DisplayName);

    private sealed record RequestEmailChangeBody(string? Email);

    private sealed record ConfirmEmailChangeBody(string? Email, string? Code);

    private sealed record OrganizationDeletionBody(string? ConfirmedSlug);

    private sealed record InvitationView(
        Guid Id,
        Guid OrganizationId,
        string Email,
        TenantRole Role,
        Guid? CampId,
        DateTimeOffset ExpiresAt)
    {
        public static InvitationView From(IssuedInvitation invitation) => new(
            invitation.Id,
            invitation.OrganizationId,
            invitation.Email,
            invitation.Role,
            invitation.CampId,
            invitation.ExpiresAt);
    }
}
