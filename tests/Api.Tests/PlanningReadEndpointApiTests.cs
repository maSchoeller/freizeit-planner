using System.Collections;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Nodes;
using Activity.Contracts;
using Camps.Contracts;
using Catering.Contracts;
using Files.Contracts;
using FreizeitCockpit.TestSupport;
using Identity.Contracts;
using Identity.Implementation;
using Knowledge.Contracts;
using Logistics.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Spiritual.Contracts;
using Xunit;

namespace Api.Tests;

public sealed class PlanningReadEndpointApiTests
{
    private const string OrganizationId = "20000000-0000-0000-0000-000000000001";
    private const string CampId = "30000000-0000-0000-0000-000000000001";
    private const string ObjectId = "40000000-0000-0000-0000-000000000001";

    public static TheoryData<string> PlanningReads => new()
    {
        $"/api/v1/organizations/{OrganizationId}/catering/ingredients?query=reis",
        $"/api/v1/organizations/{OrganizationId}/catering/recipes",
        $"/api/v1/organizations/{OrganizationId}/catering/recipes/{ObjectId}",
        $"/api/v1/organizations/{OrganizationId}/camps/{CampId}/catering/meals",
        $"/api/v1/organizations/{OrganizationId}/camps/{CampId}/catering/meals/{ObjectId}",
        $"/api/v1/organizations/{OrganizationId}/camps/{CampId}/catering/meals/{ObjectId}/shopping-draft",
        $"/api/v1/organizations/{OrganizationId}/camps/{CampId}/devotions",
        $"/api/v1/organizations/{OrganizationId}/camps/{CampId}/devotions/{ObjectId}",
        $"/api/v1/organizations/{OrganizationId}/camps/{CampId}/devotions/translations",
        $"/api/v1/organizations/{OrganizationId}/camps/{CampId}/notes",
        $"/api/v1/organizations/{OrganizationId}/camps/{CampId}/notes/{ObjectId}",
        $"/api/v1/organizations/{OrganizationId}/camps/{CampId}/logistics/material",
        $"/api/v1/organizations/{OrganizationId}/camps/{CampId}/logistics/material/{ObjectId}",
        $"/api/v1/organizations/{OrganizationId}/camps/{CampId}/logistics/shopping-lists",
        $"/api/v1/organizations/{OrganizationId}/camps/{CampId}/logistics/shopping-lists/{ObjectId}",
        $"/api/v1/organizations/{OrganizationId}/camps/{CampId}/logistics/shopping-lists/{ObjectId}/items/{ObjectId}/audit",
        $"/api/v1/organizations/{OrganizationId}/camps/{CampId}/files?ownerType=Note&ownerId={ObjectId}",
        $"/api/v1/organizations/{OrganizationId}/camps/{CampId}/files/quota",
        $"/api/v1/organizations/{OrganizationId}/camps/{CampId}/activity?kinds=Created&objectTypes=Note&limit=5",
        $"/api/v1/organizations/{OrganizationId}/camps/{CampId}/search?query=wald&objectTypes=Note&metadata=tag:gruppe&limit=5",
        $"/api/v1/organizations/{OrganizationId}/camps/{CampId}/exports/schedule.csv?fromDate=2027-08-01&toDateExclusive=2027-08-08",
        $"/api/v1/organizations/{OrganizationId}/camps/{CampId}/exports/meals.csv",
        $"/api/v1/organizations/{OrganizationId}/camps/{CampId}/exports/material.csv",
        $"/api/v1/organizations/{OrganizationId}/camps/{CampId}/exports/shopping.csv",
        $"/api/v1/organizations/{OrganizationId}/camps/{CampId}/exports/shopping.csv?listId={ObjectId}",
        $"/api/v1/organizations/{OrganizationId}/camps/{CampId}/trash",
        $"/api/v1/organizations/{OrganizationId}/camps/{CampId}/responsibility-candidates",
        $"/api/v1/organizations/{OrganizationId}/camps/{CampId}/schedule?fromDate=2027-08-01&toDateExclusive=2027-08-08",
        $"/api/v1/organizations/{OrganizationId}/camps/{CampId}/schedule/{ObjectId}",
        $"/api/v1/organizations/{OrganizationId}/camps/by-slug/testcamp",
        $"/api/v1/organizations/{OrganizationId}/camps",
        "/api/v1/account",
        "/api/v1/account/memberships",
        "/api/v1/auth/sessions",
        $"/api/v1/invitations/organizations/{OrganizationId}",
        $"/api/v1/organizations/{OrganizationId}/members",
        "/api/v1/platform/organizations"
    };

