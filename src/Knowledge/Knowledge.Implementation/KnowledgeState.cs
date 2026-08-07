using Knowledge.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Knowledge.Implementation;

internal interface IKnowledgeState
{
    Task<IReadOnlyList<NoteEntity>> ListNotesAsync(
        Guid organizationId,
        Guid campId,
        NoteState state,
        CancellationToken cancellationToken);

    Task<NoteEntity?> FindNoteAsync(
        Guid organizationId,
        Guid campId,
        Guid noteId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<NoteEntity>> FindExpiredNotesAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken);

    void AddNote(NoteEntity note);

    void RemoveNotes(IEnumerable<NoteEntity> notes);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

internal sealed class EfKnowledgeState(KnowledgeDbContext dbContext) : IKnowledgeState
{
    public async Task<IReadOnlyList<NoteEntity>> ListNotesAsync(
        Guid organizationId,
        Guid campId,
        NoteState state,
        CancellationToken cancellationToken) =>
        await dbContext.Notes
            .Where(item =>
                item.OrganizationId == organizationId &&
                item.CampId == campId &&
                item.State == state)
            .Include(item => item.Tags)
            .Include(item => item.Links)
            .ToListAsync(cancellationToken);

    public Task<NoteEntity?> FindNoteAsync(
        Guid organizationId,
        Guid campId,
        Guid noteId,
        CancellationToken cancellationToken) =>
        dbContext.Notes
            .Include(item => item.Tags)
            .Include(item => item.Links)
            .SingleOrDefaultAsync(
                item => item.OrganizationId == organizationId && item.CampId == campId && item.Id == noteId,
                cancellationToken);

    public async Task<IReadOnlyList<NoteEntity>> FindExpiredNotesAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken) =>
        await dbContext.Notes
            .Where(item => item.State == NoteState.Trashed && item.PurgeAfter <= now)
            .OrderBy(item => item.PurgeAfter)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public void AddNote(NoteEntity note) => dbContext.Notes.Add(note);

    public void RemoveNotes(IEnumerable<NoteEntity> notes) => dbContext.Notes.RemoveRange(notes);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
