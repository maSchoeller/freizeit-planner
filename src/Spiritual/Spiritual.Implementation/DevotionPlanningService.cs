using Identity.Contracts;
using Spiritual.Contracts;

namespace Spiritual.Implementation;

public sealed class DevotionPlanningService(
    IDevotionState state,
    ITenantAccessControl accessControl,
    IBiblePassageProvider bibleProvider,
    TimeProvider timeProvider) : IDevotionPlanning
{
    public async Task<IReadOnlyList<DevotionSummary>> ListAsync(
        DevotionScope scope,
        CancellationToken cancellationToken)
    {
        await RequireAccessAsync(scope.ActorId, scope.OrganizationId, scope.CampId, CampAction.Read, cancellationToken);
        return (await state.ListAsync(
                scope.OrganizationId,
                scope.CampId,
                false,
                cancellationToken))
            .Select(ToSummary)
            .ToArray();
    }

    public async Task<DevotionDetails?> GetAsync(
        DevotionKey key,
        CancellationToken cancellationToken)
    {
        await RequireAccessAsync(key.ActorId, key.OrganizationId, key.CampId, CampAction.Read, cancellationToken);
        var devotion = await state.FindAsync(
            key.OrganizationId,
            key.CampId,
            key.DevotionId,
            cancellationToken);
        return devotion is { DeletedAt: null } ? ToDetails(devotion) : null;
    }

    public async Task<DevotionDetails> CreateAsync(
        CreateDevotion command,
        CancellationToken cancellationToken)
    {
        await RequireAccessAsync(
            command.ActorId,
            command.OrganizationId,
            command.CampId,
            CampAction.WriteContent,
            cancellationToken);
        var devotion = new DevotionRecord(
            Guid.NewGuid(),
            command.OrganizationId,
            command.CampId,
            command.Topic,
            command.BibleReference,
            command.Translation,
            command.CoreMessage,
            command.MarkdownContent,
            command.ResponsibleUserIds,
            command.MaterialNotes,
            command.ScheduleEntryId,
            timeProvider.GetUtcNow());
        await state.AddAsync(devotion, cancellationToken);
        return ToDetails(devotion);
    }

    public async Task<DevotionDetails> UpdateAsync(
        UpdateDevotion command,
        CancellationToken cancellationToken)
    {
        await RequireAccessAsync(
            command.ActorId,
            command.OrganizationId,
            command.CampId,
            CampAction.WriteContent,
            cancellationToken);
        var devotion = await RequireDevotionAsync(
            command.OrganizationId,
            command.CampId,
            command.DevotionId,
            cancellationToken);
        devotion.Update(command, timeProvider.GetUtcNow());
        await state.SaveAsync(devotion, cancellationToken);
        return ToDetails(devotion);
    }

    public async Task MoveToTrashAsync(
        ChangeDevotionLifecycle command,
        CancellationToken cancellationToken)
    {
        var devotion = await RequireWritableDevotionAsync(command, cancellationToken);
        devotion.MoveToTrash(command.ExpectedVersion, timeProvider.GetUtcNow());
        await state.SaveAsync(devotion, cancellationToken);
    }

    public async Task RestoreAsync(
        ChangeDevotionLifecycle command,
        CancellationToken cancellationToken)
    {
        var devotion = await RequireWritableDevotionAsync(command, cancellationToken);
        devotion.Restore(command.ExpectedVersion, timeProvider.GetUtcNow());
        await state.SaveAsync(devotion, cancellationToken);
    }

    public async Task<BibleSnapshotRefreshResult> RefreshBibleSnapshotAsync(
        RefreshBibleSnapshot command,
        CancellationToken cancellationToken)
    {
        await RequireAccessAsync(
            command.ActorId,
            command.OrganizationId,
            command.CampId,
            CampAction.WriteContent,
            cancellationToken);
        var devotion = await RequireDevotionAsync(
            command.OrganizationId,
            command.CampId,
            command.DevotionId,
            cancellationToken);
        devotion.EnsureVersion(command.ExpectedVersion);

        var fetched = await bibleProvider.FetchAsync(
            new BiblePassageRequest(devotion.Translation, devotion.BibleReference),
            cancellationToken);
        if (fetched is { Status: BiblePassageFetchStatus.Found, Passage: { } passage })
        {
            var now = timeProvider.GetUtcNow();
            devotion.ReplaceBibleSnapshot(
                new BibleSnapshot(
                    passage.Reference,
                    passage.TextExcerpt,
                    passage.TechnicalTranslationId,
                    passage.TranslationDisplayName,
                    passage.License,
                    passage.Attribution,
                    passage.RetrievedAt,
                    BibleSnapshotOrigin.Provider),
                command.ExpectedVersion,
                now);
            await state.SaveAsync(devotion, cancellationToken);
            return new BibleSnapshotRefreshResult(
                BibleSnapshotRefreshStatus.Refreshed,
                ToDetails(devotion));
        }

        var status = fetched.Status switch
        {
            BiblePassageFetchStatus.ReferenceNotFound => BibleSnapshotRefreshStatus.ReferenceNotFound,
            BiblePassageFetchStatus.TimedOut => BibleSnapshotRefreshStatus.TimedOut,
            _ => BibleSnapshotRefreshStatus.ProviderUnavailable
        };
        return new BibleSnapshotRefreshResult(status, ToDetails(devotion));
    }

    public async Task<DevotionDetails> SaveManualBibleSnapshotAsync(
        SaveManualBibleSnapshot command,
        CancellationToken cancellationToken)
    {
        await RequireAccessAsync(
            command.ActorId,
            command.OrganizationId,
            command.CampId,
            CampAction.WriteContent,
            cancellationToken);
        var devotion = await RequireDevotionAsync(
            command.OrganizationId,
            command.CampId,
            command.DevotionId,
            cancellationToken);
        var reference = Required(
            command.Reference,
            "bible_reference_required",
            "Bitte gib eine Bibelstelle ein.");
        var text = Required(
            command.TextExcerpt,
            "bible_text_required",
            "Bitte gib einen Bibeltext ein.");
        var translation = BibleTranslationCatalog.Get(command.Translation);
        var now = timeProvider.GetUtcNow();
        devotion.ReplaceBibleSnapshot(
            new BibleSnapshot(
                reference,
                text,
                translation.TechnicalId,
                translation.DisplayName,
                translation.License,
                translation.Attribution,
                now,
                BibleSnapshotOrigin.Manual),
            command.ExpectedVersion,
            now);
        await state.SaveAsync(devotion, cancellationToken);
        return ToDetails(devotion);
    }

    public Task<IReadOnlyList<BibleTranslationView>> ListBibleTranslationsAsync(
        CancellationToken cancellationToken) =>
        Task.FromResult(BibleTranslationCatalog.Views);

    private async Task<DevotionRecord> RequireWritableDevotionAsync(
        ChangeDevotionLifecycle command,
        CancellationToken cancellationToken)
    {
        await RequireAccessAsync(
            command.ActorId,
            command.OrganizationId,
            command.CampId,
            CampAction.WriteContent,
            cancellationToken);
        return await RequireDevotionAsync(
            command.OrganizationId,
            command.CampId,
            command.DevotionId,
            cancellationToken);
    }

    private async Task<DevotionRecord> RequireDevotionAsync(
        Guid organizationId,
        Guid campId,
        Guid devotionId,
        CancellationToken cancellationToken) =>
        await state.FindAsync(organizationId, campId, devotionId, cancellationToken)
        ?? throw Rule("devotion_not_found", "Die Andacht wurde nicht gefunden.");

    private async Task RequireAccessAsync(
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
            throw Rule("camp_access_denied", "Du darfst auf diese Andacht nicht zugreifen.");
        }
    }

    private static DevotionSummary ToSummary(DevotionRecord devotion) => new(
        devotion.Id,
        devotion.OrganizationId,
        devotion.CampId,
        devotion.Topic,
        devotion.BibleReference,
        devotion.Translation,
        Array.AsReadOnly(devotion.ResponsibleUserIds.ToArray()),
        devotion.ScheduleEntryId,
        devotion.BibleSnapshot is not null,
        devotion.Version);

    private static DevotionDetails ToDetails(DevotionRecord devotion) => new(
        devotion.Id,
        devotion.OrganizationId,
        devotion.CampId,
        devotion.Topic,
        devotion.BibleReference,
        devotion.Translation,
        devotion.CoreMessage,
        devotion.MarkdownContent,
        Array.AsReadOnly(devotion.ResponsibleUserIds.ToArray()),
        devotion.MaterialNotes,
        devotion.ScheduleEntryId,
        devotion.BibleSnapshot,
        devotion.CreatedAt,
        devotion.UpdatedAt,
        devotion.DeletedAt,
        devotion.Version);

    private static string Required(string value, string code, string message)
    {
        var trimmed = value.Trim();
        return trimmed.Length > 0 ? trimmed : throw Rule(code, message);
    }

    private static SpiritualRuleException Rule(string code, string message) => new(code, message);
}
