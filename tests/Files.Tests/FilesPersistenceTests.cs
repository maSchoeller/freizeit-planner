using Files.Implementation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Files.Tests;

public sealed class FilesPersistenceTests
{
    [Fact]
    public void PostgreSqlModelHasTenantScopeConstraintsAndConcurrency()
    {
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseNpgsql("Host=localhost;Database=freizeit;Username=test;Password=test")
            .Options;
        using var dbContext = new FilesDbContext(options);

        var model = dbContext.GetService<IDesignTimeModel>().Model;
        var attachment = model.FindEntityType("Files.Implementation.AttachmentEntity");
        var grant = model.FindEntityType("Files.Implementation.AttachmentReadGrantEntity");
        var table = StoreObjectIdentifier.Table("attachments", "files");

        Assert.NotNull(attachment);
        Assert.NotNull(grant);
        Assert.Equal("files", model.GetDefaultSchema());
        Assert.Equal("organization_id", attachment!.FindProperty("OrganizationId")?.GetColumnName(table));
        Assert.Equal("camp_id", attachment.FindProperty("CampId")?.GetColumnName(table));
        Assert.True(attachment.FindProperty("Version")?.IsConcurrencyToken);
        Assert.Contains(attachment.GetCheckConstraints(), item => item.Name == "CK_attachments_owner_scope");
        Assert.True(grant!.GetIndexes().Single(item =>
            item.Properties.Count == 1 && item.Properties[0].Name == "TokenHash").IsUnique);
        Assert.Equal(DeleteBehavior.Cascade, grant.GetForeignKeys().Single().DeleteBehavior);
    }
}
