using System.Security.Cryptography;
using System.Text;
using Files.Contracts;
using Identity.Contracts;

namespace Files.Implementation;

public sealed class AttachmentService(
    IAttachmentState state,
    IAttachmentOwnerAuthorization ownerAuthorization,
    ITenantAccessControl tenantAccessControl,
    IPrivateBlobStorage blobStorage,
    TimeProvider timeProvider) : IAttachmentCatalog, IAttachmentReader, IAttachmentMaintenance
{
    public const long ScopeQuotaBytes = 100L * 1024 * 1024;

    private static readonly TimeSpan ReadGrantLifetime = TimeSpan.FromSeconds(60);

    public async Task<IReadOnlyList<AttachmentView>> ListAsync(
        AttachmentOwnerQuery query,
        CancellationToken cancellationToken)
    {
        _ = await RequireOwnerAccessAsync(
            query.ActorId,
            query.OrganizationId,
            query.CampId,
            query.Owner,
            AttachmentOwnerAction.Read,
            cancellationToken);
        return (await state.ListAsync(
                query.OrganizationId,
                query.CampId,
                query.Owner,
                query.IncludeDeleted,
                cancellationToken))
            .Select(ToView)
            .ToArray();
    }

    public async Task<IReadOnlyList<AttachmentView>> ListTrashAsync(
        AttachmentTrashQuery query,
        CancellationToken cancellationToken)
    {
        await RequireCampManagementAsync(
            query.ActorId,
            query.OrganizationId,
            query.CampId,
            cancellationToken);

        return (await state.ListTrashAsync(
                query.OrganizationId,
                query.CampId,
                cancellationToken))
            .Select(ToView)
            .ToArray();
    }

    public async Task<AttachmentView> UploadAsync(
        UploadAttachment command,
        Stream content,
        CancellationToken cancellationToken)
    {
        var scope = await RequireOwnerAccessAsync(
            command.ActorId,
            command.OrganizationId,
            command.CampId,
            command.Owner,
            AttachmentOwnerAction.AddAttachment,
            cancellationToken);
        var upload = await AttachmentUploadValidator.ValidateAsync(command, content, cancellationToken);
        var blobName = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var attachment = new AttachmentRecord(
            Guid.NewGuid(),
            scope.OrganizationId,
            scope.CampId,
            command.Owner,
            scope.QuotaScope,
            blobName,
            upload.FileName,
            upload.MediaType,
            upload.ContentType,
            upload.Content.LongLength,
            command.ActorId,
            timeProvider.GetUtcNow());
        if (!await state.TryReserveAsync(attachment, ScopeQuotaBytes, cancellationToken))
        {
            throw Rule("attachment_quota_exceeded", "Das Speicherlimit von 100 MiB ist für diesen Bereich erreicht.");
        }

        try
        {
            await using var uploadStream = new MemoryStream(upload.Content, writable: false);
            await blobStorage.StoreAsync(
                new PrivateBlobWrite(blobName, upload.ContentType, upload.Content.LongLength),
                uploadStream,
                cancellationToken);
            attachment.MarkAvailable();
            await state.MarkAvailableAsync(attachment, cancellationToken);
            return ToView(attachment);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await CompensateFailedUploadAsync(attachment);
            throw;
        }
        catch (Exception exception)
        {
            await CompensateFailedUploadAsync(attachment);
            throw new FilesRuleException(
                "attachment_storage_unavailable",
                "Der Anhang konnte nicht gespeichert werden. Bitte versuche es erneut.",
                exception);
        }
    }

    public async Task<AttachmentView> MoveToTrashAsync(
        ChangeAttachmentLifecycle command,
        CancellationToken cancellationToken)
    {
        var attachment = await RequireAttachmentAsync(command, cancellationToken);
        _ = await RequireOwnerAccessAsync(
            command.ActorId,
            command.OrganizationId,
            command.CampId,
            attachment.Owner,
            AttachmentOwnerAction.RemoveAttachment,
            cancellationToken);
        attachment.MoveToTrash(command.ExpectedVersion, timeProvider.GetUtcNow());
        await state.SaveAsync(attachment, cancellationToken);
        await state.RevokeReadGrantsAsync(attachment.Id, cancellationToken);
        return ToView(attachment);
    }

    public async Task<AttachmentView> RestoreAsync(
        ChangeAttachmentLifecycle command,
        CancellationToken cancellationToken)
    {
        if (command.CampId is not { } campId)
        {
            throw Rule(
                "attachment_restore_scope_invalid",
                "Anhänge der Rezeptbibliothek können nicht über den Camp-Papierkorb wiederhergestellt werden.");
        }
        await RequireCampManagementAsync(
            command.ActorId,
            command.OrganizationId,
            campId,
            cancellationToken);
        var attachment = await RequireAttachmentAsync(command, cancellationToken);
        _ = await RequireOwnerAccessAsync(
            command.ActorId,
            command.OrganizationId,
            command.CampId,
            attachment.Owner,
            AttachmentOwnerAction.RestoreAttachment,
            cancellationToken);
        attachment.Restore(command.ExpectedVersion, timeProvider.GetUtcNow());
        await state.SaveAsync(attachment, cancellationToken);
        return ToView(attachment);
    }

    public async Task<AttachmentQuotaView> GetQuotaAsync(
        AttachmentQuotaQuery query,
        CancellationToken cancellationToken)
    {
        await RequireQuotaAccessAsync(query, cancellationToken);
        EnsureQuotaScope(query.Scope, query.CampId);
        var usage = await state.GetQuotaUsageAsync(
            query.OrganizationId,
            query.CampId,
            query.Scope,
            cancellationToken);
        return new AttachmentQuotaView(
            query.Scope,
            ScopeQuotaBytes,
            usage.UsedBytes,
            usage.PendingBytes,
            Math.Max(0, ScopeQuotaBytes - usage.UsedBytes));
    }

    public async Task<AttachmentReadGrant> IssueReadGrantAsync(
        AttachmentReadGrantRequest request,
        CancellationToken cancellationToken)
    {
        var attachment = await state.FindAsync(
            request.OrganizationId,
            request.CampId,
            request.AttachmentId,
            cancellationToken)
            ?? throw NotFound();
        EnsureAvailable(attachment);
        _ = await RequireOwnerAccessAsync(
            request.ActorId,
            request.OrganizationId,
            request.CampId,
            attachment.Owner,
            AttachmentOwnerAction.Read,
            cancellationToken);

        var token = ToBase64Url(RandomNumberGenerator.GetBytes(32));
        var now = timeProvider.GetUtcNow();
        await state.AddReadGrantAsync(
            new AttachmentReadGrantRecord(
                Guid.NewGuid(),
                attachment.OrganizationId,
                attachment.CampId,
                attachment.Id,
                request.ActorId,
                HashToken(token),
                now,
                now.Add(ReadGrantLifetime)),
            cancellationToken);
        return new AttachmentReadGrant(
            token,
            attachment.Id,
            now.Add(ReadGrantLifetime),
            Disposition(attachment.MediaType));
    }

    public async Task<AttachmentContent> OpenReadAsync(
        OpenAttachmentReadGrant request,
        CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(RequiredToken(request.Token));
        var grant = await state.FindReadGrantAsync(request.ActorId, tokenHash, cancellationToken)
            ?? throw InvalidGrant();
        var now = timeProvider.GetUtcNow();
        if (grant.UsedAt is not null || grant.ExpiresAt <= now)
        {
            throw InvalidGrant();
        }
        var attachment = await state.FindAsync(
            grant.OrganizationId,
            grant.CampId,
            grant.AttachmentId,
            cancellationToken)
            ?? throw InvalidGrant();
        EnsureAvailable(attachment);

        _ = await RequireOwnerAccessAsync(
            request.ActorId,
            attachment.OrganizationId,
            attachment.CampId,
            attachment.Owner,
            AttachmentOwnerAction.Read,
            cancellationToken);
        if (!await state.TryConsumeReadGrantAsync(grant.Id, now, cancellationToken))
        {
            throw InvalidGrant();
        }

        var blob = await blobStorage.OpenReadAsync(attachment.BlobName, cancellationToken)
            ?? throw Rule("attachment_blob_missing", "Die Datei des Anhangs wurde nicht gefunden.");
        return new AttachmentContent(
            blob.Content,
            attachment.OriginalFileName,
            attachment.ContentType,
            blob.Length,
            Disposition(attachment.MediaType),
            attachment.Version);
    }

    public async Task<AttachmentPurgeResult> PurgeDueAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        if (batchSize is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        }
        var due = await state.ListDueForPurgeAsync(
            timeProvider.GetUtcNow(),
            batchSize,
            cancellationToken);
        var metadataPurged = 0;
        var blobsDeleted = 0;
        var failures = 0;
        foreach (var attachment in due)
        {
            try
            {
                if (await blobStorage.DeleteIfExistsAsync(attachment.BlobName, cancellationToken))
                {
                    blobsDeleted++;
                }
                await state.DeletePurgedAsync(attachment, cancellationToken);
                metadataPurged++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                failures++;
            }
        }
        return new AttachmentPurgeResult(metadataPurged, blobsDeleted, failures);
    }

    public async Task<int> DeleteExpiredReadGrantsAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        if (batchSize is < 1 or > 10000)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        }
        return await state.DeleteExpiredReadGrantsAsync(
            timeProvider.GetUtcNow(),
            batchSize,
            cancellationToken);
    }

    private async Task<AttachmentRecord> RequireAttachmentAsync(
        ChangeAttachmentLifecycle command,
        CancellationToken cancellationToken) =>
        await state.FindAsync(
            command.OrganizationId,
            command.CampId,
            command.AttachmentId,
            cancellationToken)
        ?? throw NotFound();

    private async Task<AttachmentOwnerScope> RequireOwnerAccessAsync(
        Guid actorId,
        Guid organizationId,
        Guid? campId,
        AttachmentOwnerReference owner,
        AttachmentOwnerAction action,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(owner.Type) || owner.Id == Guid.Empty)
        {
            throw NotFound();
        }
        var expectedScope = ExpectedScope(owner.Type, organizationId, campId);
        var decision = await ownerAuthorization.AuthorizeAsync(
            new AttachmentOwnerAccessRequest(
                actorId,
                organizationId,
                campId,
                owner,
                action),
            cancellationToken);
        if (!decision.Allowed || decision.Scope != expectedScope)
        {
            throw Rule("attachment_access_denied", "Du darfst auf diesen Anhang nicht zugreifen.");
        }
        return expectedScope;
    }

    private async Task RequireCampManagementAsync(
        Guid actorId,
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken)
    {
        var decision = await tenantAccessControl.AuthorizeCampAsync(
            new CampAccessRequest(actorId, organizationId, campId, CampAction.ManageCamp),
            cancellationToken);
        if (!decision.Allowed)
        {
            throw Rule("attachment_access_denied", "Du darfst den Papierkorb nicht verwalten.");
        }
    }

    private async Task RequireQuotaAccessAsync(
        AttachmentQuotaQuery query,
        CancellationToken cancellationToken)
    {
        TenantAccessDecision decision;
        if (query.Scope == AttachmentQuotaScopeType.Camp && query.CampId is { } campId)
        {
            decision = await tenantAccessControl.AuthorizeCampAsync(
                new CampAccessRequest(query.ActorId, query.OrganizationId, campId, CampAction.Read),
                cancellationToken);
        }
        else
        {
            decision = await tenantAccessControl.AuthorizeOrganizationAsync(
                new OrganizationAccessRequest(query.ActorId, query.OrganizationId, OrganizationAction.Read),
                cancellationToken);
        }
        if (!decision.Allowed)
        {
            throw Rule("attachment_access_denied", "Du darfst auf diesen Speicherbereich nicht zugreifen.");
        }
    }

    private async Task CompensateFailedUploadAsync(AttachmentRecord attachment)
    {
        try
        {
            _ = await blobStorage.DeleteIfExistsAsync(attachment.BlobName, CancellationToken.None);
        }
        catch
        {
            // A later orphan cleanup may safely retry the random blob name.
        }
        await state.CancelPendingAsync(attachment, CancellationToken.None);
    }

    private static AttachmentOwnerScope ExpectedScope(
        AttachmentOwnerType ownerType,
        Guid organizationId,
        Guid? campId)
    {
        if (ownerType == AttachmentOwnerType.Recipe)
        {
            if (campId is not null)
            {
                throw Rule("attachment_scope_invalid", "Rezeptanhänge gehören zur veranstalterweiten Rezeptbibliothek.");
            }
            return new AttachmentOwnerScope(
                organizationId,
                null,
                AttachmentQuotaScopeType.OrganizationRecipeLibrary);
        }
        if (campId is null)
        {
            throw Rule("attachment_scope_invalid", "Dieser Anhang muss einem Camp zugeordnet sein.");
        }
        return new AttachmentOwnerScope(organizationId, campId, AttachmentQuotaScopeType.Camp);
    }

    private static void EnsureQuotaScope(AttachmentQuotaScopeType scope, Guid? campId)
    {
        if ((scope == AttachmentQuotaScopeType.Camp) != (campId is not null))
        {
            throw Rule("attachment_scope_invalid", "Der Speicherbereich ist ungültig.");
        }
    }

    private static void EnsureAvailable(AttachmentRecord attachment)
    {
        if (attachment.State != AttachmentLifecycleState.Available)
        {
            throw NotFound();
        }
    }

    private static AttachmentView ToView(AttachmentRecord attachment) => new(
        attachment.Id,
        attachment.OrganizationId,
        attachment.CampId,
        attachment.Owner,
        attachment.OriginalFileName,
        attachment.MediaType,
        attachment.ContentType,
        attachment.SizeBytes,
        attachment.State,
        attachment.CreatedBy,
        attachment.CreatedAt,
        attachment.DeletedAt,
        attachment.PurgeAt,
        attachment.Version);

    private static AttachmentContentDisposition Disposition(AttachmentMediaType mediaType) =>
        mediaType == AttachmentMediaType.Pdf
            ? AttachmentContentDisposition.Attachment
            : AttachmentContentDisposition.Inline;

    private static byte[] HashToken(string token) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(token));

    private static string RequiredToken(string token)
    {
        var trimmed = token.Trim();
        return trimmed.Length is > 0 and <= 200 ? trimmed : throw InvalidGrant();
    }

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static FilesRuleException InvalidGrant() =>
        Rule("attachment_grant_invalid", "Der kurzlebige Dateizugriff ist ungültig oder abgelaufen.");

    private static FilesRuleException NotFound() =>
        Rule("attachment_not_found", "Der Anhang wurde nicht gefunden.");

    private static FilesRuleException Rule(string code, string message) => new(code, message);
}
