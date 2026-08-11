using System.Globalization;
using System.Security.Claims;
using Identity.Contracts;
using Microsoft.AspNetCore.Antiforgery;

internal static class TenantAdministrationEndpoints
{
    public static IEndpointRouteBuilder MapTenantAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var organizations = endpoints.MapGroup("/api/v1/organizations").RequireAuthorization();
        organizations.MapGet("/{organizationId:guid}/members", ListMembersAsync)
            .Produces<IReadOnlyList<OrganizationMemberView>>();
        organizations.MapPatch("/{organizationId:guid}/members/{userId:guid}/role", ChangeRoleAsync)
            .Produces<OrganizationMemberView>()
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);
        organizations.MapDelete("/{organizationId:guid}/members/{userId:guid}", RemoveMemberAsync)
            .Produces(StatusCodes.Status204NoContent);
        organizations.MapPut("/{organizationId:guid}/camps/{campId:guid}/members/{userId:guid}", AssignCampAsync)
            .Produces<CampAssignmentView>();
        organizations.MapDelete("/{organizationId:guid}/camps/{campId:guid}/members/{userId:guid}", RemoveCampAsync)
            .Produces(StatusCodes.Status204NoContent);

        var superAdmin = endpoints.MapGroup("/api/v1/superadmin").RequireAuthorization();
        superAdmin.MapGet("/users", SearchSuperAdminUsersAsync)
            .Produces<AdministrationPage<UserAdministrationView>>();
        superAdmin.MapGet("/organizations", ListSuperAdminOrganizationsAsync)
            .Produces<IReadOnlyList<SuperAdminOrganizationView>>();
        superAdmin.MapPatch("/organizations/{organizationId:guid}/status", ChangeStatusAsync)
            .Produces<OrganizationStatusView>()
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);
        superAdmin.MapPatch("/users/{userId:guid}/status", ChangeGlobalAccountStatusAsync)
            .Produces<UserAdministrationView>();
        superAdmin.MapPatch("/users/{userId:guid}/superadmin", ChangeSuperAdminAsync)
            .Produces<UserAdministrationView>();
        superAdmin.MapPost("/users/{userId:guid}/unlock", ClearLoginLockoutAsync)
            .Produces<UserAdministrationView>();
        superAdmin.MapPut("/users/{userId:guid}/organizations/{organizationId:guid}", ChangeMembershipAsync)
            .Produces<OrganizationAdministrationView>();
        superAdmin.MapPut("/users/{userId:guid}/organizations/{organizationId:guid}/camps/{campId:guid}",
                ChangeCampAssignmentAsync)
            .Produces<CampAdministrationView>()
            .Produces(StatusCodes.Status204NoContent);

        organizations.MapGet("/{organizationId:guid}/administration/users", SearchOrganizationUsersAsync)
            .Produces<AdministrationPage<UserAdministrationView>>();
        organizations.MapPut("/{organizationId:guid}/administration/users/{userId:guid}/membership",
                ChangeMembershipAsync)
            .Produces<OrganizationAdministrationView>();
        organizations.MapPut("/{organizationId:guid}/administration/users/{userId:guid}/camps/{campId:guid}",
                ChangeCampAssignmentAsync)
            .Produces<CampAdministrationView>()
            .Produces(StatusCodes.Status204NoContent);
        return endpoints;
    }

    private static async Task<IResult> SearchSuperAdminUsersAsync(
        string? search,
        int? page,
        int? pageSize,
        ClaimsPrincipal principal,
        IUserAdministration administration,
        CancellationToken cancellationToken) =>
        TryActor(principal, out var actorId)
            ? await ExecuteAsync(async () => Results.Ok(await administration.SearchUsersAsync(
                new UserAdministrationQuery(actorId, search, page ?? 1, pageSize ?? 25),
                cancellationToken)))
            : Results.Unauthorized();

    private static async Task<IResult> SearchOrganizationUsersAsync(
        Guid organizationId,
        string? search,
        int? page,
        int? pageSize,
        ClaimsPrincipal principal,
        IUserAdministration administration,
        CancellationToken cancellationToken) =>
        TryActor(principal, out var actorId)
            ? await ExecuteAsync(async () => Results.Ok(await administration.SearchUsersAsync(
                new UserAdministrationQuery(actorId, search, page ?? 1, pageSize ?? 25, organizationId),
                cancellationToken)))
            : Results.Unauthorized();

    private static async Task<IResult> ListSuperAdminOrganizationsAsync(
        ClaimsPrincipal principal,
        IUserAdministration administration,
        CancellationToken cancellationToken) =>
        TryActor(principal, out var actorId)
            ? await ExecuteAsync(async () => Results.Ok(
                await administration.ListOrganizationsAsync(actorId, cancellationToken)))
            : Results.Unauthorized();

    private static async Task<IResult> ChangeGlobalAccountStatusAsync(
        Guid userId,
        ChangeGlobalAccountStatusBody body,
        HttpContext context,
        IAntiforgery antiforgery,
        IUserAdministration administration,
        CancellationToken cancellationToken)
    {
        if (await ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!TryReadAdministrationVersion(context.Request, out var version)) return PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var result = await administration.ChangeGlobalAccountStatusAsync(
                new ChangeGlobalAccountStatusCommand(actorId, userId, body.Status, version),
                cancellationToken);
            WriteEtag(context.Response, result.Version);
            return Results.Ok(result);
        });
    }

    private static async Task<IResult> ChangeSuperAdminAsync(
        Guid userId,
        ChangeSuperAdminBody body,
        HttpContext context,
        IAntiforgery antiforgery,
        IUserAdministration administration,
        CancellationToken cancellationToken)
    {
        if (await ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!TryReadAdministrationVersion(context.Request, out var version)) return PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var result = await administration.ChangeSuperAdminAsync(
                new ChangeSuperAdminCommand(actorId, userId, body.IsSuperAdmin, version),
                cancellationToken);
            WriteEtag(context.Response, result.Version);
            return Results.Ok(result);
        });
    }

    private static async Task<IResult> ClearLoginLockoutAsync(
        Guid userId,
        HttpContext context,
        IAntiforgery antiforgery,
        IUserAdministration administration,
        CancellationToken cancellationToken)
    {
        if (await ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!TryReadAdministrationVersion(context.Request, out var version)) return PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var result = await administration.ClearLoginLockoutAsync(
                new ClearLoginLockoutCommand(actorId, userId, version),
                cancellationToken);
            WriteEtag(context.Response, result.Version);
            return Results.Ok(result);
        });
    }

    private static async Task<IResult> ChangeMembershipAsync(
        Guid organizationId,
        Guid userId,
        ChangeMembershipBody body,
        HttpContext context,
        IAntiforgery antiforgery,
        IUserAdministration administration,
        CancellationToken cancellationToken)
    {
        if (await ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!TryReadAdministrationVersion(context.Request, out var version)) return PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var result = await administration.ChangeMembershipAsync(
                new ChangeMembershipCommand(actorId, organizationId, userId, body.Status, body.Role, version),
                cancellationToken);
            WriteEtag(context.Response, result.Version);
            return Results.Ok(result);
        });
    }

    private static async Task<IResult> ChangeCampAssignmentAsync(
        Guid organizationId,
        Guid campId,
        Guid userId,
        ChangeCampAssignmentBody body,
        HttpContext context,
        IAntiforgery antiforgery,
        IUserAdministration administration,
        CancellationToken cancellationToken)
    {
        if (await ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!TryReadAdministrationVersion(context.Request, out var version)) return PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var result = await administration.ChangeCampAssignmentAsync(
                new ChangeCampAssignmentCommand(actorId, organizationId, campId, userId, body.Role, version),
                cancellationToken);
            if (result is null) return Results.NoContent();
            WriteEtag(context.Response, result.Version);
            return Results.Ok(result);
        });
    }

    private static async Task<IResult> ListMembersAsync(
        Guid organizationId,
        ClaimsPrincipal principal,
        ITenantAdministration administration,
        CancellationToken cancellationToken)
    {
        return TryActor(principal, out var actorId)
            ? await ExecuteAsync(async () => Results.Ok(
                await administration.ListOrganizationMembersAsync(
                    actorId,
                    organizationId,
                    cancellationToken)))
            : Results.Unauthorized();
    }

    private static async Task<IResult> ChangeRoleAsync(
        Guid organizationId,
        Guid userId,
        ChangeRoleBody body,
        HttpContext context,
        IAntiforgery antiforgery,
        ITenantAdministration administration,
        CancellationToken cancellationToken)
    {
        if (await ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!TryReadVersion(context.Request, out var version)) return PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var result = await administration.ChangeOrganizationRoleAsync(
                new OrganizationRoleChange(actorId, organizationId, userId, body.Role, version),
                cancellationToken);
            WriteEtag(context.Response, result.Version);
            return Results.Ok(result);
        });
    }

    private static async Task<IResult> RemoveMemberAsync(
        Guid organizationId,
        Guid userId,
        HttpContext context,
        IAntiforgery antiforgery,
        ITenantAdministration administration,
        CancellationToken cancellationToken)
    {
        if (await ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!TryReadVersion(context.Request, out var version)) return PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            await administration.RemoveOrganizationMemberAsync(
                new OrganizationMemberRemoval(actorId, organizationId, userId, version),
                cancellationToken);
            return Results.NoContent();
        });
    }

    private static async Task<IResult> AssignCampAsync(
        Guid organizationId,
        Guid campId,
        Guid userId,
        AssignCampBody body,
        HttpContext context,
        IAntiforgery antiforgery,
        ITenantAdministration administration,
        CancellationToken cancellationToken)
    {
        if (await ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!TryActor(context.User, out var actorId)) return Results.Unauthorized();
        var hasVersion = TryReadVersion(context.Request, out var version);
        return await ExecuteAsync(async () =>
        {
            var result = await administration.AssignCampMemberAsync(
                new CampMemberAssignment(
                    actorId,
                    organizationId,
                    campId,
                    userId,
                    body.Role,
                    hasVersion ? version : null),
                cancellationToken);
            WriteEtag(context.Response, result.Version);
            return Results.Ok(result);
        });
    }

    private static async Task<IResult> RemoveCampAsync(
        Guid organizationId,
        Guid campId,
        Guid userId,
        HttpContext context,
        IAntiforgery antiforgery,
        ITenantAdministration administration,
        CancellationToken cancellationToken)
    {
        if (await ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!TryReadVersion(context.Request, out var version)) return PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            await administration.RemoveCampMemberAsync(
                new CampMemberRemoval(actorId, organizationId, campId, userId, version),
                cancellationToken);
            return Results.NoContent();
        });
    }

    private static async Task<IResult> ChangeStatusAsync(
        Guid organizationId,
        ChangeStatusBody body,
        HttpContext context,
        IAntiforgery antiforgery,
        ISuperAdminOrganizationAdministration administration,
        CancellationToken cancellationToken)
    {
        if (await ValidateMutationAsync(context, antiforgery) is { } failure) return failure;
        if (!TryActor(context.User, out var actorId)) return Results.Unauthorized();
        if (!TryReadVersion(context.Request, out var version)) return PreconditionRequired();
        return await ExecuteAsync(async () =>
        {
            var result = await administration.ChangeOrganizationStatusAsync(
                new OrganizationStatusChange(actorId, organizationId, body.Status, version),
                cancellationToken);
            WriteEtag(context.Response, result.Version);
            return Results.Ok(result);
        });
    }

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
                "version_conflict" => StatusCodes.Status412PreconditionFailed,
                "last_super_admin" or "self_suspension" => StatusCodes.Status409Conflict,
                "organization_not_found" or "user_not_found" or "camp_assignment_not_found" =>
                    StatusCodes.Status404NotFound,
                "super_admin_required" or "organization_admin_required"
                    or "role_escalation" or "membership_management_denied" or "membership_required"
                    or "camp_assignment_required" or "account_suspended" => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status400BadRequest
            };
            return Results.Problem(
                statusCode: status,
                title: "Aktion nicht möglich",
                detail: exception.Message,
                extensions: new Dictionary<string, object?> { ["errorCode"] = exception.ErrorCode });
        }
    }

    private static ValueTask<IResult?> ValidateMutationAsync(HttpContext context, IAntiforgery antiforgery) =>
        IdentityEndpoints.ValidateAntiforgeryAsync(context, antiforgery);

    private static bool TryActor(ClaimsPrincipal principal, out Guid actorId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out actorId);

    private static bool TryReadVersion(HttpRequest request, out long version)
    {
        version = default;
        var value = request.Headers.IfMatch.ToString().Trim();
        if (value.StartsWith("W/", StringComparison.OrdinalIgnoreCase)) value = value[2..];
        return long.TryParse(value.Trim('"'), NumberStyles.None, CultureInfo.InvariantCulture, out version)
            && version > 0;
    }

    private static bool TryReadAdministrationVersion(HttpRequest request, out long version)
    {
        version = default;
        var value = request.Headers.IfMatch.ToString().Trim();
        if (value.StartsWith("W/", StringComparison.OrdinalIgnoreCase)) value = value[2..];
        return long.TryParse(value.Trim('"'), NumberStyles.None, CultureInfo.InvariantCulture, out version)
            && version >= 0;
    }

    private static IResult PreconditionRequired() => Results.Problem(
        statusCode: StatusCodes.Status428PreconditionRequired,
        title: "Versionsangabe erforderlich",
        detail: "Sende die zuletzt gelesene Version im If-Match-Header.",
        extensions: new Dictionary<string, object?> { ["errorCode"] = "if_match_required" });

    private static void WriteEtag(HttpResponse response, long version) =>
        response.Headers.ETag = $"\"{version.ToString(CultureInfo.InvariantCulture)}\"";

    private sealed record ChangeRoleBody(TenantRole Role);

    private sealed record AssignCampBody(TenantRole Role);

    private sealed record ChangeStatusBody(OrganizationStatus Status);

    private sealed record ChangeGlobalAccountStatusBody(AccountStatus Status);

    private sealed record ChangeSuperAdminBody(bool IsSuperAdmin);

    private sealed record ChangeMembershipBody(MembershipStatus Status, OrganizationRole? Role);

    private sealed record ChangeCampAssignmentBody(CampRole? Role);
}
