using Camps.Implementation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Camps.Tests;

public sealed class CampsPersistenceTests
{
    [Fact]
    public void ModelCarriesTenantKeysConcurrencyAndTimingConstraints()
    {
        using var database = CreateContext();
        var camp = database.Model.FindEntityType("Camps.Implementation.CampEntity");
        var schedule = database.Model.FindEntityType("Camps.Implementation.ScheduleEntryEntity");
        var responsibility = database.Model.FindEntityType(
            "Camps.Implementation.ScheduleResponsibilityEntity");

        Assert.NotNull(camp);
        Assert.NotNull(schedule);
        Assert.NotNull(responsibility);
        Assert.Equal("organization_id", camp.FindProperty("OrganizationId")?.GetColumnName());
        Assert.True(camp.FindProperty("Version")?.IsConcurrencyToken);
        Assert.Equal("organization_id", schedule.FindProperty("OrganizationId")?.GetColumnName());
        Assert.Equal("camp_id", schedule.FindProperty("CampId")?.GetColumnName());
        Assert.True(schedule.FindProperty("Version")?.IsConcurrencyToken);
        Assert.Equal("organization_id", responsibility.FindProperty("OrganizationId")?.GetColumnName());
        Assert.Equal("camp_id", responsibility.FindProperty("CampId")?.GetColumnName());
    }

    [Fact]
    public void MigrationForcesRowLevelSecurityOnEveryTenantTable()
    {
        using var database = CreateContext();
        var script = database.GetService<IMigrator>().GenerateScript();

        Assert.Contains("ALTER TABLE camps.camps FORCE ROW LEVEL SECURITY", script);
        Assert.Contains("ALTER TABLE camps.schedule_entries FORCE ROW LEVEL SECURITY", script);
        Assert.Contains(
            "ALTER TABLE camps.schedule_responsibilities FORCE ROW LEVEL SECURITY",
            script);
        Assert.Contains("camps.runtime_can_write_schedule", script);
    }

    private static CampsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CampsDbContext>()
            .UseNpgsql("Host=localhost;Database=freizeit_cockpit;Username=postgres")
            .Options;
        return new CampsDbContext(options);
    }
}
