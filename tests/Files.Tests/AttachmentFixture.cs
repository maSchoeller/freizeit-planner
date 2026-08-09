using System.Collections.Concurrent;
using Files.Contracts;
using Files.Implementation;
using Identity.Contracts;

namespace Files.Tests;

internal sealed class AttachmentFixture
{
    private AttachmentFixture()
    {
        State = new InMemoryAttachmentState();
        Storage = new InMemoryPrivateBlobStorage();
        OwnerAuthorization = new ConfigurableOwnerAuthorization();
        TenantAccess = new ConfigurableTenantAccessControl();
        Clock = new ManualTimeProvider(new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero));
        Service = new AttachmentService(
            State,
            OwnerAuthorization,
            TenantAccess,
            Storage,
            Clock);
    }

    public Guid ActorId { get; } = Guid.Parse("81000000-0000-0000-0000-000000000001");

    public Guid OrganizationId { get; } = Guid.Parse("82000000-0000-0000-0000-000000000001");

    public Guid CampId { get; } = Guid.Parse("83000000-0000-0000-0000-000000000001");

    public Guid OwnerId { get; } = Guid.Parse("84000000-0000-0000-0000-000000000001");

    public AttachmentService Service { get; }

#pragma warning disable CA1859 // Acceptance tests intentionally use public module interfaces.
    public IAttachmentCatalog Catalog => Service;

    public IAttachmentReader Reader => Service;

    public IAttachmentMaintenance Maintenance => Service;
#pragma warning restore CA1859

    public InMemoryAttachmentState State { get; }

    public InMemoryPrivateBlobStorage Storage { get; }

    public ConfigurableOwnerAuthorization OwnerAuthorization { get; }

    public ConfigurableTenantAccessControl TenantAccess { get; }

    public ManualTimeProvider Clock { get; }

    public static AttachmentFixture Create() => new();

    public UploadAttachment UploadCommand(
        string fileName,
        string contentType,
        long? length,
        AttachmentOwnerType ownerType = AttachmentOwnerType.Note,
        Guid? campId = null) =>
        new(
            ActorId,
            OrganizationId,
            ownerType == AttachmentOwnerType.Recipe ? null : campId ?? CampId,
            new AttachmentOwnerReference(ownerType, OwnerId),
            fileName,
            contentType,
            length);

    public async Task<AttachmentView> UploadPngAsync(CancellationToken cancellationToken)
    {
        await using var content = new MemoryStream(
            [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x01]);
        return await Catalog.UploadAsync(
            UploadCommand("bild.png", "image/png", content.Length),
            content,
            cancellationToken);
    }
}

internal sealed class ConfigurableOwnerAuthorization : IAttachmentOwnerAuthorization
{
    public bool Denied { get; set; }

    public AttachmentOwnerScope? OverrideScope { get; set; }

    public Task<AttachmentOwnerAccessDecision> AuthorizeAsync(
        AttachmentOwnerAccessRequest request,
        CancellationToken cancellationToken)
    {
        if (Denied)
        {
            return Task.FromResult(AttachmentOwnerAccessDecision.Deny());
        }
        var scope = OverrideScope ?? new AttachmentOwnerScope(
            request.OrganizationId,
            request.Owner.Type == AttachmentOwnerType.Recipe ? null : request.CampId,
            request.Owner.Type == AttachmentOwnerType.Recipe
                ? AttachmentQuotaScopeType.OrganizationRecipeLibrary
                : AttachmentQuotaScopeType.Camp);
        return Task.FromResult(AttachmentOwnerAccessDecision.Permit(scope));
    }
}

internal sealed class ConfigurableTenantAccessControl : ITenantAccessControl
{
    public HashSet<CampAction> DeniedCampActions { get; } = [];

    public Task<TenantAccessDecision> AuthorizeOrganizationAsync(
        OrganizationAccessRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(TenantAccessDecision.Permit(TenantRole.Member));

    public Task<TenantAccessDecision> AuthorizeCampAsync(
        CampAccessRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(DeniedCampActions.Contains(request.Action)
            ? TenantAccessDecision.Deny(TenantAccessDenial.PermissionDenied)
            : TenantAccessDecision.Permit(TenantRole.Member));
}

internal sealed class InMemoryPrivateBlobStorage : IPrivateBlobStorage
{
    private readonly ConcurrentDictionary<string, byte[]> blobs = new(StringComparer.Ordinal);

    public string StoredBlobName { get; private set; } = string.Empty;

    public bool FailWrites { get; set; }

    public bool FailDeletes { get; set; }

    public int Count => blobs.Count;

    public async Task StoreAsync(
        PrivateBlobWrite write,
        Stream content,
        CancellationToken cancellationToken)
    {
        if (FailWrites)
        {
            throw new IOException("Speicher nicht verfügbar.");
        }
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        if (!blobs.TryAdd(write.BlobName, buffer.ToArray()))
        {
            throw new IOException("Blob existiert bereits.");
        }
        StoredBlobName = write.BlobName;
    }

    public Task<PrivateBlobContent?> OpenReadAsync(
        string blobName,
        CancellationToken cancellationToken)
    {
        if (!blobs.TryGetValue(blobName, out var value))
        {
            return Task.FromResult<PrivateBlobContent?>(null);
        }
        PrivateBlobContent result = new(new MemoryStream(value, writable: false), value.LongLength);
        return Task.FromResult<PrivateBlobContent?>(result);
    }

    public Task<bool> DeleteIfExistsAsync(
        string blobName,
        CancellationToken cancellationToken)
    {
        if (FailDeletes)
        {
            throw new IOException("Löschen nicht verfügbar.");
        }
        return Task.FromResult(blobs.TryRemove(blobName, out _));
    }
}

internal sealed class InMemoryAttachmentState : IAttachmentState
{
    private readonly object gate = new();
    private readonly List<AttachmentRecord> attachments = [];
    private readonly List<AttachmentReadGrantRecord> grants = [];

    public IReadOnlyList<AttachmentRecord> Attachments
    {
        get
        {
            lock (gate)
            {
                return attachments.ToArray();
            }
        }
    }

    public IReadOnlyList<AttachmentReadGrantRecord> Grants
    {
        get
        {
            lock (gate)
            {
                return grants.ToArray();
            }
        }
    }

    public void Seed(AttachmentRecord attachment)
    {
        lock (gate)
        {
            attachments.Add(attachment);
        }
    }

    public ValueTask<IReadOnlyList<AttachmentRecord>> ListAsync(
        Guid organizationId,
        Guid? campId,
        AttachmentOwnerReference owner,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return ValueTask.FromResult<IReadOnlyList<AttachmentRecord>>(attachments.Where(item =>
                item.OrganizationId == organizationId
                && item.CampId == campId
                && item.Owner == owner
                && (includeDeleted || item.State == AttachmentLifecycleState.Available)).ToArray());
        }
    }

    public ValueTask<IReadOnlyList<AttachmentRecord>> ListTrashAsync(
        Guid organizationId,
        Guid campId,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return ValueTask.FromResult<IReadOnlyList<AttachmentRecord>>(attachments.Where(item =>
                item.OrganizationId == organizationId
                && item.CampId == campId
                && item.State == AttachmentLifecycleState.Deleted).ToArray());
        }
    }

