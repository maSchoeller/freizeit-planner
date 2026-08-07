using Knowledge.Implementation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Knowledge.Tests;

public sealed class KnowledgePersistenceTests
{
    [Fact]
    public void EveryNotebookRowCarriesOrganizationAndCampScope()
    {
        using var context = new KnowledgeDbContext(
            new DbContextOptionsBuilder<KnowledgeDbContext>()
                .UseNpgsql("Host=localhost;Database=model_only")
                .Options);

        AssertScope<NoteEntity>(context.Model);
        AssertScope<NoteTagEntity>(context.Model);
        AssertScope<NoteLinkEntity>(context.Model);

        var tagForeignKey = context.Model.FindEntityType(typeof(NoteTagEntity))!.GetForeignKeys().Single();
        Assert.Equal(["NoteId", "OrganizationId", "CampId"], tagForeignKey.Properties.Select(item => item.Name));
        var linkForeignKey = context.Model.FindEntityType(typeof(NoteLinkEntity))!.GetForeignKeys().Single();
        Assert.Equal(["NoteId", "OrganizationId", "CampId"], linkForeignKey.Properties.Select(item => item.Name));
    }

    private static void AssertScope<TEntity>(Microsoft.EntityFrameworkCore.Metadata.IModel model)
    {
        var entity = model.FindEntityType(typeof(TEntity))!;
        Assert.NotNull(entity.FindProperty("OrganizationId"));
        Assert.NotNull(entity.FindProperty("CampId"));
    }
}
