using Knowledge.Contracts;
using Knowledge.Implementation;
using Identity.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Knowledge.Tests;

public sealed class EfKnowledgeStateTests
{
    [Fact]
    public async Task RelationalAdapterPersistsRevisionTrashAndRestore()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var database = new KnowledgeDbContext(options);
        await database.Database.EnsureCreatedAsync(cancellationToken);
        var service = new KnowledgeService(
            database,
            new AllowKnowledgeAccessControl(),
            new TestKnowledgeCampContext(),
            new TestNoteLinkResolver(),
            new TestTimeProvider(new DateTimeOffset(2027, 8, 2, 10, 0, 0, TimeSpan.Zero)));
        var actorId = Guid.Parse("10000000-0000-0000-0000-000000000004");
        var organizationId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var campId = Guid.Parse("30000000-0000-0000-0000-000000000001");

        var note = await service.CreateNoteAsync(
            new CreateNote(actorId, organizationId, campId,
                new NoteContent("Packliste", "## Wichtig\n\n- Bibeln\n- Bälle", ["Team"], true, [])),
            cancellationToken);
        database.ChangeTracker.Clear();
        Assert.Single(await service.ListNotesAsync(
            new NotebookQuery(actorId, organizationId, campId, SearchText: "Bälle"),
            cancellationToken));
        Assert.Equal(note.Id, (await service.GetNoteAsync(
            new NoteRequest(actorId, organizationId, campId, note.Id),
            cancellationToken))?.Id);
        database.ChangeTracker.Clear();

        var trashed = await service.MoveNoteToTrashAsync(
            new MoveNoteToTrash(actorId, organizationId, campId, note.Id, note.Version),
            cancellationToken);
        database.ChangeTracker.Clear();
        Assert.Single(await service.ListNotesAsync(
            new NotebookQuery(actorId, organizationId, campId, NotebookSection.Trash),
            cancellationToken));
        var restored = await service.RestoreNoteAsync(
            new RestoreNote(actorId, organizationId, campId, note.Id, trashed.Version),
            cancellationToken);
        Assert.Equal(NoteState.Active, restored.State);
        Assert.Equal(trashed.Version + 1, restored.Version);

        var erasure = new KnowledgeDataErasure(database);
        var pseudonymized = await erasure.PseudonymizeUserAsync(actorId, Guid.Empty, 50, cancellationToken);
        Assert.Equal(1, pseudonymized.ChangedRecords);
        Assert.False(pseudonymized.HasRemaining);

        var erased = await erasure.EraseOrganizationAsync(organizationId, 50, cancellationToken);
        Assert.Equal(1, erased.ChangedRecords);
        Assert.False(erased.HasRemaining);
        Assert.Equal("knowledge", erasure.Area);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            erasure.PseudonymizeUserAsync(actorId, Guid.Empty, 501, cancellationToken));

        var retention = new KnowledgeRetentionService(database, TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            retention.PurgeExpiredNotesAsync(0, cancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            retention.PurgeExpiredNotesAsync(501, cancellationToken));
    }
}
