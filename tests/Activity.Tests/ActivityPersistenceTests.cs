using Activity.Implementation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Activity.Tests;

public sealed class ActivityPersistenceTests
{
    [Fact]
    public void EveryActivityRowCarriesOrganizationAndCampScope()
    {
        using var context = new ActivityDbContext(
            new DbContextOptionsBuilder<ActivityDbContext>()
                .UseNpgsql("Host=localhost;Database=model_only")
                .Options);

        AssertScope<ActivityEventEntity>(context.Model);
        AssertScope<SearchDocumentEntity>(context.Model);

        var searchDocument = context.Model.FindEntityType(typeof(SearchDocumentEntity))!;
        Assert.True(searchDocument.FindProperty(nameof(SearchDocumentEntity.Version))!.IsConcurrencyToken);
        Assert.Equal("jsonb", searchDocument.FindProperty(nameof(SearchDocumentEntity.MetadataJson))!.GetColumnType());
    }

    private static void AssertScope<TEntity>(Microsoft.EntityFrameworkCore.Metadata.IModel model)
    {
        var entity = model.FindEntityType(typeof(TEntity))!;
        Assert.NotNull(entity.FindProperty("OrganizationId"));
        Assert.NotNull(entity.FindProperty("CampId"));
        Assert.NotNull(entity.FindProperty("Version"));
    }
}
