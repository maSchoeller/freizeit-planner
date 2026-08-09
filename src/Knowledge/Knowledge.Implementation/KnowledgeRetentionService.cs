using Knowledge.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Knowledge.Implementation;

public sealed class KnowledgeRetentionService(
    KnowledgeDbContext dbContext,
    TimeProvider timeProvider) : INotebookRetention
{
    public async Task<NotePurgeResult> PurgeExpiredNotesAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        if (batchSize is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        }

        var notes = await dbContext.Notes
            .Where(item => item.State == NoteState.Trashed && item.PurgeAfter <= timeProvider.GetUtcNow())
            .OrderBy(item => item.PurgeAfter)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken);
        dbContext.Notes.RemoveRange(notes);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new NotePurgeResult(notes.Length);
    }
}
