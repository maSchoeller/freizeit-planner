using Camps.Contracts;
using Identity.Contracts;

namespace Camps.Implementation;

public sealed class SchedulePlanningService(
    ICampsState state,
    CampPlanningService camps,
    ITenantAccessControl accessControl,
    TimeProvider timeProvider) : ISchedulePlanning, IScheduleReferenceAccess
{
    public async Task<IReadOnlyList<ScheduleEntryView>> ListAsync(
        ScheduleRangeQuery query,
        CancellationToken cancellationToken)
    {
        if (query.ToDateExclusive <= query.FromDate)
        {
            throw Rule("invalid_schedule_range", "Der Anzeigezeitraum ist ungültig.");
        }
        var camp = await camps.RequireReadableCampAsync(
            query.ActorId,
            query.OrganizationId,
            query.CampId,
            cancellationToken);
        var entries = await state.ListScheduleEntriesAsync(
            query.OrganizationId,
            query.CampId,
            cancellationToken);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(camp.TimeZoneId);
        var visible = entries
            .Where(item => Overlaps(
                GetLocalRange(item, timeZone),
                (query.FromDate.ToDateTime(TimeOnly.MinValue),
                    query.ToDateExclusive.ToDateTime(TimeOnly.MinValue))))
            .ToArray();
        return visible
            .OrderBy(item => GetLocalRange(item, timeZone).Start)
            .ThenBy(item => item.Title, StringComparer.CurrentCulture)
            .Select(item => ToView(
                item,
                camp.TimeZoneId,
                entries.Any(other => other.Id != item.Id && Overlaps(
                    GetLocalRange(item, timeZone),
                    GetLocalRange(other, timeZone)))))
            .ToArray();
    }

    public async Task<ScheduleEntryView> GetAsync(
        ScheduleEntryQuery query,
        CancellationToken cancellationToken)
    {
        var camp = await camps.RequireReadableCampAsync(
            query.ActorId,
            query.OrganizationId,
            query.CampId,
            cancellationToken);
        var entry = await RequireEntryAsync(
            query.OrganizationId,
            query.CampId,
            query.ScheduleEntryId,
            cancellationToken);
        var all = await state.ListScheduleEntriesAsync(
            query.OrganizationId,
            query.CampId,
            cancellationToken);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(camp.TimeZoneId);
        var overlaps = all.Any(other => other.Id != entry.Id && Overlaps(
            GetLocalRange(entry, timeZone),
            GetLocalRange(other, timeZone)));
        return ToView(entry, camp.TimeZoneId, overlaps);
    }

    public async Task<ScheduleEntryView> CreateAsync(
        CreateScheduleEntry command,
        CancellationToken cancellationToken)
    {
        var camp = await camps.RequireWritableCampAsync(
            command.ActorId,
            command.OrganizationId,
            command.CampId,
            CampAction.WriteContent,
            cancellationToken);
        var values = ValidateContent(
            command.Title,
            command.Description,
            command.Location,
            command.Category,
            command.ResponsibleUserIds,
            command.Audience);
        await RequireResponsibilitiesAsync(
            command.OrganizationId,
            command.CampId,
            values.ResponsibleUserIds,
            cancellationToken);
        var timing = ResolveTiming(command.Timing, camp.TimeZoneId);
        var entry = new ScheduleEntryRecord(
            Guid.NewGuid(),
            command.OrganizationId,
            command.CampId,
            timing,
            values.Title,
            values.Description,
            values.Location,
            values.Category,
            command.Status,
            values.ResponsibleUserIds,
            values.Audience);
        await state.AddScheduleEntryAsync(entry, cancellationToken);
        var all = await state.ListScheduleEntriesAsync(
            command.OrganizationId,
            command.CampId,
            cancellationToken);
        return ToView(entry, camp.TimeZoneId, HasOverlap(entry, all, camp.TimeZoneId));
    }

    public async Task<ScheduleEntryView> UpdateAsync(
        UpdateScheduleEntry command,
        CancellationToken cancellationToken)
    {
        var camp = await camps.RequireWritableCampAsync(
            command.ActorId,
            command.OrganizationId,
            command.CampId,
            CampAction.WriteContent,
            cancellationToken);
        var entry = await RequireEntryAsync(
            command.OrganizationId,
            command.CampId,
            command.ScheduleEntryId,
            cancellationToken);
        var values = ValidateContent(
            command.Title,
            command.Description,
            command.Location,
            command.Category,
            command.ResponsibleUserIds,
            command.Audience);
        await RequireResponsibilitiesAsync(
            command.OrganizationId,
            command.CampId,
            values.ResponsibleUserIds,
            cancellationToken);
        var timing = ResolveTiming(command.Timing, camp.TimeZoneId);
        entry.Update(
            timing,
            values.Title,
            values.Description,
            values.Location,
            values.Category,
            command.Status,
            values.ResponsibleUserIds,
            values.Audience,
            command.ExpectedVersion);
        await state.SaveScheduleEntryAsync(entry, command.ExpectedVersion, cancellationToken);
        var all = await state.ListScheduleEntriesAsync(
            command.OrganizationId,
            command.CampId,
            cancellationToken);
        return ToView(entry, camp.TimeZoneId, HasOverlap(entry, all, camp.TimeZoneId));
    }

    public async Task<ScheduleEntryReference> DeleteAsync(
        DeleteScheduleEntry command,
        CancellationToken cancellationToken)
    {
        _ = await camps.RequireWritableCampAsync(
            command.ActorId,
            command.OrganizationId,
            command.CampId,
            CampAction.WriteContent,
            cancellationToken);
        var entry = await RequireEntryAsync(
            command.OrganizationId,
            command.CampId,
            command.ScheduleEntryId,
            cancellationToken);
        entry.MoveToTrash(command.ExpectedVersion, timeProvider.GetUtcNow());
        await state.DeleteScheduleEntryAsync(entry, command.ExpectedVersion, cancellationToken);
        return ToReference(entry);
    }

    public async Task<IReadOnlyList<TrashedScheduleEntry>> ListTrashAsync(
        ScheduleTrashQuery query,
        CancellationToken cancellationToken)
    {
        await RequireManagerAsync(query.ActorId, query.OrganizationId, query.CampId, cancellationToken);
        return (await state.ListDeletedScheduleEntriesAsync(
                query.OrganizationId,
                query.CampId,
                cancellationToken))
            .OrderByDescending(item => item.DeletedAt)
            .Select(item => new TrashedScheduleEntry(
                item.Id,
                item.OrganizationId,
                item.CampId,
                item.Title,
                item.DeletedAt!.Value,
                item.PurgeAt!.Value,
                item.Version))
            .ToArray();
    }

    public async Task<ScheduleEntryView> RestoreAsync(
        RestoreScheduleEntry command,
        CancellationToken cancellationToken)
    {
        var camp = await camps.RequireWritableCampAsync(
            command.ActorId,
            command.OrganizationId,
            command.CampId,
            CampAction.ManageCamp,
            cancellationToken);
        var entry = await state.FindDeletedScheduleEntryAsync(
            command.OrganizationId,
            command.CampId,
            command.ScheduleEntryId,
            cancellationToken)
            ?? throw Rule("schedule_entry_not_found", "Der Zeitplaneintrag wurde nicht gefunden.");
        entry.Restore(command.ExpectedVersion, timeProvider.GetUtcNow());
        await state.DeleteScheduleEntryAsync(entry, command.ExpectedVersion, cancellationToken);
        var all = await state.ListScheduleEntriesAsync(command.OrganizationId, command.CampId, cancellationToken);
        return ToView(entry, camp.TimeZoneId, HasOverlap(entry, all, camp.TimeZoneId));
    }

    public async Task<ScheduleEntryReference> RequireAsync(
        ScheduleEntryReferenceRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Purpose == ScheduleReferencePurpose.LinkForWrite)
        {
            _ = await camps.RequireWritableCampAsync(
                request.ActorId,
                request.OrganizationId,
                request.CampId,
                CampAction.WriteContent,
                cancellationToken);
        }
        else
        {
            _ = await camps.RequireReadableCampAsync(
                request.ActorId,
                request.OrganizationId,
                request.CampId,
                cancellationToken);
        }
        var entry = await RequireEntryAsync(
            request.OrganizationId,
            request.CampId,
            request.ScheduleEntryId,
            cancellationToken);
        return ToReference(entry);
    }

    private async Task<ScheduleEntryRecord> RequireEntryAsync(
        Guid organizationId,
        Guid campId,
        Guid scheduleEntryId,
        CancellationToken cancellationToken) =>
        await state.FindScheduleEntryAsync(
            organizationId,
            campId,
            scheduleEntryId,
            cancellationToken)
        ?? throw Rule("schedule_entry_not_found", "Der Zeitplaneintrag wurde nicht gefunden.");

    private async Task RequireResponsibilitiesAsync(
        Guid organizationId,
        Guid campId,
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken)
    {
        foreach (var userId in userIds)
        {
            var decision = await accessControl.AuthorizeCampAsync(
                new CampAccessRequest(userId, organizationId, campId, CampAction.Read),
                cancellationToken);
            if (!decision.Allowed)
            {
                throw Rule(
                    "invalid_responsibility",
                    "Mindestens eine verantwortliche Person hat keinen Zugriff auf dieses Camp.");
            }
        }
    }

    private async Task RequireManagerAsync(
        Guid actorId,
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken)
    {
        var decision = await accessControl.AuthorizeCampAsync(
            new CampAccessRequest(actorId, organizationId, campId, CampAction.ManageCamp),
            cancellationToken);
        if (!decision.Allowed)
        {
            throw Rule("camp_access_denied", "Du darfst den Papierkorb dieses Camps nicht verwalten.");
        }
    }

    private static ScheduleTimingRecord ResolveTiming(
        ScheduleTimingInput input,
        string timeZoneId)
    {
        if (input.IsAllDay)
        {
            if (input.LocalStart is not null
                || input.LocalEnd is not null
                || input.StartDate is not { } startDate
                || input.EndDateExclusive is not { } endDate
                || endDate <= startDate)
            {
                throw Rule("invalid_schedule_time", "Der ganztägige Zeitraum ist ungültig.");
            }
            return new ScheduleTimingRecord(true, null, null, startDate, endDate);
        }

        if (input.StartDate is not null
            || input.EndDateExclusive is not null
            || input.LocalStart is not { } localStart
            || input.LocalEnd is not { } localEnd
            || localStart.Kind != DateTimeKind.Unspecified
            || localEnd.Kind != DateTimeKind.Unspecified
            || localEnd <= localStart)
        {
            throw Rule("invalid_schedule_time", "Der Uhrzeitraum ist ungültig.");
        }
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var startsAtUtc = ResolveLocal(localStart, input.StartChoice, timeZone);
        var endsAtUtc = ResolveLocal(localEnd, input.EndChoice, timeZone);
        if (endsAtUtc <= startsAtUtc)
        {
            throw Rule("invalid_schedule_time", "Das Ende muss nach dem Beginn liegen.");
        }
        return new ScheduleTimingRecord(false, startsAtUtc, endsAtUtc, null, null);
    }

    private static DateTimeOffset ResolveLocal(
        DateTime local,
        AmbiguousLocalTimeChoice choice,
        TimeZoneInfo timeZone)
    {
        if (timeZone.IsInvalidTime(local))
        {
            throw Rule(
                "local_time_nonexistent",
                "Diese lokale Uhrzeit existiert wegen der Zeitumstellung nicht.");
        }
        if (timeZone.IsAmbiguousTime(local))
        {
            if (choice == AmbiguousLocalTimeChoice.Reject)
            {
                throw Rule(
                    "local_time_ambiguous",
                    "Diese lokale Uhrzeit kommt zweimal vor. Bitte wähle die frühere oder spätere Variante.");
            }
            var offsets = timeZone.GetAmbiguousTimeOffsets(local);
            var offset = choice == AmbiguousLocalTimeChoice.EarlierOffset
                ? offsets.Max()
                : offsets.Min();
            return new DateTimeOffset(local, offset).ToUniversalTime();
        }
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timeZone), TimeSpan.Zero);
    }

    private static ContentValues ValidateContent(
        string title,
        string? description,
        string? location,
        string category,
        IReadOnlyList<Guid> responsibleUserIds,
        string? audience)
    {
        var normalizedTitle = NormalizeRequired(title, 200, "invalid_schedule_title", "Der Titel ist ungültig.");
        var normalizedCategory = NormalizeRequired(
            category,
            80,
            "invalid_schedule_category",
            "Die Kategorie ist ungültig.");
        var normalizedDescription = NormalizeOptional(description, 8000, "Die Beschreibung ist zu lang.");
        var normalizedLocation = NormalizeOptional(location, 240, "Der Ort ist zu lang.");
        var normalizedAudience = NormalizeOptional(audience, 160, "Die Zielgruppe ist zu lang.");
        var responsibilities = responsibleUserIds
            .Where(item => item != Guid.Empty)
            .Distinct()
            .ToArray();
        if (responsibilities.Length == 0 || responsibilities.Length != responsibleUserIds.Count)
        {
            throw Rule(
                "invalid_responsibility",
                "Wähle mindestens eine eindeutige verantwortliche Person.");
        }
        return new ContentValues(
            normalizedTitle,
            normalizedDescription,
            normalizedLocation,
            normalizedCategory,
            responsibilities,
            normalizedAudience);
    }

    private static string NormalizeRequired(
        string value,
        int maximumLength,
        string errorCode,
        string message)
    {
        var normalized = value.Trim();
        if (normalized.Length is 0 || normalized.Length > maximumLength)
        {
            throw Rule(errorCode, message);
        }
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maximumLength, string message)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        return normalized.Length <= maximumLength
            ? normalized
            : throw Rule("invalid_schedule_content", message);
    }

    private static bool HasOverlap(
        ScheduleEntryRecord entry,
        IReadOnlyList<ScheduleEntryRecord> entries,
        string timeZoneId)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var range = GetLocalRange(entry, timeZone);
        return entries.Any(other => other.Id != entry.Id && Overlaps(range, GetLocalRange(other, timeZone)));
    }

    private static (DateTime Start, DateTime End) GetLocalRange(
        ScheduleEntryRecord entry,
        TimeZoneInfo timeZone)
    {
        if (entry.Timing.IsAllDay)
        {
            return (
                entry.Timing.StartDate!.Value.ToDateTime(TimeOnly.MinValue),
                entry.Timing.EndDateExclusive!.Value.ToDateTime(TimeOnly.MinValue));
        }
        return (
            TimeZoneInfo.ConvertTime(entry.Timing.StartsAtUtc!.Value, timeZone).DateTime,
            TimeZoneInfo.ConvertTime(entry.Timing.EndsAtUtc!.Value, timeZone).DateTime);
    }

    private static bool Overlaps(
        (DateTime Start, DateTime End) left,
        (DateTime Start, DateTime End) right) =>
        left.Start < right.End && right.Start < left.End;

    private static ScheduleEntryView ToView(
        ScheduleEntryRecord entry,
        string timeZoneId,
        bool overlaps) => new(
        entry.Id,
        entry.OrganizationId,
        entry.CampId,
        new ScheduleTimingView(
            entry.Timing.IsAllDay,
            entry.Timing.StartsAtUtc,
            entry.Timing.EndsAtUtc,
            entry.Timing.StartDate,
            entry.Timing.EndDateExclusive,
            timeZoneId),
        entry.Title,
        entry.Description,
        entry.Location,
        entry.Category,
        entry.Status,
        entry.ResponsibleUserIds,
        entry.Audience,
        overlaps,
        entry.Version);

    private static ScheduleEntryReference ToReference(ScheduleEntryRecord entry) => new(
        entry.OrganizationId,
        entry.CampId,
        entry.Id,
        entry.Version);

    private static CampsRuleException Rule(string code, string message) => new(code, message);

    private sealed record ContentValues(
        string Title,
        string? Description,
        string? Location,
        string Category,
        IReadOnlyList<Guid> ResponsibleUserIds,
        string? Audience);
}
