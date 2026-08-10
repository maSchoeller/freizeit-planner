using Files.Contracts;
using Files.Implementation;
using Identity.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Files.Tests;

public sealed class EfAttachmentStateTests
{
    [Fact]
    public async Task RelationalAdapterPersistsUploadGrantTrashAndRestore()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var database = new FilesDbContext(options);
        await database.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE attachments (
                "Id" TEXT NOT NULL PRIMARY KEY,
                organization_id TEXT NOT NULL,
                camp_id TEXT NULL,
                "OwnerType" TEXT NOT NULL,
                owner_id TEXT NOT NULL,
                "QuotaScope" TEXT NOT NULL,
                "BlobName" TEXT NOT NULL,
                "OriginalFileName" TEXT NOT NULL,
                "MediaType" TEXT NOT NULL,
                "ContentType" TEXT NOT NULL,
                "SizeBytes" INTEGER NOT NULL,
                "CreatedBy" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "State" TEXT NOT NULL,
                "DeletedAt" TEXT NULL,
                "PurgeAt" TEXT NULL,
                "Version" INTEGER NOT NULL
            );
            CREATE TABLE read_grants (
                "Id" TEXT NOT NULL PRIMARY KEY,
                organization_id TEXT NOT NULL,
                camp_id TEXT NULL,
                attachment_id TEXT NOT NULL,
                actor_id TEXT NOT NULL,
                "TokenHash" BLOB NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "ExpiresAt" TEXT NOT NULL,
                "UsedAt" TEXT NULL
            );
            """,
            cancellationToken);
        var state = new EfAttachmentState(database);
        var storage = new InMemoryPrivateBlobStorage();
        var service = new AttachmentService(
            state,
            new ConfigurableOwnerAuthorization(),
            new ConfigurableTenantAccessControl(),
            storage,
            new ManualTimeProvider(new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero)));
        var actorId = Guid.Parse("81000000-0000-0000-0000-000000000001");
        var organizationId = Guid.Parse("82000000-0000-0000-0000-000000000001");
        var campId = Guid.Parse("83000000-0000-0000-0000-000000000001");
        var owner = new AttachmentOwnerReference(
            AttachmentOwnerType.Note,
            Guid.Parse("84000000-0000-0000-0000-000000000001"));
        var attachmentId = Guid.Parse("85000000-0000-0000-0000-000000000001");
        var createdAt = new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);

        await using var upload = new MemoryStream(
            [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x01]);
        await storage.StoreAsync(new PrivateBlobWrite("blob-1", "image/png", upload.Length), upload,
            cancellationToken);
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO attachments
                ("Id", organization_id, camp_id, "OwnerType", owner_id, "QuotaScope", "BlobName",
                 "OriginalFileName", "MediaType", "ContentType", "SizeBytes", "CreatedBy", "CreatedAt",
                 "State", "DeletedAt", "PurgeAt", "Version")
            VALUES
                ({attachmentId}, {organizationId}, {campId}, {AttachmentOwnerType.Note.ToString()}, {owner.Id},
                 {AttachmentQuotaScopeType.Camp.ToString()}, {"blob-1"}, {"lagerplan.png"},
                 {AttachmentMediaType.Png.ToString()}, {"image/png"}, {upload.Length}, {actorId}, {createdAt},
                 {AttachmentLifecycleState.Available.ToString()}, NULL, NULL, {1})
            """,
            cancellationToken);

        Assert.Single(await service.ListAsync(
            new AttachmentOwnerQuery(actorId, organizationId, campId, owner),
            cancellationToken));
        var quota = await service.GetQuotaAsync(
            new AttachmentQuotaQuery(actorId, organizationId, campId, AttachmentQuotaScopeType.Camp),
            cancellationToken);
        Assert.Equal(upload.Length, quota.UsedBytes);

        var grant = await service.IssueReadGrantAsync(
            new AttachmentReadGrantRequest(actorId, organizationId, campId, attachmentId),
            cancellationToken);
        database.ChangeTracker.Clear();
        Assert.NotEmpty(grant.Token);

        var deleted = await service.MoveToTrashAsync(
            new ChangeAttachmentLifecycle(actorId, organizationId, campId,
                attachmentId, 1),
            cancellationToken);
        database.ChangeTracker.Clear();
        Assert.Equal(AttachmentLifecycleState.Deleted, Assert.Single(await service.ListAsync(
            new AttachmentOwnerQuery(actorId, organizationId, campId, owner, IncludeDeleted: true),
            cancellationToken)).State);
        var restored = await service.RestoreAsync(
            new ChangeAttachmentLifecycle(actorId, organizationId, campId,
                attachmentId, deleted.Version),
            cancellationToken);
        Assert.Equal(AttachmentLifecycleState.Available, restored.State);
        Assert.Equal(deleted.Version + 1, restored.Version);

        database.ChangeTracker.Clear();
        var erasure = new FilesDataErasure(database, storage);
        var pseudonymized = await erasure.PseudonymizeUserAsync(actorId, Guid.Empty, 50, cancellationToken);
        Assert.Equal(1, pseudonymized.ChangedRecords);
        Assert.False(pseudonymized.HasRemaining);
        Assert.Equal("files", erasure.Area);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            erasure.EraseOrganizationAsync(organizationId, 0, cancellationToken));

        var maintenance = new AttachmentMaintenanceService(
            database,
            storage,
            new ManualTimeProvider(createdAt));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            maintenance.PurgeDueAsync(0, cancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            maintenance.PurgeDueAsync(501, cancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            maintenance.DeleteExpiredReadGrantsAsync(0, cancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            maintenance.DeleteExpiredReadGrantsAsync(501, cancellationToken));
    }
}
