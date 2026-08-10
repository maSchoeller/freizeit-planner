using Activity.Implementation;
using Camps.Implementation;
using Catering.Implementation;
using FreizeitCockpit.ServiceDefaults;
using Identity.Implementation;
using Knowledge.Implementation;
using Logistics.Implementation;
using Files.Implementation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Spiritual.Implementation;

const long migrationLock = 7_590_111_001;
var builder = Host.CreateApplicationBuilder(args);
builder.AddFreizeitServiceDefaults();
builder.Services.AddSingleton(services => FreizeitServiceDefaults.CreatePostgresDataSource(
    builder.Configuration,
    builder.Environment));
builder.Services.AddScoped(services =>
    services.GetRequiredService<NpgsqlDataSource>().CreateConnection());
builder.Services.AddDbContext<IdentityDbContext>((services, options) =>
    options.UseNpgsql(services.GetRequiredService<NpgsqlConnection>()));
builder.Services.AddDbContext<CampsDbContext>((services, options) =>
    options.UseNpgsql(services.GetRequiredService<NpgsqlConnection>()));
builder.Services.AddDbContext<CateringDbContext>((services, options) =>
    options.UseNpgsql(services.GetRequiredService<NpgsqlConnection>()));
builder.Services.AddDbContext<SpiritualDbContext>((services, options) =>
    options.UseNpgsql(services.GetRequiredService<NpgsqlConnection>()));
builder.Services.AddDbContext<KnowledgeDbContext>((services, options) =>
    options.UseNpgsql(services.GetRequiredService<NpgsqlConnection>()));
builder.Services.AddDbContext<LogisticsDbContext>((services, options) =>
    options.UseNpgsql(services.GetRequiredService<NpgsqlConnection>()));
builder.Services.AddDbContext<FilesDbContext>((services, options) =>
    options.UseNpgsql(services.GetRequiredService<NpgsqlConnection>()));
builder.Services.AddDbContext<ActivityDbContext>((services, options) =>
    options.UseNpgsql(services.GetRequiredService<NpgsqlConnection>()));

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("FreizeitCockpit.Migrator");
using var correlation = FreizeitCorrelation.BeginOperation(logger, "migrator.run");
var dataSource = host.Services.GetRequiredService<NpgsqlDataSource>();
await using var connection = await dataSource.OpenConnectionAsync();
await using var acquire = new NpgsqlCommand("SELECT pg_advisory_lock(@lock)", connection);
acquire.Parameters.AddWithValue("lock", migrationLock);
await acquire.ExecuteNonQueryAsync();
try
{
    await using var scope = host.Services.CreateAsyncScope();
    var identityDb = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await identityDb.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<CampsDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<CateringDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<SpiritualDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<LogisticsDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<FilesDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<ActivityDbContext>().Database.MigrateAsync();

    await IdentitySeeder.SeedAsync(
        identityDb,
        builder.Environment.IsDevelopment());
}
finally
{
    await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock(@lock)", connection);
    release.Parameters.AddWithValue("lock", migrationLock);
    await release.ExecuteNonQueryAsync();
}
