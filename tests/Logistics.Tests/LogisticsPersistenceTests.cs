using Logistics.Implementation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Logistics.Tests;

public sealed class LogisticsPersistenceTests
{
    [Fact]
    public void ModelCarriesTenantKeysConcurrencyAndQuantityPrecision()
    {
        using var database = CreateContext();
        var entityNames = new[]
        {
            "MaterialRequirementEntity",
            "MaterialResponsibilityEntity",
            "ShoppingListEntity",
            "ShoppingItemEntity",
            "ShoppingItemResponsibilityEntity",
            "ShoppingCheckEventEntity",
        };

        foreach (var entityName in entityNames)
        {
            var entity = database.Model.FindEntityType($"Logistics.Implementation.{entityName}");
            Assert.NotNull(entity);
            Assert.Equal("organization_id", entity.FindProperty("OrganizationId")?.GetColumnName());
            Assert.Equal("camp_id", entity.FindProperty("CampId")?.GetColumnName());
        }

        var material = database.Model.FindEntityType(
            "Logistics.Implementation.MaterialRequirementEntity");
        var list = database.Model.FindEntityType("Logistics.Implementation.ShoppingListEntity");
        var item = database.Model.FindEntityType("Logistics.Implementation.ShoppingItemEntity");

        Assert.True(material!.FindProperty("Version")?.IsConcurrencyToken);
        Assert.Equal(18, material.FindProperty("QuantityValue")?.GetPrecision());
        Assert.Equal(6, material.FindProperty("QuantityValue")?.GetScale());
        Assert.True(list!.FindProperty("Version")?.IsConcurrencyToken);
        Assert.False(list.FindProperty("ChangeSequence")?.IsConcurrencyToken);
        Assert.True(item!.FindProperty("Version")?.IsConcurrencyToken);
        Assert.Equal(18, item.FindProperty("QuantityValue")?.GetPrecision());
        Assert.Equal(6, item.FindProperty("QuantityValue")?.GetScale());
    }

    [Fact]
    public void MigrationForcesRlsAndKeepsCheckAuditImmutable()
    {
        using var database = CreateContext();
        var script = database.GetService<IMigrator>().GenerateScript();
        var tables = new[]
        {
            "material_requirements",
            "material_responsibilities",
            "shopping_lists",
            "shopping_items",
            "shopping_item_responsibilities",
            "shopping_check_events",
        };

        foreach (var table in tables)
        {
            Assert.Contains(
                $"ALTER TABLE logistics.{table} FORCE ROW LEVEL SECURITY",
                script);
        }

        Assert.Contains("logistics.runtime_can_read_camp", script);
        Assert.Contains("logistics.runtime_can_write_camp", script);
        Assert.Contains("CREATE POLICY shopping_check_events_insert", script);
        Assert.DoesNotContain("CREATE POLICY shopping_check_events_update", script);
        Assert.DoesNotContain("CREATE POLICY shopping_check_events_delete", script);
    }

    private static LogisticsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LogisticsDbContext>()
            .UseNpgsql("Host=localhost;Database=freizeit_cockpit;Username=postgres")
            .Options;
        return new LogisticsDbContext(options);
    }
}
