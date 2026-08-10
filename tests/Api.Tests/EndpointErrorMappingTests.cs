using System.Reflection;
using Activity.Contracts;
using Camps.Contracts;
using Catering.Contracts;
using Files.Contracts;
using Identity.Contracts;
using Knowledge.Contracts;
using Logistics.Contracts;
using Microsoft.AspNetCore.Http;
using Spiritual.Contracts;
using Xunit;

namespace Api.Tests;

public sealed class EndpointErrorMappingTests
{
    public static TheoryData<string, int> LifecycleErrors => new()
    {
        { "invitation_rate_limited", StatusCodes.Status429TooManyRequests },
        { "email_change_rate_limited", StatusCodes.Status429TooManyRequests },
        { "platform_admin_required", StatusCodes.Status403Forbidden },
        { "owner_required", StatusCodes.Status403Forbidden },
        { "role_escalation", StatusCodes.Status403Forbidden },
        { "membership_required", StatusCodes.Status403Forbidden },
        { "invitation_not_found", StatusCodes.Status404NotFound },
        { "organization_not_found", StatusCodes.Status404NotFound },
        { "user_not_found", StatusCodes.Status404NotFound },
        { "last_owner", StatusCodes.Status409Conflict },
        { "organization_slug_conflict", StatusCodes.Status409Conflict },
        { "email_conflict", StatusCodes.Status409Conflict },
        { "invalid_request", StatusCodes.Status400BadRequest }
    };

    public static TheoryData<string, int> AdministrationErrors => new()
    {
        { "version_conflict", StatusCodes.Status412PreconditionFailed },
        { "last_owner", StatusCodes.Status409Conflict },
        { "organization_not_found", StatusCodes.Status404NotFound },
        { "user_not_found", StatusCodes.Status404NotFound },
        { "camp_assignment_not_found", StatusCodes.Status404NotFound },
        { "platform_admin_required", StatusCodes.Status403Forbidden },
        { "role_escalation", StatusCodes.Status403Forbidden },
        { "membership_management_denied", StatusCodes.Status403Forbidden },
        { "membership_required", StatusCodes.Status403Forbidden },
        { "camp_assignment_required", StatusCodes.Status403Forbidden },
        { "invalid_request", StatusCodes.Status400BadRequest }
    };

    public static TheoryData<string, int> CampErrors => new()
    {
        { "version_conflict", StatusCodes.Status412PreconditionFailed },
        { "camp_not_found", StatusCodes.Status404NotFound },
        { "schedule_entry_not_found", StatusCodes.Status404NotFound },
        { "camp_access_denied", StatusCodes.Status403Forbidden },
        { "schedule_access_denied", StatusCodes.Status403Forbidden },
        { "camp_archived", StatusCodes.Status409Conflict },
        { "invalid_request", StatusCodes.Status400BadRequest }
    };

    [Theory]
    [MemberData(nameof(LifecycleErrors))]
    public async Task LifecycleErrorsHaveStableHttpStatus(string errorCode, int expectedStatus)
        => Assert.Equal(expectedStatus, await ExecuteAsync("LifecycleEndpoints",
            new IdentityRuleException(errorCode, "Fehler")));

    [Theory]
    [MemberData(nameof(AdministrationErrors))]
    public async Task AdministrationErrorsHaveStableHttpStatus(string errorCode, int expectedStatus)
        => Assert.Equal(expectedStatus, await ExecuteAsync("TenantAdministrationEndpoints",
            new IdentityRuleException(errorCode, "Fehler")));

    [Theory]
    [MemberData(nameof(CampErrors))]
    public async Task CampErrorsHaveStableHttpStatus(string errorCode, int expectedStatus)
        => Assert.Equal(expectedStatus, await ExecuteAsync("CampPlanningEndpoints",
            new CampsRuleException(errorCode, "Fehler")));

    [Fact]
    public async Task PlanningEndpointFamiliesMapEveryDomainExceptionToProblemDetails()
    {
        var errors = new (string Endpoint, Exception Exception)[]
        {
            ("ActivityEndpoints", new ActivityRuleException("activity_invalid", "Fehler")),
            ("ActivityEndpoints", new IdentityRuleException("identity_invalid", "Fehler")),
            ("CateringEndpoints", new CateringRuleException("catering_invalid", "Fehler")),
            ("CateringEndpoints", new ActivityRuleException("activity_invalid", "Fehler")),
            ("FileEndpoints", new FilesRuleException("files_invalid", "Fehler")),
            ("FileEndpoints", new ActivityRuleException("activity_invalid", "Fehler")),
            ("KnowledgeEndpoints", new KnowledgeRuleException("knowledge_invalid", "Fehler")),
            ("KnowledgeEndpoints", new ActivityRuleException("activity_invalid", "Fehler")),
            ("LogisticsEndpoints", new LogisticsRuleException("logistics_invalid", "Fehler")),
            ("LogisticsEndpoints", new ActivityRuleException("activity_invalid", "Fehler")),
            ("SpiritualEndpoints", new SpiritualRuleException("spiritual_invalid", "Fehler")),
            ("SpiritualEndpoints", new ActivityRuleException("activity_invalid", "Fehler")),
            ("CampPlanningEndpoints", new ActivityRuleException("activity_invalid", "Fehler")),
            ("CampPlanningEndpoints", new CateringRuleException("catering_invalid", "Fehler")),
            ("CampPlanningEndpoints", new SpiritualRuleException("spiritual_invalid", "Fehler")),
            ("CampPlanningEndpoints", new IdentityRuleException("identity_invalid", "Fehler"))
        };

        foreach (var (endpoint, exception) in errors)
            Assert.Equal(StatusCodes.Status400BadRequest, await ExecuteAsync(endpoint, exception));
    }

    private static async Task<int> ExecuteAsync(string endpointTypeName, Exception exception)
    {
        var endpointType = typeof(Program).Assembly.GetType(endpointTypeName)
            ?? throw new InvalidOperationException($"Endpoint type {endpointTypeName} was not found.");
        var method = endpointType.GetMethod("ExecuteAsync", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"ExecuteAsync was not found on {endpointTypeName}.");
        Func<Task<IResult>> action = () => Task.FromException<IResult>(exception);
        var task = method.Invoke(null, [action]) as Task<IResult>
            ?? throw new InvalidOperationException($"ExecuteAsync on {endpointTypeName} returned no task.");
        var result = await task;
        return Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode
            ?? throw new InvalidOperationException("The problem result has no HTTP status.");
    }
}
