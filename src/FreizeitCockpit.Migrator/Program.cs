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

    await IdentitySeeder.SeedAsync(
        identityDb,
        builder.Configuration["Bootstrap:PlatformAdminEmail"],
        builder.Environment.IsDevelopment());
}
finally
{
    await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock(@lock)", connection);
    release.Parameters.AddWithValue("lock", migrationLock);
    await release.ExecuteNonQueryAsync();
}
