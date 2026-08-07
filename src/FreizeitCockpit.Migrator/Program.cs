using Identity.Implementation;
using Microsoft.EntityFrameworkCore;
using Npgsql;

const long migrationLock = 7_590_111_001;
var builder = Host.CreateApplicationBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("freizeit")
    ?? throw new InvalidOperationException("ConnectionStrings:freizeit must be configured.");
builder.Services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(connectionString));

using var host = builder.Build();
await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();
await using var acquire = new NpgsqlCommand("SELECT pg_advisory_lock(@lock)", connection);
acquire.Parameters.AddWithValue("lock", migrationLock);
await acquire.ExecuteNonQueryAsync();
try
{
    await using var scope = host.Services.CreateAsyncScope();
    var identityDb = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await identityDb.Database.MigrateAsync();

    if (builder.Environment.IsDevelopment()
        && !await identityDb.Users.AnyAsync(user => user.NormalizedEmail == "MIRIAM@EXAMPLE.TEST"))
    {
        identityDb.Users.Add(new ApplicationUser
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000001"),
            UserName = "miriam@example.test",
            NormalizedUserName = "MIRIAM@EXAMPLE.TEST",
            Email = "miriam@example.test",
            NormalizedEmail = "MIRIAM@EXAMPLE.TEST",
            EmailConfirmed = true,
            DisplayName = "Miriam König",
            SecurityStamp = Guid.NewGuid().ToString("N")
        });
        await identityDb.SaveChangesAsync();
    }
}
finally
{
    await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock(@lock)", connection);
    release.Parameters.AddWithValue("lock", migrationLock);
    await release.ExecuteNonQueryAsync();
}
