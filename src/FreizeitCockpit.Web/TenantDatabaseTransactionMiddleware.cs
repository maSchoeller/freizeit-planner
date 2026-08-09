using Activity.Implementation;
using System.Security.Claims;
using Camps.Implementation;
using Catering.Implementation;
using Identity.Implementation;
using Knowledge.Implementation;
using Logistics.Implementation;
using Files.Implementation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Spiritual.Implementation;

internal sealed class TenantDatabaseTransactionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var database = context.RequestServices.GetService<IdentityDbContext>();
        if (database is null || !context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        await using var transaction = await database.Database.BeginTransactionAsync(context.RequestAborted);
        await EnlistAsync<CampsDbContext>(context, transaction, context.RequestAborted);
        await EnlistAsync<CateringDbContext>(context, transaction, context.RequestAborted);
        await EnlistAsync<SpiritualDbContext>(context, transaction, context.RequestAborted);
        await EnlistAsync<KnowledgeDbContext>(context, transaction, context.RequestAborted);
        await EnlistAsync<LogisticsDbContext>(context, transaction, context.RequestAborted);
        await EnlistAsync<FilesDbContext>(context, transaction, context.RequestAborted);
        await EnlistAsync<ActivityDbContext>(context, transaction, context.RequestAborted);
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var (organizationId, campId) = ReadTenantRoute(context.Request.Path);
        var operation = ReadOperation(context.Request);
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.user_id', {userId}, true), set_config('app.organization_id', {organizationId}, true), set_config('app.camp_id', {campId}, true), set_config('app.operation', {operation}, true)",
            context.RequestAborted);
        try
        {
            await next(context);
            if (context.Response.StatusCode < StatusCodes.Status400BadRequest)
            {
                await transaction.CommitAsync(context.RequestAborted);
            }
            else
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task EnlistAsync<TContext>(
        HttpContext context,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
        where TContext : DbContext
    {
        var database = context.RequestServices.GetService<TContext>();
        if (database is not null)
        {
            await database.Database.UseTransactionAsync(
                transaction.GetDbTransaction(),
                cancellationToken);
        }
    }

    private static string ReadOperation(HttpRequest request)
    {
        if (HttpMethods.IsPost(request.Method)
            && request.Path.Equals("/api/v1/invitations/accept"))
        {
            return "invitation_acceptance";
        }
        if (HttpMethods.IsPost(request.Method)
            && request.Path.Equals("/api/v1/invitations/organizations"))
        {
            return "platform_create_organization";
        }
        return request.Path.StartsWithSegments("/api/v1/platform")
            ? "platform_admin"
            : "tenant";
    }

    private static (string OrganizationId, string CampId) ReadTenantRoute(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        string organizationId = string.Empty;
        string campId = string.Empty;
        for (var index = 0; index + 1 < segments.Length; index++)
        {
            if (segments[index] == "organizations" && Guid.TryParse(segments[index + 1], out var organization))
            {
                organizationId = organization.ToString("D");
            }
            if (segments[index] == "camps" && Guid.TryParse(segments[index + 1], out var camp))
            {
                campId = camp.ToString("D");
            }
        }
        return (organizationId, campId);
    }
}
