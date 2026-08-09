using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Activity.Implementation;

public sealed class ActivityDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ActivityDbContext>
{
    public ActivityDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ActivityDbContext>()
            .UseNpgsql("Host=localhost;Database=freizeit;Username=postgres")
            .Options;
        return new ActivityDbContext(options);
    }
}
