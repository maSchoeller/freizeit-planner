using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;

internal static class PlanningEndpointSupport
{
    public static bool TryActor(ClaimsPrincipal principal, out Guid actorId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out actorId);

    public static ValueTask<IResult?> ValidateMutationAsync(
        HttpContext context,
        IAntiforgery antiforgery) =>
        IdentityEndpoints.ValidateAntiforgeryAsync(context, antiforgery);

    public static bool TryReadVersion(HttpRequest request, out long version)
    {
        version = default;
        var value = request.Headers.IfMatch.ToString().Trim();
        if (value.StartsWith("W/", StringComparison.OrdinalIgnoreCase)) value = value[2..];
        return long.TryParse(value.Trim('"'), NumberStyles.None, CultureInfo.InvariantCulture, out version)
            && version > 0;
    }

    public static IResult PreconditionRequired() => Results.Problem(
        statusCode: StatusCodes.Status428PreconditionRequired,
        title: "Versionsangabe erforderlich",
        detail: "Sende die zuletzt gelesene Version im If-Match-Header.",
        extensions: new Dictionary<string, object?> { ["errorCode"] = "if_match_required" });

    public static void WriteEtag(HttpResponse response, long version) =>
        response.Headers.ETag = $"\"{version.ToString(CultureInfo.InvariantCulture)}\"";

    public static IResult Problem(string errorCode, string message, string title)
    {
        var status = errorCode switch
        {
            "version_conflict" => StatusCodes.Status412PreconditionFailed,
            var code when code.EndsWith("_not_found", StringComparison.Ordinal) => StatusCodes.Status404NotFound,
            var code when code.EndsWith("_access_denied", StringComparison.Ordinal) => StatusCodes.Status403Forbidden,
            "camp_archived" => StatusCodes.Status409Conflict,
            "provider_unavailable" or "provider_timeout" => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status400BadRequest
        };
        return Results.Problem(
            statusCode: status,
            title: title,
            detail: message,
            extensions: new Dictionary<string, object?> { ["errorCode"] = errorCode });
    }
}
