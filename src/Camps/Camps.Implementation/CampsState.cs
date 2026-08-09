namespace Camps.Implementation;

public interface ICampsState
{
    ValueTask<IReadOnlyList<CampRecord>> ListCampsAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    ValueTask<CampRecord?> FindCampAsync(
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken);

    ValueTask<CampRecord?> FindCampBySlugAsync(
        Guid organizationId,
        string slug,
        CancellationToken cancellationToken);

    ValueTask AddCampAsync(CampRecord camp, CancellationToken cancellationToken);

    ValueTask SaveCampAsync(
        CampRecord camp,
        long expectedVersion,
        CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<ScheduleEntryRecord>> ListScheduleEntriesAsync(
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken);

    ValueTask<ScheduleEntryRecord?> FindScheduleEntryAsync(
        Guid organizationId,
        Guid campId,
        Guid scheduleEntryId,
        CancellationToken cancellationToken);

    ValueTask AddScheduleEntryAsync(
        ScheduleEntryRecord scheduleEntry,
        CancellationToken cancellationToken);

    ValueTask SaveScheduleEntryAsync(
        ScheduleEntryRecord scheduleEntry,
        long expectedVersion,
        CancellationToken cancellationToken);

    ValueTask DeleteScheduleEntryAsync(
        ScheduleEntryRecord scheduleEntry,
        long expectedVersion,
        CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<ScheduleEntryRecord>> ListDeletedScheduleEntriesAsync(
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken);

    ValueTask<ScheduleEntryRecord?> FindDeletedScheduleEntryAsync(
        Guid organizationId,
        Guid campId,
        Guid scheduleEntryId,
        CancellationToken cancellationToken);

    ValueTask<int> PurgeDueScheduleEntriesAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken);
}
