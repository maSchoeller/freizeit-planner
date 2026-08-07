using System.Text.RegularExpressions;
using Camps.Contracts;
using Identity.Contracts;

namespace Camps.Implementation;

public sealed partial class CampPlanningService(
    ICampsState state,
    ITenantAccessControl accessControl,
    TimeProvider timeProvider) : ICampManagement, ICampPlanningDefaults
{
    private const string DefaultTimeZoneId = "Europe/Berlin";

    public async Task<IReadOnlyList<CampSummary>> ListAsync(
        CampListQuery query,
        CancellationToken cancellationToken)
    {
        var camps = await state.ListCampsAsync(query.OrganizationId, cancellationToken);
        var visible = new List<CampSummary>(camps.Count);
        foreach (var camp in camps)
        {
            var decision = await accessControl.AuthorizeCampAsync(
                new CampAccessRequest(
                    query.ActorId,
                    query.OrganizationId,
                    camp.Id,
                    CampAction.Read),
                cancellationToken);
            if (decision.Allowed)
            {
                visible.Add(ToSummary(camp));
            }
        }

        return visible
            .OrderBy(item => item.StartsOn)
            .ThenBy(item => item.Name, StringComparer.CurrentCulture)
            .ToArray();
    }

    public async Task<CampView> GetBySlugAsync(
        CampBySlugQuery query,
        CancellationToken cancellationToken)
    {
        var slug = NormalizeSlug(query.CampSlug);
        var camp = await state.FindCampBySlugAsync(query.OrganizationId, slug, cancellationToken)
            ?? throw Rule("camp_not_found", "Das Camp wurde nicht gefunden.");
        await RequireCampAccessAsync(
            query.ActorId,
            query.OrganizationId,
            camp.Id,
            CampAction.Read,
            cancellationToken);
        return ToView(camp);
    }

    public async Task<CampView> CreateAsync(
        CreateCamp command,
        CancellationToken cancellationToken)
    {
        await RequireOrganizationAccessAsync(
            command.ActorId,
            command.OrganizationId,
            OrganizationAction.ManageCamps,
            cancellationToken);
        var values = Validate(
            command.Name,
            command.Slug,
            command.Description,
            command.StartsOn,
            command.EndsOn,
            command.TimeZoneId,
            command.DefaultPortions);
        if (await state.FindCampBySlugAsync(
                command.OrganizationId,
                values.Slug,
                cancellationToken) is not null)
        {
            throw Rule("camp_slug_conflict", "Dieser Camp-Link ist bereits vergeben.");
        }

        var camp = new CampRecord(
            Guid.NewGuid(),
            command.OrganizationId,
            values.Name,
            values.Slug,
            values.Description,
            command.StartsOn,
            command.EndsOn,
            values.TimeZone.Id,
            command.DefaultPortions);
        await state.AddCampAsync(camp, cancellationToken);
        return ToView(camp);
    }

    public async Task<CampView> UpdateAsync(
        UpdateCamp command,
        CancellationToken cancellationToken)
    {
        await RequireCampAccessAsync(
            command.ActorId,
            command.OrganizationId,
            command.CampId,
            CampAction.ManageCamp,
            cancellationToken);
        var camp = await RequireCampAsync(
            command.OrganizationId,
            command.CampId,
            cancellationToken);
        RequireActive(camp);
        var values = Validate(
            command.Name,
            command.Slug,
            command.Description,
            command.StartsOn,
            command.EndsOn,
            command.TimeZoneId,
            command.DefaultPortions);
        var duplicate = await state.FindCampBySlugAsync(
            command.OrganizationId,
            values.Slug,
            cancellationToken);
        if (duplicate is not null && duplicate.Id != camp.Id)
        {
            throw Rule("camp_slug_conflict", "Dieser Camp-Link ist bereits vergeben.");
        }

        camp.Update(
            values.Name,
            values.Slug,
            values.Description,
            command.StartsOn,
            command.EndsOn,
            values.TimeZone.Id,
            command.DefaultPortions,
            command.ExpectedVersion);
        await state.SaveCampAsync(camp, command.ExpectedVersion, cancellationToken);
        return ToView(camp);
    }

    public async Task<CampView> ChangeStatusAsync(
        ChangeCampStatus command,
        CancellationToken cancellationToken)
    {
        await RequireCampAccessAsync(
            command.ActorId,
            command.OrganizationId,
            command.CampId,
            CampAction.ManageCamp,
            cancellationToken);
        var camp = await RequireCampAsync(
            command.OrganizationId,
            command.CampId,
            cancellationToken);
        camp.ChangeStatus(command.Status, command.ExpectedVersion);
        await state.SaveCampAsync(camp, command.ExpectedVersion, cancellationToken);
        return ToView(camp);
    }

    public async Task<CampPlanningDefaults> GetAsync(
        CampAccessQuery query,
        CancellationToken cancellationToken)
    {
        await RequireCampAccessAsync(
            query.ActorId,
            query.OrganizationId,
            query.CampId,
            CampAction.Read,
            cancellationToken);
        var camp = await RequireCampAsync(query.OrganizationId, query.CampId, cancellationToken);
        return new CampPlanningDefaults(camp.Id, camp.DefaultPortions, camp.Status, camp.Version);
    }

    internal async Task<CampRecord> RequireWritableCampAsync(
        Guid actorId,
        Guid organizationId,
        Guid campId,
        CampAction action,
        CancellationToken cancellationToken)
    {
        await RequireCampAccessAsync(actorId, organizationId, campId, action, cancellationToken);
        var camp = await RequireCampAsync(organizationId, campId, cancellationToken);
        RequireActive(camp);
        return camp;
    }

    internal async Task<CampRecord> RequireReadableCampAsync(
        Guid actorId,
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken)
    {
        await RequireCampAccessAsync(
            actorId,
            organizationId,
            campId,
            CampAction.Read,
            cancellationToken);
        return await RequireCampAsync(organizationId, campId, cancellationToken);
    }

    private async Task<CampRecord> RequireCampAsync(
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken) =>
        await state.FindCampAsync(organizationId, campId, cancellationToken)
        ?? throw Rule("camp_not_found", "Das Camp wurde nicht gefunden.");

    private async Task RequireOrganizationAccessAsync(
        Guid actorId,
        Guid organizationId,
        OrganizationAction action,
        CancellationToken cancellationToken)
    {
        var decision = await accessControl.AuthorizeOrganizationAsync(
            new OrganizationAccessRequest(actorId, organizationId, action),
            cancellationToken);
        if (!decision.Allowed)
        {
            throw Rule("camp_access_denied", "Du darfst Camps in dieser Organization nicht verwalten.");
        }
    }

    private async Task RequireCampAccessAsync(
        Guid actorId,
        Guid organizationId,
        Guid campId,
        CampAction action,
        CancellationToken cancellationToken)
    {
        var decision = await accessControl.AuthorizeCampAsync(
            new CampAccessRequest(actorId, organizationId, campId, action),
            cancellationToken);
        if (!decision.Allowed)
        {
            throw Rule("camp_access_denied", "Du darfst dieses Camp nicht verwenden.");
        }
    }

    private CampView ToView(CampRecord camp) => new(
        camp.Id,
        camp.OrganizationId,
        camp.Name,
        camp.Slug,
        camp.Description,
        camp.StartsOn,
        camp.EndsOn,
        camp.TimeZoneId,
        camp.DefaultPortions,
        camp.Status,
        GetPeriod(camp),
        camp.Version);

    private CampSummary ToSummary(CampRecord camp) => new(
        camp.Id,
        camp.OrganizationId,
        camp.Name,
        camp.Slug,
        camp.StartsOn,
        camp.EndsOn,
        camp.TimeZoneId,
        camp.DefaultPortions,
        camp.Status,
        GetPeriod(camp),
        camp.Version);

    private CampPeriod GetPeriod(CampRecord camp)
    {
        var localNow = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), FindTimeZone(camp.TimeZoneId));
        var today = DateOnly.FromDateTime(localNow.DateTime);
        if (today < camp.StartsOn) return CampPeriod.Upcoming;
        return today > camp.EndsOn ? CampPeriod.Past : CampPeriod.Ongoing;
    }

    private static CampValues Validate(
        string name,
        string slug,
        string? description,
        DateOnly startsOn,
        DateOnly endsOn,
        string? timeZoneId,
        int defaultPortions)
    {
        var normalizedName = name.Trim();
        if (normalizedName.Length is < 1 or > 160)
        {
            throw Rule("invalid_camp_name", "Der Camp-Name muss zwischen 1 und 160 Zeichen lang sein.");
        }
        var normalizedSlug = NormalizeSlug(slug);
        var normalizedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (normalizedDescription?.Length > 4000)
        {
            throw Rule("invalid_camp_description", "Die Beschreibung darf höchstens 4000 Zeichen lang sein.");
        }
        if (endsOn < startsOn)
        {
            throw Rule("invalid_camp_dates", "Das Enddatum darf nicht vor dem Startdatum liegen.");
        }
        if (defaultPortions <= 0)
        {
            throw Rule("invalid_default_portions", "Die Standard-Personenzahl muss größer als null sein.");
        }
        var timeZone = FindTimeZone(string.IsNullOrWhiteSpace(timeZoneId) ? DefaultTimeZoneId : timeZoneId.Trim());
        return new CampValues(normalizedName, normalizedSlug, normalizedDescription, timeZone);
    }

    private static string NormalizeSlug(string slug)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        if (normalized.Length is < 1 or > 80 || !SlugPattern().IsMatch(normalized))
        {
            throw Rule(
                "invalid_camp_slug",
                "Der Camp-Link darf nur Kleinbuchstaben, Zahlen und einzelne Bindestriche enthalten.");
        }
        return normalized;
    }

    private static TimeZoneInfo FindTimeZone(string timeZoneId)
    {
        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            if (!timeZone.HasIanaId)
            {
                throw Rule("invalid_time_zone", "Bitte wähle eine gültige IANA-Zeitzone.");
            }
            return timeZone;
        }
        catch (TimeZoneNotFoundException)
        {
            throw Rule("invalid_time_zone", "Bitte wähle eine gültige IANA-Zeitzone.");
        }
        catch (InvalidTimeZoneException)
        {
            throw Rule("invalid_time_zone", "Bitte wähle eine gültige IANA-Zeitzone.");
        }
    }

    private static void RequireActive(CampRecord camp)
    {
        if (camp.Status == CampStatus.Archived)
        {
            throw Rule("camp_archived", "Archivierte Camps sind schreibgeschützt.");
        }
    }

    private static CampsRuleException Rule(string code, string message) => new(code, message);

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();

    private sealed record CampValues(
        string Name,
        string Slug,
        string? Description,
        TimeZoneInfo TimeZone);
}
