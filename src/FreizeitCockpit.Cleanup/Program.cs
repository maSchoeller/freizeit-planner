using Azure.Identity;
using Azure.Storage.Blobs;
using Activity.Implementation;
using Camps.Implementation;
using Catering.Implementation;
using Files.Contracts;
using Files.Implementation;
using FreizeitCockpit.Cleanup;
using FreizeitCockpit.ServiceDefaults;
using Identity.Contracts;
using Identity.Implementation;
using Knowledge.Contracts;
using Knowledge.Implementation;
using Logistics.Implementation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Spiritual.Implementation;
using Spiritual.Contracts;

var builder = Host.CreateApplicationBuilder(args);
builder.AddFreizeitServiceDefaults();

builder.Services.AddSingleton(
    TimeProvider.System);
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
builder.Services.AddDbContext<KnowledgeDbContext>((services, options) =>
    options.UseNpgsql(services.GetRequiredService<NpgsqlConnection>()));
builder.Services.AddDbContext<LogisticsDbContext>((services, options) =>
    options.UseNpgsql(services.GetRequiredService<NpgsqlConnection>()));
builder.Services.AddDbContext<SpiritualDbContext>((services, options) =>
    options.UseNpgsql(services.GetRequiredService<NpgsqlConnection>()));
builder.Services.AddDbContext<FilesDbContext>((services, options) =>
    options.UseNpgsql(services.GetRequiredService<NpgsqlConnection>()));
builder.Services.AddDbContext<ActivityDbContext>((services, options) =>
    options.UseNpgsql(services.GetRequiredService<NpgsqlConnection>()));

builder.Services.AddSingleton<IPrivateBlobStorage>(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("blobs");
    BlobContainerClient container;
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        container = new BlobContainerClient(connectionString, "attachments");
    }
    else
    {
        var serviceUri = builder.Configuration["Storage:BlobServiceUri"]
            ?? throw new InvalidOperationException("Storage:BlobServiceUri must be configured.");
        container = new BlobContainerClient(
            new Uri(new Uri(serviceUri), "attachments"),
            new DefaultAzureCredential());
    }

    return new AzurePrivateBlobStorage(container);
});
builder.Services.AddScoped<IIdentityMaintenance, IdentityMaintenanceService>();
builder.Services.AddScoped<INotebookRetention, KnowledgeRetentionService>();
builder.Services.AddScoped<IAttachmentMaintenance, AttachmentMaintenanceService>();
builder.Services.AddScoped<IDevotionState, EfDevotionState>();
builder.Services.AddScoped<IDevotionRetention, DevotionRetentionService>();
builder.Services.AddScoped<IDataErasure, ActivityDataErasure>();
builder.Services.AddScoped<IDataErasure, CampsDataErasure>();
builder.Services.AddScoped<IDataErasure, CateringDataErasure>();
builder.Services.AddScoped<IDataErasure, FilesDataErasure>();
builder.Services.AddScoped<IDataErasure, KnowledgeDataErasure>();
builder.Services.AddScoped<IDataErasure, LogisticsDataErasure>();
builder.Services.AddScoped<IDataErasure, SpiritualDataErasure>();
builder.Services.AddSingleton(new CleanupOptions
{
    BatchSize = builder.Configuration.GetValue<int?>("Cleanup:BatchSize") ?? 100
});
builder.Services.AddScoped<CleanupJob>();

using var host = builder.Build();
await using var scope = host.Services.CreateAsyncScope();
await scope.ServiceProvider
    .GetRequiredService<CleanupJob>()
    .RunAsync(CancellationToken.None);
