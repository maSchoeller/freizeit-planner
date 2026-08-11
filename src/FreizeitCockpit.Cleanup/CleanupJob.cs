using Files.Contracts;
using Camps.Contracts;
using Catering.Contracts;
using Identity.Contracts;
using Knowledge.Contracts;
using Logistics.Contracts;
using Microsoft.Extensions.Logging;
using Spiritual.Contracts;

namespace FreizeitCockpit.Cleanup;

public sealed class CleanupOptions
{
    public int BatchSize { get; init; } = 100;

    public IReadOnlyList<string> RequiredErasureAreas { get; init; } =
        ["activity", "camps", "catering", "files", "knowledge", "logistics", "spiritual"];
}

public sealed record CleanupResult(
    IdentityCleanupResult Identity,
    NotePurgeResult Notes,
    AttachmentPurgeResult Attachments,
    DevotionPurgeResult Devotions,
    LogisticsRetentionResult Logistics,
    ScheduleRetentionResult Schedule,
    MealRetentionResult Meals,
    int ExpiredAttachmentReadGrants);

public sealed class CleanupRetryableException(int retryableFailures)
    : InvalidOperationException("At least one blob could not be deleted.")
{
    public int RetryableFailures { get; } = retryableFailures;
}

public sealed class CleanupJob(
    IIdentityMaintenance identityMaintenance,
    INotebookRetention notebookRetention,
    IAttachmentMaintenance attachmentMaintenance,
    IDevotionRetention devotionRetention,
    ILogisticsRetention logisticsRetention,
    IScheduleRetention scheduleRetention,
    IMealRetention mealRetention,
    IEnumerable<IDataErasure> dataErasures,
    ILogger<CleanupJob> logger,
    CleanupOptions options)
{
    private static readonly Guid PseudonymousUserId = Guid.Empty;

    private static readonly Action<ILogger, int, int, int, int, int, int, Exception?> LogCompleted =
        LoggerMessage.Define<int, int, int, int, int, int>(
            LogLevel.Information,
            new EventId(1001, "CleanupCompleted"),
            "Cleanup completed: {IdentityItems} identity items, {Notes} notes, "
                + "{AttachmentMetadata} attachment records, {AttachmentBlobs} blobs, "
                + "{ReadGrants} read grants, {Devotions} devotions.");

    private static readonly Action<ILogger, int, int, int, Exception?> LogLogisticsCompleted =
        LoggerMessage.Define<int, int, int>(
            LogLevel.Information,
            new EventId(1002, "LogisticsCleanupCompleted"),
            "Logistics cleanup completed: {Materials} material requirements, {ShoppingLists} shopping lists, "
                + "{ShoppingItems} shopping items.");

    private static readonly Action<ILogger, int, Exception?> LogScheduleCompleted =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(1003, "ScheduleCleanupCompleted"),
            "Schedule cleanup completed: {ScheduleEntries} schedule entries.");

    private static readonly Action<ILogger, int, Exception?> LogMealsCompleted =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(1004, "MealCleanupCompleted"),
            "Meal cleanup completed: {Meals} meals.");

    public async Task<CleanupResult> RunAsync(CancellationToken cancellationToken)
    {
        if (options.BatchSize is < 1 or > 500)
        {
            throw new InvalidOperationException("Cleanup:BatchSize must be between 1 and 500.");
        }

        var identity = await identityMaintenance.CleanupExpiredAsync(
            options.BatchSize,
            cancellationToken);
        var notes = await notebookRetention.PurgeExpiredNotesAsync(
            options.BatchSize,
            cancellationToken);
        var expiredReadGrants = await attachmentMaintenance.DeleteExpiredReadGrantsAsync(
            options.BatchSize,
            cancellationToken);
        var attachments = await attachmentMaintenance.PurgeDueAsync(
            options.BatchSize,
            cancellationToken);
        var devotions = await devotionRetention.PurgeExpiredDevotionsAsync(
            options.BatchSize,
            cancellationToken);
        var logistics = await logisticsRetention.PurgeExpiredAsync(
            options.BatchSize,
            cancellationToken);
        var schedule = await scheduleRetention.PurgeExpiredAsync(
            options.BatchSize,
            cancellationToken);
        var meals = await mealRetention.PurgeExpiredAsync(options.BatchSize, cancellationToken);
        var erasureFailures = await EraseDueDataAsync(cancellationToken);

        LogCompleted(
            logger,
            identity.ExpiredEmailChangeChallenges
                + identity.ExpiredInvitations
                + identity.StaleSessions
                + identity.StaleRateEvents,
            notes.PurgedNotes,
            attachments.MetadataPurged,
            attachments.BlobsDeleted,
            expiredReadGrants,
            devotions.PurgedDevotions,
            null);
        LogLogisticsCompleted(
            logger,
            logistics.PurgedMaterials,
            logistics.PurgedShoppingLists,
            logistics.PurgedShoppingItems,
            null);
        LogScheduleCompleted(logger, schedule.PurgedScheduleEntries, null);
        LogMealsCompleted(logger, meals.PurgedMeals, null);

        var retryableFailures = attachments.RetryableFailures + erasureFailures;
        if (retryableFailures > 0)
        {
            throw new CleanupRetryableException(retryableFailures);
        }

        return new CleanupResult(identity, notes, attachments, devotions, logistics, schedule, meals,
            expiredReadGrants);
    }

    private async Task<int> EraseDueDataAsync(CancellationToken cancellationToken)
    {
        var areas = dataErasures.OrderBy(item => item.Area, StringComparer.Ordinal).ToArray();
        var actualAreas = areas.Select(item => item.Area).ToArray();
        var requiredAreas = options.RequiredErasureAreas
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        if (!actualAreas.SequenceEqual(requiredAreas, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "The configured data-erasure areas do not match the required module set.");
        }

        var candidates = await identityMaintenance.ClaimDueErasuresAsync(
            options.BatchSize,
            cancellationToken);
        var failures = 0;

        foreach (var organizationId in candidates.OrganizationIds)
        {
            var complete = true;
            foreach (var area in areas)
            {
                var result = await area.EraseOrganizationAsync(
                    organizationId,
                    options.BatchSize,
                    cancellationToken);
                failures += result.RetryableFailures;
                complete &= !result.HasRemaining && result.RetryableFailures == 0;
            }

            if (complete)
            {
                await identityMaintenance.CompleteOrganizationErasureAsync(
                    organizationId,
                    cancellationToken);
            }
        }

        foreach (var userId in candidates.UserIds)
        {
            var complete = true;
            foreach (var area in areas)
            {
                var result = await area.PseudonymizeUserAsync(
                    userId,
                    PseudonymousUserId,
                    options.BatchSize,
                    cancellationToken);
                failures += result.RetryableFailures;
                complete &= !result.HasRemaining && result.RetryableFailures == 0;
            }

            if (complete)
            {
                await identityMaintenance.CompleteAccountErasureAsync(userId, cancellationToken);
            }
        }

        return failures;
    }
}
