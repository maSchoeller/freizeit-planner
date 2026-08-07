using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Camps.Implementation;

public sealed class CampsDesignTimeDbContextFactory : IDesignTimeDbContextFactory<CampsDbContext>
{
    public CampsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CampsDbContext>()
            .UseNpgsql("Host=localhost;Database=freizeit_cockpit;Username=postgres")
            .Options;
        return new CampsDbContext(options);
    }
}
