using Identity.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Knowledge.Implementation;

public sealed class KnowledgeDataErasure(KnowledgeDbContext dbContext) : IDataErasure
{
    public string Area => "knowledge";

    public async Task<DataErasureResult> EraseOrganizationAsync(
        Guid organizationId,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var notes = await dbContext.Notes
            .Where(item => item.OrganizationId == organizationId)
            .OrderBy(item => item.Id)
            .Take(Validate(batchSize))
            .ToArrayAsync(cancellationToken);
        dbContext.Notes.RemoveRange(notes);
        await dbContext.SaveChangesAsync(cancellationToken);
        var remaining = await dbContext.Notes.AnyAsync(
            item => item.OrganizationId == organizationId,
            cancellationToken);
        return new DataErasureResult(notes.Length, 0, remaining);
    }

    public async Task<DataErasureResult> PseudonymizeUserAsync(
        Guid userId,
        Guid pseudonymousUserId,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var notes = await dbContext.Notes
            .Where(item => item.CreatedBy == userId || item.UpdatedBy == userId || item.TrashedBy == userId)
            .OrderBy(item => item.Id)
            .Take(Validate(batchSize))
            .ToArrayAsync(cancellationToken);
        foreach (var note in notes)
        {
            if (note.CreatedBy == userId)
            {
                note.CreatedBy = pseudonymousUserId;
            }

            if (note.UpdatedBy == userId)
            {
                note.UpdatedBy = pseudonymousUserId;
            }

            if (note.TrashedBy == userId)
            {
                note.TrashedBy = pseudonymousUserId;
            }

            note.Version++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var remaining = await dbContext.Notes.AnyAsync(
            item => item.CreatedBy == userId || item.UpdatedBy == userId || item.TrashedBy == userId,
            cancellationToken);
        return new DataErasureResult(notes.Length, 0, remaining);
    }

    private static int Validate(int batchSize) => batchSize is >= 1 and <= 500
        ? batchSize
        : throw new ArgumentOutOfRangeException(nameof(batchSize));
}
