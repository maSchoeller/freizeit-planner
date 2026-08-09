using Azure.Core;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace FreizeitCockpit.ServiceDefaults;

public static class FreizeitServiceDefaults
{
    private static readonly string[] PostgreSqlScopes =
        ["https://ossrdbms-aad.database.windows.net/.default"];

    public static IHostApplicationBuilder AddFreizeitServiceDefaults(
        this IHostApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry();
        builder.Services.Configure<OpenTelemetryLoggerOptions>(options =>
        {
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
        });

        var telemetry = builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(builder.Environment.ApplicationName))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("Npgsql"))
            .WithTracing(tracing => tracing
                .AddSource(FreizeitCorrelation.ActivitySourceName)
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.Filter = context =>
                        !context.Request.Path.StartsWithSegments("/health")
                        && !context.Request.Path.StartsWithSegments("/ready");
                })
                .AddHttpClientInstrumentation(options =>
                {
                    options.FilterHttpRequestMessage = request =>
                        request.RequestUri?.Host is not "169.254.169.254";
                })
                .AddNpgsql());

        if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        {
            telemetry.UseAzureMonitor();
        }
        else if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            telemetry.UseOtlpExporter();
        }

        return builder;
    }

    public static NpgsqlDataSource CreatePostgresDataSource(
        IConfiguration configuration,
        IHostEnvironment environment,
        string connectionName = "freizeit")
    {
        var connectionString = configuration.GetConnectionString(connectionName)
            ?? throw new InvalidOperationException($"ConnectionStrings:{connectionName} must be configured.");
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        var authentication = configuration["Database:Authentication"];
        if (environment.IsProduction()
            && string.Equals(authentication, "ManagedIdentity", StringComparison.OrdinalIgnoreCase))
        {
            var clientId = configuration["AZURE_CLIENT_ID"];
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new InvalidOperationException(
                    "AZURE_CLIENT_ID must identify the user-assigned PostgreSQL identity in production.");
            }

            TokenCredential credential = new ManagedIdentityCredential(
                ManagedIdentityId.FromUserAssignedClientId(clientId));
            dataSourceBuilder.UsePeriodicPasswordProvider(
                async (_, cancellationToken) =>
                    (await credential.GetTokenAsync(
                        new TokenRequestContext(PostgreSqlScopes),
                        cancellationToken)).Token,
                successRefreshInterval: TimeSpan.FromMinutes(45),
                failureRefreshInterval: TimeSpan.FromSeconds(10));
        }

        return dataSourceBuilder.Build();
    }
}