    public ValueTask<AttachmentRecord?> FindAsync(
        Guid organizationId,
        Guid? campId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return ValueTask.FromResult(attachments.SingleOrDefault(item =>
                item.OrganizationId == organizationId && item.CampId == campId && item.Id == attachmentId));
        }
    }

    public ValueTask<bool> TryReserveAsync(
        AttachmentRecord attachment,
        long quotaLimitBytes,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            var used = attachments.Where(item => SameScope(item, attachment)).Sum(item => item.SizeBytes);
            if (used + attachment.SizeBytes > quotaLimitBytes)
            {
                return ValueTask.FromResult(false);
            }
            attachments.Add(attachment);
            return ValueTask.FromResult(true);
        }
    }

    public ValueTask MarkAvailableAsync(AttachmentRecord attachment, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    public ValueTask CancelPendingAsync(AttachmentRecord attachment, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            _ = attachments.Remove(attachment);
            return ValueTask.CompletedTask;
        }
    }

    public ValueTask SaveAsync(AttachmentRecord attachment, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    public ValueTask<AttachmentQuotaUsage> GetQuotaUsageAsync(
        Guid organizationId,
        Guid? campId,
        AttachmentQuotaScopeType scope,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            var matching = attachments.Where(item => item.OrganizationId == organizationId
                && item.CampId == campId
                && item.QuotaScope == scope).ToArray();
            return ValueTask.FromResult(new AttachmentQuotaUsage(
                matching.Sum(item => item.SizeBytes),
                matching.Where(item => item.State == AttachmentLifecycleState.PendingUpload).Sum(item => item.SizeBytes)));
        }
    }

    public ValueTask AddReadGrantAsync(AttachmentReadGrantRecord grant, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            grants.Add(grant);
            return ValueTask.CompletedTask;
        }
    }

    public ValueTask<AttachmentReadGrantRecord?> FindReadGrantAsync(
        Guid actorId,
        byte[] tokenHash,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return ValueTask.FromResult(grants.SingleOrDefault(item =>
                item.ActorId == actorId && item.TokenHash.AsSpan().SequenceEqual(tokenHash)));
        }
    }

    public ValueTask<bool> TryConsumeReadGrantAsync(
        Guid grantId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            var index = grants.FindIndex(item => item.Id == grantId);
            if (index < 0 || grants[index].UsedAt is not null || grants[index].ExpiresAt <= now)
            {
                return ValueTask.FromResult(false);
            }
            grants[index] = grants[index] with { UsedAt = now };
            return ValueTask.FromResult(true);
        }
    }

    public ValueTask RevokeReadGrantsAsync(Guid attachmentId, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            grants.RemoveAll(item => item.AttachmentId == attachmentId);
            return ValueTask.CompletedTask;
        }
    }

    public ValueTask<IReadOnlyList<AttachmentRecord>> ListDueForPurgeAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return ValueTask.FromResult<IReadOnlyList<AttachmentRecord>>(attachments
                .Where(item => item.State == AttachmentLifecycleState.Deleted && item.PurgeAt <= now)
                .Take(batchSize)
                .ToArray());
        }
    }

    public ValueTask DeletePurgedAsync(AttachmentRecord attachment, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            _ = attachments.Remove(attachment);
            grants.RemoveAll(item => item.AttachmentId == attachment.Id);
            return ValueTask.CompletedTask;
        }
    }

    public ValueTask<int> DeleteExpiredReadGrantsAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            var expired = grants.Where(item => item.ExpiresAt <= now || item.UsedAt is not null)
                .Take(batchSize)
                .ToArray();
            foreach (var grant in expired)
            {
                _ = grants.Remove(grant);
            }
            return ValueTask.FromResult(expired.Length);
        }
    }

    private static bool SameScope(AttachmentRecord left, AttachmentRecord right) =>
        left.OrganizationId == right.OrganizationId
        && left.CampId == right.CampId
        && left.QuotaScope == right.QuotaScope;
}

internal sealed class ManualTimeProvider(DateTimeOffset current) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => current;

    public void Advance(TimeSpan duration) => current = current.Add(duration);
}
