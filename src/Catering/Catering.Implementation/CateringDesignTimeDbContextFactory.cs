using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Catering.Implementation;

public sealed class CateringDesignTimeDbContextFactory : IDesignTimeDbContextFactory<CateringDbContext>
{
    public CateringDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CateringDbContext>()
            .UseNpgsql("Host=localhost;Database=freizeit;Username=postgres")
            .Options;
        return new CateringDbContext(options);
    }
}
