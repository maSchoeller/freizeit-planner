var builder = DistributedApplication.CreateBuilder(args);

var database = builder
    .AddPostgres("postgres")
    .WithImageTag("17")
    .WithDataVolume()
    .AddDatabase("freizeit");

var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var blobs = storage.AddBlobs("blobs");

var mailpit = builder
    .AddContainer("mailpit", "axllent/mailpit", "v1.27")
    .WithHttpEndpoint(targetPort: 8025, name: "mail-ui")
    .WithEndpoint(targetPort: 1025, name: "smtp");

var bible = builder.AddProject<Projects.FreizeitCockpit_BibleStub>("bible-stub");
var migrator = builder
    .AddProject<Projects.FreizeitCockpit_Migrator>("migrator")
    .WithReference(database)
    .WaitFor(database);

builder
    .AddProject<Projects.FreizeitCockpit_Web>("web")
    .WithExternalHttpEndpoints()
    .WithReference(database)
    .WithReference(blobs)
    .WithReference(bible)
    .WithReference(mailpit.GetEndpoint("smtp"))
    .WaitForCompletion(migrator);

builder
    .AddProject<Projects.FreizeitCockpit_Cleanup>("cleanup")
    .WithReference(database)
    .WithReference(blobs)
    .WaitFor(database);

builder.Build().Run();