    [Theory]
    [MemberData(nameof(PlanningReads))]
    public async Task AuthenticatedPlanningReadsExecuteTheirEndpointContract(string path)
    {
        var sender = new CapturingSender();
        using var factory = CreateFactory(sender);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var cancellationToken = TestContext.Current.CancellationToken;
        await LoginAsync(client, sender, cancellationToken);

        using var response = await client.GetAsync(path, cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode != HttpStatusCode.InternalServerError,
            $"{path} returned {response.StatusCode}: {body}");
        Assert.True(response.StatusCode != HttpStatusCode.Unauthorized,
            $"{path} returned {response.StatusCode}: {body}");
    }

    [Fact]
    public async Task AuthenticatedPlanningMutationsExecuteTheirOpenApiContract()
    {
        var sender = new CapturingSender();
        using var factory = CreateFactory(sender);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var cancellationToken = TestContext.Current.CancellationToken;
        await LoginAsync(client, sender, cancellationToken);
        var csrf = await GetAntiforgeryAsync(client, cancellationToken);
        var openApiPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../src/FreizeitCockpit.Web/openapi/FreizeitCockpit.Web.json"));
        var document = JsonNode.Parse(await File.ReadAllTextAsync(openApiPath, cancellationToken))!.AsObject();
        var failures = new List<string>();
        var executed = 0;

        foreach (var (path, pathValue) in document["paths"]!.AsObject())
        {
            foreach (var (methodName, operationValue) in pathValue!.AsObject())
            {
                if (methodName is not ("post" or "put" or "patch" or "delete")) continue;
                if (path.StartsWith("/api/v1/auth/", StringComparison.Ordinal)
                    || path.EndsWith("/files", StringComparison.Ordinal)
                    || path.EndsWith("/recipe-files", StringComparison.Ordinal)) continue;
                var requestPath = ReplaceRouteValues(path);
                using var request = new HttpRequestMessage(new HttpMethod(methodName.ToUpperInvariant()), requestPath);
                request.Headers.Add("X-CSRF-TOKEN", csrf);
                request.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
                if (TryCreateRequestBody(document, operationValue!.AsObject(), out var body))
                    request.Content = JsonContent.Create(body);

                using var response = await client.SendAsync(request, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                executed++;
                if (response.StatusCode == HttpStatusCode.InternalServerError)
                    failures.Add($"{methodName.ToUpperInvariant()} {requestPath}: {responseBody}");

                using var missingVersion = new HttpRequestMessage(
                    new HttpMethod(methodName.ToUpperInvariant()), requestPath);
                missingVersion.Headers.Add("X-CSRF-TOKEN", csrf);
                if (body is not null) missingVersion.Content = JsonContent.Create(body.DeepClone());
                using var missingVersionResponse = await client.SendAsync(missingVersion, cancellationToken);
                if (missingVersionResponse.StatusCode == HttpStatusCode.InternalServerError)
                    failures.Add($"{methodName.ToUpperInvariant()} {requestPath} without If-Match returned 500.");

                using var missingAntiforgery = new HttpRequestMessage(
                    new HttpMethod(methodName.ToUpperInvariant()), requestPath);
                missingAntiforgery.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
                if (body is not null) missingAntiforgery.Content = JsonContent.Create(body.DeepClone());
                using var missingAntiforgeryResponse = await client.SendAsync(missingAntiforgery, cancellationToken);
                if (missingAntiforgeryResponse.StatusCode == HttpStatusCode.InternalServerError)
                    failures.Add($"{methodName.ToUpperInvariant()} {requestPath} without antiforgery returned 500.");
            }
        }

        Assert.True(executed >= 45, $"Only {executed} mutation contracts were executed.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static WebApplicationFactory<Program> CreateFactory(CapturingSender sender) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                Replace<IPasswordlessState>(services, PasswordlessTestState.WithMiriam());
                Replace<ILoginCodeSender>(services, sender);
                Replace<IEmailChangeCodeSender>(services);
                Replace<IInvitationSender>(services);
                Replace<IInvitationLifecycle>(services);
                Replace<ITransferableInvitationLinks>(services);
                Replace<IInvitationRegistration>(services);
                Replace<IAccountLifecycle>(services);
                Replace<IEmailChangeLifecycle>(services);
                Replace<IAuthenticationSessionManagement>(services);
                Replace<IPasswordMaintenance>(services);
                Replace<ITenantAccessControl>(services);
                Replace<ITenantAdministration>(services);
                Replace<ICampMemberDirectory>(services);
                Replace<IPlatformAdministration>(services);
                Replace<ICampManagement>(services);
                Replace<ICampPlanningDefaults>(services);
                Replace<ISchedulePlanning>(services);
                Replace<IScheduleReferenceAccess>(services);
                Replace<IOrganizationCateringLibrary>(services);
                Replace<ICampMealPlanning>(services);
                Replace<IMealShoppingSource>(services);
                Replace<IDevotionPlanning>(services);
                Replace<ICampNotebook>(services);
                Replace<IMaterialPlanning>(services);
                Replace<IShoppingPlanning>(services);
                Replace<IShoppingTransfer>(services);
                Replace<IShoppingAudit>(services);
                Replace<IAttachmentCatalog>(services);
                Replace<IAttachmentReader>(services);
                Replace<IActivityJournal>(services);
                Replace<ICampSearchIndex>(services);
                Replace<ICampExportFormatter>(services);
            });
        });

    private static string ReplaceRouteValues(string path) => path
        .Replace("{organizationId}", OrganizationId, StringComparison.Ordinal)
        .Replace("{campId}", CampId, StringComparison.Ordinal)
        .Replace("{invitationId}", ObjectId, StringComparison.Ordinal)
        .Replace("{sessionId}", ObjectId, StringComparison.Ordinal)
        .Replace("{userId}", ObjectId, StringComparison.Ordinal)
        .Replace("{mealId}", ObjectId, StringComparison.Ordinal)
        .Replace("{recipeSnapshotId}", ObjectId, StringComparison.Ordinal)
        .Replace("{devotionId}", ObjectId, StringComparison.Ordinal)
        .Replace("{attachmentId}", ObjectId, StringComparison.Ordinal)
        .Replace("{materialId}", ObjectId, StringComparison.Ordinal)
        .Replace("{listId}", ObjectId, StringComparison.Ordinal)
        .Replace("{itemId}", ObjectId, StringComparison.Ordinal)
        .Replace("{noteId}", ObjectId, StringComparison.Ordinal)
        .Replace("{scheduleEntryId}", ObjectId, StringComparison.Ordinal)
        .Replace("{recipeId}", ObjectId, StringComparison.Ordinal);

    private static bool TryCreateRequestBody(JsonObject document, JsonObject operation, out JsonNode? body)
    {
        body = null;
        if (operation["requestBody"] is not JsonObject requestBody
            || requestBody["content"] is not JsonObject content) return false;
        var mediaType = content.FirstOrDefault(item => item.Key.Contains("json", StringComparison.OrdinalIgnoreCase));
        if (mediaType.Value is not JsonObject media || media["schema"] is not JsonObject schema) return false;
        body = CreateExample(document, schema, 0);
        return true;
    }

    private static JsonNode? CreateExample(JsonObject document, JsonObject schema, int depth)
    {
        if (depth > 12) return null;
        if (schema["type"] is JsonArray declaredTypes
            && declaredTypes.Any(item => item?.GetValue<string>() == "null")) return null;
        if (schema["$ref"] is JsonValue reference)
        {
            var name = reference.GetValue<string>().Split('/')[^1];
            return CreateExample(document, document["components"]!["schemas"]![name]!.AsObject(), depth + 1);
        }
        foreach (var composition in new[] { "oneOf", "anyOf", "allOf" })
        {
            if (schema[composition] is not JsonArray alternatives) continue;
            var selected = alternatives.OfType<JsonObject>().FirstOrDefault(item => !IsNullSchema(item));
            if (selected is not null) return CreateExample(document, selected, depth + 1);
        }
        if (schema["enum"] is JsonArray values && values.Count > 0) return values[0]?.DeepClone();
        var type = GetSchemaType(schema);
        if (type == "object" || schema["properties"] is JsonObject)
        {
            var result = new JsonObject();
            if (schema["properties"] is JsonObject properties)
                foreach (var (name, value) in properties)
                    result[name] = CreateExample(document, value!.AsObject(), depth + 1);
            return result;
        }
        if (type == "array")
        {
            var result = new JsonArray();
            if (schema["minItems"]?.GetValue<int>() is > 0 && schema["items"] is JsonObject items)
                result.Add(CreateExample(document, items, depth + 1));
            return result;
        }
        if (type == "boolean") return true;
        if (type is "integer" or "number") return 1;
        if (type == "string")
        {
            var format = schema["format"]?.GetValue<string>();
            return format switch
            {
                "uuid" => ObjectId,
                "date" => "2027-08-01",
                "date-time" => "2027-08-01T09:00:00Z",
                "email" => "test@example.test",
                _ => "Test"
            };
        }
        return null;
    }

    private static bool IsNullSchema(JsonObject schema)
        => GetSchemaType(schema) == "null";

    private static string? GetSchemaType(JsonObject schema)
    {
        if (schema["type"] is JsonValue value) return value.GetValue<string>();
        if (schema["type"] is JsonArray values)
            return values.Select(item => item?.GetValue<string>()).FirstOrDefault(item => item != "null");
        return null;
    }

    private static void Replace<T>(IServiceCollection services, T instance) where T : class
    {
        services.RemoveAll<T>();
        services.AddSingleton(instance);
    }

    private static void Replace<T>(IServiceCollection services) where T : class
        => Replace(services, DispatchProxy.Create<T, ContractProxy>());

    private static Task LoginAsync(HttpClient client, CapturingSender sender,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        client.DefaultRequestHeaders.Authorization = new(
            "Test",
            "10000000-0000-0000-0000-000000000001");
        return Task.CompletedTask;
    }

    private static async Task<string> GetAntiforgeryAsync(HttpClient client, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync("/api/v1/auth/antiforgery", cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AntiforgeryResponse>(cancellationToken);
        return Assert.IsType<string>(body?.Token);
    }

    private static HttpRequestMessage Post(string path, object body, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", token);
        return request;
    }

    private sealed record AntiforgeryResponse(string Token);

    private sealed class CapturingSender : ILoginCodeSender
    {
        public List<string> Codes { get; } = [];

        public Task SendAsync(string email, string code, DateTimeOffset expiresAt,
            CancellationToken cancellationToken)
        {
            Codes.Add(code);
            return Task.CompletedTask;
        }
    }

    public class ContractProxy : DispatchProxy
    {
        private static readonly Guid StableId = Guid.Parse(ObjectId);

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.DeclaringType == typeof(IMaterialPlanning) && targetMethod.Name == "ListAsync")
                return Task.FromResult<IReadOnlyList<MaterialRequirementSummary>>([]);
            if (targetMethod?.DeclaringType == typeof(IMaterialPlanning) && targetMethod.Name == "GetAsync")
                return Task.FromResult<MaterialRequirement?>(ValidMaterial());
            if (targetMethod?.DeclaringType == typeof(IMaterialPlanning)
                && targetMethod.ReturnType == typeof(Task<MaterialRequirement>))
                return Task.FromResult(ValidMaterial());
            if (targetMethod?.DeclaringType == typeof(IShoppingPlanning) && targetMethod.Name == "ListAsync")
                return Task.FromResult<IReadOnlyList<ShoppingListSummary>>([]);
            if (targetMethod?.DeclaringType == typeof(IShoppingPlanning) && targetMethod.Name == "GetAsync")
                return Task.FromResult<ShoppingList?>(ValidShoppingList());
            if (targetMethod?.DeclaringType == typeof(IShoppingPlanning)
                && targetMethod.ReturnType == typeof(Task<ShoppingList>))
                return Task.FromResult(ValidShoppingList());
            if (targetMethod?.DeclaringType == typeof(IShoppingPlanning)
                && targetMethod.ReturnType == typeof(Task<ShoppingListChange>))
                return Task.FromResult(new ShoppingListChange(StableId, 1, 1, ValidShoppingItem()));
            if (targetMethod?.DeclaringType == typeof(IShoppingTransfer))
                return Task.FromResult(new ShoppingTransferResult(StableId, 1, 1, [ValidShoppingItem()]));
            return CreateValue(targetMethod?.ReturnType ?? typeof(void), 0);
        }

        private static MaterialRequirement ValidMaterial() => new(StableId, StableId, StableId, "Material",
            "Beschreibung", new LogisticsQuantity(1m, LogisticsUnit.Gram), [StableId], "Geschäft", "Notiz",
            ProcurementStatus.Planned, null, 1);

        private static ShoppingList ValidShoppingList() =>
            new(StableId, StableId, StableId, "Einkauf", [ValidShoppingItem()], 1, 1);

        private static ShoppingItem ValidShoppingItem() => new(StableId, StableId, "Position",
            new LogisticsQuantity(1m, LogisticsUnit.Gram), [StableId], "Geschäft", "Notiz",
            new ShoppingItemSource(ShoppingSourceKind.Spontaneous, "Spontan"), false, null, null, 1);

        private static object? CreateValue(Type type, int depth)
        {
            if (type == typeof(void)) return null;
            if (type == typeof(Task)) return Task.CompletedTask;
            if (type == typeof(ValueTask)) return ValueTask.CompletedTask;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var valueType = type.GetGenericArguments()[0];
                return typeof(Task).GetMethod(nameof(Task.FromResult))!
                    .MakeGenericMethod(valueType).Invoke(null, [CreateValue(valueType, depth + 1)]);
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTask<>))
                return Activator.CreateInstance(type, CreateValue(type.GetGenericArguments()[0], depth + 1));
            if (Nullable.GetUnderlyingType(type) is { } nullableType)
                return CreateValue(nullableType, depth + 1);
            if (type == typeof(string)) return "Test";
            if (type == typeof(Guid)) return StableId;
            if (type == typeof(bool)) return true;
            if (type == typeof(int)) return 1;
            if (type == typeof(long)) return 1L;
            if (type == typeof(decimal)) return 1m;
            if (type == typeof(double)) return 1d;
            if (type == typeof(DateOnly)) return new DateOnly(2027, 8, 1);
            if (type == typeof(TimeOnly)) return new TimeOnly(9, 0);
            if (type == typeof(DateTime)) return new DateTime(2027, 8, 1, 9, 0, 0, DateTimeKind.Utc);
            if (type == typeof(DateTimeOffset)) return new DateTimeOffset(2027, 8, 1, 9, 0, 0, TimeSpan.Zero);
            if (type == typeof(TimeSpan)) return TimeSpan.FromMinutes(30);
            if (type == typeof(byte[])) return Array.Empty<byte>();
            if (type == typeof(ReadOnlyMemory<byte>)) return ReadOnlyMemory<byte>.Empty;
            if (type == typeof(Memory<byte>)) return Memory<byte>.Empty;
            if (type == typeof(Quantity)) return new Quantity(1m, MeasurementUnit.Gram);
            if (type == typeof(LogisticsQuantity)) return new LogisticsQuantity(1m, LogisticsUnit.Gram);
            if (type.IsEnum) return Enum.GetValues(type).GetValue(0);
            if (type.IsArray)
            {
                var itemType = type.GetElementType()!;
                var result = Array.CreateInstance(itemType, 1);
                result.SetValue(CreateValue(itemType, depth + 1), 0);
                return result;
            }
            if (TryCreateCollection(type, depth, out var collection)) return collection;
            if (depth > 8) return type.IsValueType ? Activator.CreateInstance(type) : null;
            var constructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public)
                .OrderByDescending(item => item.GetParameters().Length).FirstOrDefault();
            if (constructor is not null)
            {
                var values = constructor.GetParameters()
                    .Select(parameter => CreateValue(parameter.ParameterType, depth + 1)).ToArray();
                try { return constructor.Invoke(values); }
                catch (TargetInvocationException) { }
            }
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        private static bool TryCreateCollection(Type type, int depth, out object? result)
        {
            result = null;
            if (!type.IsGenericType) return false;
            var definition = type.GetGenericTypeDefinition();
            var arguments = type.GetGenericArguments();
            if (definition == typeof(IReadOnlyList<>) || definition == typeof(IList<>)
                || definition == typeof(IEnumerable<>) || definition == typeof(IReadOnlyCollection<>)
                || definition == typeof(ICollection<>))
            {
                var array = Array.CreateInstance(arguments[0], 1);
                array.SetValue(CreateValue(arguments[0], depth + 1), 0);
                result = array;
                return true;
            }
            if (definition == typeof(IReadOnlyDictionary<,>) || definition == typeof(IDictionary<,>))
            {
                var dictionaryType = typeof(Dictionary<,>).MakeGenericType(arguments);
                var dictionary = (IDictionary)Activator.CreateInstance(dictionaryType)!;
                var key = CreateValue(arguments[0], depth + 1)
                    ?? throw new InvalidOperationException("A dictionary key cannot be null.");
                dictionary.Add(key, CreateValue(arguments[1], depth + 1));
                result = dictionary;
                return true;
            }
            return false;
        }
    }
}
