using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Logistics.Implementation;

public sealed class LogisticsDesignTimeDbContextFactory : IDesignTimeDbContextFactory<LogisticsDbContext>
{
    public LogisticsDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<LogisticsDbContext>()
            .UseNpgsql("Host=localhost;Database=freizeit_cockpit;Username=postgres")
            .Options);
}
