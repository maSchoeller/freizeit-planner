using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Spiritual.Implementation;
using Xunit;

namespace Spiritual.Tests;

public sealed class SpiritualPersistenceTests
{
    [Fact]
    public void PostgreSqlModelUsesTenantColumnsAndVersionedDevotions()
    {
        var options = new DbContextOptionsBuilder<SpiritualDbContext>()
            .UseNpgsql("Host=localhost;Database=freizeit;Username=test;Password=test")
            .Options;
        using var dbContext = new SpiritualDbContext(options);

        var devotion = dbContext.Model.FindEntityType("Spiritual.Implementation.DevotionEntity");
        var table = StoreObjectIdentifier.Table("devotions", "spiritual");
        var snapshot = dbContext.Model.FindEntityType("Spiritual.Implementation.BibleSnapshotEntity");

        Assert.NotNull(devotion);
        Assert.NotNull(snapshot);
        Assert.Equal("spiritual", dbContext.Model.GetDefaultSchema());
        Assert.Equal("organization_id", devotion!.FindProperty("OrganizationId")?.GetColumnName(table));
        Assert.Equal("camp_id", devotion.FindProperty("CampId")?.GetColumnName(table));
        Assert.True(devotion.FindProperty("Version")?.IsConcurrencyToken);
        Assert.Equal("uuid[]", devotion.FindProperty("ResponsibleUserIds")?.GetColumnType());
        Assert.Equal("bible_snapshots", snapshot!.GetTableName());
        Assert.Contains(
            devotion.GetForeignKeys(),
            foreignKey => foreignKey.Properties.Single().Name == "CurrentBibleSnapshotId"
                && foreignKey.DeleteBehavior == DeleteBehavior.Restrict);
    }
}
