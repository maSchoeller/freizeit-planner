using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using Camps.Contracts;
using FreizeitCockpit.TestSupport;
using Identity.Contracts;
using Identity.Implementation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Api.Tests;

public sealed class AtomicPlanningTransactionTests
{
    [Fact]
    public async Task ResponsibilityCandidatesUseTheRealMinimizedCampDirectory()
    {
        var connectionString = Environment.GetEnvironmentVariable("FREIZEIT_ATOMIC_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var sender = new CapturingSender();
        using var factory = CreateFactory(connectionString, sender);
        using var client = CreateClient(factory);
        var cancellationToken = TestContext.Current.CancellationToken;
        await LoginAsync(client, sender, cancellationToken);

        using var response = await client.GetAsync(
            "/api/v1/organizations/20000000-0000-0000-0000-000000000001/camps/"
            + "30000000-0000-0000-0000-000000000001/responsibility-candidates",
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var candidates = await response.Content.ReadFromJsonAsync<IReadOnlyList<CampMemberSummary>>(
            cancellationToken);

        Assert.Equal(5, candidates?.Count);
        Assert.DoesNotContain(
            candidates!,
            candidate => candidate.UserId == Guid.Parse("10000000-0000-0000-0000-000000000006"));
    }

    [Fact]
    public async Task FailureInMealCreationRollsBackScheduleCreation()
    {
        var connectionString = Environment.GetEnvironmentVariable("FREIZEIT_ATOMIC_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var sender = new CapturingSender();
        using var logs = new ExceptionLoggerProvider();
        using var factory = CreateFactory(connectionString, sender, logs);
        using var client = CreateClient(factory);
        var cancellationToken = TestContext.Current.CancellationToken;
        await LoginAsync(client, sender, cancellationToken);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var organizationId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var campId = Guid.NewGuid();
        await using (var setupConnection = new NpgsqlConnection(connectionString))
        {
            await setupConnection.OpenAsync(cancellationToken);
            await using var insertCamp = new NpgsqlCommand(
                """
                INSERT INTO camps.camps
                    ("Id", organization_id, "Name", "Slug", "Description", "StartsOn", "EndsOn",
                     "TimeZoneId", "DefaultPortions", "Status", "Version")
                VALUES
                    (@id, @organizationId, @name, @slug, 'Transaktionstest', DATE '2027-08-01',
                     DATE '2027-08-08', 'Europe/Berlin', 20, 0, 1)
                """,
                setupConnection);
            insertCamp.Parameters.AddWithValue("id", campId);
            insertCamp.Parameters.AddWithValue("organizationId", organizationId);
            insertCamp.Parameters.AddWithValue("name", $"Rollback Camp {suffix}");
            insertCamp.Parameters.AddWithValue("slug", $"rollback-{suffix}");
            await insertCamp.ExecuteNonQueryAsync(cancellationToken);
        }

        var scheduleTitle = $"Rollback Zeitplan {suffix}";
        var csrf = await GetAntiforgeryAsync(client, cancellationToken);
        using var createLinked = CreateJsonRequest(
            HttpMethod.Post,
            $"/api/v1/organizations/{organizationId:D}/camps/{campId:D}/schedule/with-meal",
            new
            {
                schedule = new
                {
                    timing = new
                    {
                        isAllDay = false,
                        localStart = new DateTime(2027, 8, 2, 12, 0, 0),
                        localEnd = new DateTime(2027, 8, 2, 13, 0, 0),
                        startDate = (DateOnly?)null,
                        endDateExclusive = (DateOnly?)null,
                        startChoice = AmbiguousLocalTimeChoice.Reject,
                        endChoice = AmbiguousLocalTimeChoice.Reject
                    },
                    title = scheduleTitle,
                    description = (string?)null,
                    location = "Speisesaal",
                    category = "Essen",
                    status = ScheduleEntryStatus.Planned,
                    responsibleUserIds = Array.Empty<Guid>(),
                    audience = (string?)null
                },
                meal = new
                {
                    name = " ",
                    portionOverride = (int?)null,
                    recipeIds = Array.Empty<Guid>()
                }
            },
            csrf);

        using var failed = await client.SendAsync(createLinked, cancellationToken);
        Assert.True(
            failed.StatusCode == HttpStatusCode.BadRequest,
            $"Linked creation returned {(int)failed.StatusCode}: {string.Join(Environment.NewLine, logs.Exceptions)}");

        await using var verificationConnection = new NpgsqlConnection(connectionString);
        await verificationConnection.OpenAsync(cancellationToken);
        await using var countCommand = new NpgsqlCommand(
            "SELECT count(*) FROM camps.schedule_entries WHERE \"Title\" = @title",
            verificationConnection);
        countCommand.Parameters.AddWithValue("title", scheduleTitle);
        var persisted = Convert.ToInt32(
            await countCommand.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);
        Assert.Equal(0, persisted);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string connectionString,
        CapturingSender sender,
        ILoggerProvider? loggerProvider = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:freizeit", connectionString);
            if (loggerProvider is not null)
            {
                builder.ConfigureLogging(logging => logging.AddProvider(loggerProvider));
            }

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPasswordlessState>();
                services.RemoveAll<ILoginCodeSender>();
                services.AddSingleton<IPasswordlessState>(PasswordlessTestState.WithMiriam());
                services.AddSingleton<ILoginCodeSender>(sender);
            });
        });

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private static async Task LoginAsync(
        HttpClient client,
        CapturingSender sender,
        CancellationToken cancellationToken)
    {
        var csrf = await GetAntiforgeryAsync(client, cancellationToken);
        using var requestCode = CreateJsonRequest(
            HttpMethod.Post,
            "/api/v1/auth/code",
            new { email = "miriam@example.test" },
            csrf);
        using var requested = await client.SendAsync(requestCode, cancellationToken);
        requested.EnsureSuccessStatusCode();
        csrf = await GetAntiforgeryAsync(client, cancellationToken);
        using var verify = CreateJsonRequest(
            HttpMethod.Post,
            "/api/v1/auth/verify",
            new { email = "miriam@example.test", code = Assert.Single(sender.Codes), rememberMe = false },
            csrf);
        using var verified = await client.SendAsync(verify, cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
    }

    private static async Task<string> GetAntiforgeryAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync("/api/v1/auth/antiforgery", cancellationToken);
        response.EnsureSuccessStatusCode();
        var value = await response.Content.ReadFromJsonAsync<AntiforgeryResponse>(cancellationToken);
        return Assert.IsType<string>(value?.Token);
    }

    private static HttpRequestMessage CreateJsonRequest(
        HttpMethod method,
        string uri,
        object body,
        string csrf)
    {
        var request = new HttpRequestMessage(method, uri) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        return request;
    }

    private sealed record AntiforgeryResponse(string Token);

    private sealed class CapturingSender : ILoginCodeSender
    {
        public List<string> Codes { get; } = [];

        public Task SendAsync(
            string email,
            string code,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken)
        {
            Codes.Add(code);
            return Task.CompletedTask;
        }
    }

    private sealed class ExceptionLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<Exception> Exceptions { get; } = [];

        public ILogger CreateLogger(string categoryName) => new ExceptionLogger(Exceptions);

        public void Dispose()
        {
        }

        private sealed class ExceptionLogger(ConcurrentQueue<Exception> exceptions) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state)
                where TState : notnull => NoopScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (exception is not null) exceptions.Enqueue(exception);
            }
        }

        private sealed class NoopScope : IDisposable
        {
            public static NoopScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
