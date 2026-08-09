using System.Text;
using System.Text.Json;
using Activity.Contracts;
using Identity.Contracts;

namespace Activity.Implementation;

public sealed class ActivityService : IActivityJournal, ICampSearchIndex
{
    private readonly IActivityState state;
    private readonly ITenantAccessControl accessControl;

    public ActivityService(ActivityDbContext dbContext, ITenantAccessControl accessControl)
        : this(new EfActivityState(dbContext), accessControl)
    {
    }

    internal ActivityService(IActivityState state, ITenantAccessControl accessControl)
    {
        this.state = state;
        this.accessControl = accessControl;
    }

    public async Task<ActivityEvent> RecordAsync(
        RecordActivity request,
        CancellationToken cancellationToken)
    {
        await EnsureCampAccessAsync(
            request.ActorId,
            request.OrganizationId,
            request.CampId,
            CampAction.WriteContent,
            cancellationToken);
        if (request.Timestamp == default)
        {
            throw Rule("activity_timestamp_required", "Für den Aktivitätseintrag fehlt der Zeitpunkt.");
        }

        var entity = new ActivityEventEntity
        {
            Id = Guid.NewGuid(),
            ActorId = request.ActorId,
            OrganizationId = request.OrganizationId,
            CampId = request.CampId,
            Kind = request.Kind,
            ObjectType = NormalizeText(request.ObjectType, 80, "activity_object_type_required", "Der Objekttyp fehlt."),
            ObjectId = request.ObjectId,
            Title = NormalizeText(request.Title, 160, "activity_title_required", "Der Titel fehlt."),
            Timestamp = request.Timestamp
        };
        state.AddEvent(entity);
        await state.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<IReadOnlyList<ActivityEvent>> ListAsync(
        ActivityQuery request,
        CancellationToken cancellationToken)
    {
        await EnsureCampAccessAsync(
            request.ActorId,
            request.OrganizationId,
            request.CampId,
            CampAction.Read,
            cancellationToken);
        var events = await state.ListEventsAsync(request.OrganizationId, request.CampId, cancellationToken);
        var kinds = request.Kinds?.ToHashSet();
        var objectTypes = request.ObjectTypes?
            .Select(value => NormalizeText(value, 80, "activity_object_type_required", "Der Objekttyp fehlt."))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return events.Where(item =>
                (kinds is null || kinds.Contains(item.Kind)) &&
                (objectTypes is null || objectTypes.Contains(item.ObjectType)) &&
                (request.ActorFilter is null || item.ActorId == request.ActorFilter) &&
                (request.Before is null || item.Timestamp < request.Before))
            .OrderByDescending(item => item.Timestamp)
            .ThenByDescending(item => item.Id)
            .Take(Math.Clamp(request.Limit, 1, 200))
            .Select(Map)
            .ToList();
    }

    public async Task<SearchProjectionResult> UpsertAsync(
        UpsertSearchDocument request,
        CancellationToken cancellationToken)
    {
        await EnsureCampAccessAsync(
            request.ActorId,
            request.OrganizationId,
            request.CampId,
            CampAction.WriteContent,
            cancellationToken);
        EnsureSourceVersion(request.SourceVersion);
        var objectType = NormalizeText(
            request.ObjectType,
            80,
            "search_object_type_required",
            "Der Objekttyp fehlt.");
        var title = NormalizeText(request.Title, 160, "search_title_required", "Der Titel fehlt.");
        var searchText = NormalizeOptionalText(request.SearchText, 2000);
        var metadata = NormalizeMetadata(request.Metadata);
        var metadataJson = SerializeMetadata(metadata);
        var existing = await state.FindSearchDocumentAsync(
            request.OrganizationId,
            request.CampId,
            objectType,
            request.ObjectId,
            cancellationToken);

        if (existing is null)
        {
            existing = new SearchDocumentEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId = request.OrganizationId,
                CampId = request.CampId,
                ObjectType = objectType,
                ObjectId = request.ObjectId,
                Title = title,
                SearchText = searchText,
                MetadataJson = metadataJson,
                SourceVersion = request.SourceVersion,
                UpdatedAt = request.Timestamp
            };
            state.AddSearchDocument(existing);
            await SaveAsync(cancellationToken);
            return MapProjection(existing, true);
        }

        if (request.SourceVersion < existing.SourceVersion)
        {
            return MapProjection(existing, false);
        }

        if (request.SourceVersion == existing.SourceVersion)
        {
            if (!existing.IsRemoved &&
                existing.Title == title &&
                existing.SearchText == searchText &&
                existing.MetadataJson == metadataJson &&
                existing.UpdatedAt == request.Timestamp)
            {
                return MapProjection(existing, false);
            }

            throw Rule("source_version_conflict", "Für diese Quellversion liegen widersprüchliche Suchdaten vor.");
        }

        existing.Title = title;
        existing.SearchText = searchText;
        existing.MetadataJson = metadataJson;
        existing.SourceVersion = request.SourceVersion;
        existing.IsRemoved = false;
        existing.UpdatedAt = request.Timestamp;
        existing.Version++;
        await SaveAsync(cancellationToken);
        return MapProjection(existing, true);
    }

    public async Task<SearchProjectionResult> RemoveAsync(
        RemoveSearchDocument request,
        CancellationToken cancellationToken)
    {
        await EnsureCampAccessAsync(
            request.ActorId,
            request.OrganizationId,
            request.CampId,
            CampAction.WriteContent,
            cancellationToken);
        EnsureSourceVersion(request.SourceVersion);
        var objectType = NormalizeText(
            request.ObjectType,
            80,
            "search_object_type_required",
            "Der Objekttyp fehlt.");
        var existing = await state.FindSearchDocumentAsync(
            request.OrganizationId,
            request.CampId,
            objectType,
            request.ObjectId,
            cancellationToken);
        if (existing is null)
        {
            existing = new SearchDocumentEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId = request.OrganizationId,
                CampId = request.CampId,
                ObjectType = objectType,
                ObjectId = request.ObjectId,
                Title = string.Empty,
                SearchText = string.Empty,
                MetadataJson = "{}",
                SourceVersion = request.SourceVersion,
                IsRemoved = true,
                UpdatedAt = request.Timestamp
            };
            state.AddSearchDocument(existing);
            await SaveAsync(cancellationToken);
            return MapProjection(existing, true);
        }

        if (request.SourceVersion < existing.SourceVersion ||
            (request.SourceVersion == existing.SourceVersion && existing.IsRemoved))
        {
            return MapProjection(existing, false);
        }

        if (request.SourceVersion == existing.SourceVersion)
        {
            throw Rule("source_version_conflict", "Für diese Quellversion liegen widersprüchliche Suchdaten vor.");
        }

        existing.Title = string.Empty;
        existing.SearchText = string.Empty;
        existing.MetadataJson = "{}";
        existing.SourceVersion = request.SourceVersion;
        existing.IsRemoved = true;
        existing.UpdatedAt = request.Timestamp;
        existing.Version++;
        await SaveAsync(cancellationToken);
        return MapProjection(existing, true);
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        CampSearchQuery request,
        CancellationToken cancellationToken)
    {
        await EnsureCampAccessAsync(
            request.ActorId,
            request.OrganizationId,
            request.CampId,
            CampAction.Read,
            cancellationToken);
        var documents = await state.ListSearchDocumentsAsync(
            request.OrganizationId,
            request.CampId,
            cancellationToken);
        var query = NormalizeOptionalText(request.Query, 200).ToUpperInvariant();
        var objectTypes = request.ObjectTypes?
            .Select(item => NormalizeText(item, 80, "search_object_type_required", "Der Objekttyp fehlt."))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var metadataFilters = NormalizeMetadata(request.MetadataFilters ?? new Dictionary<string, string>());
        return documents.Where(document =>
            (query.Length == 0 ||
             document.Title.ToUpperInvariant().Contains(query, StringComparison.Ordinal) ||
             document.SearchText.ToUpperInvariant().Contains(query, StringComparison.Ordinal)) &&
            (objectTypes is null || objectTypes.Contains(document.ObjectType)) &&
            ContainsMetadata(DeserializeMetadata(document.MetadataJson), metadataFilters))
            .OrderByDescending(document => document.UpdatedAt)
            .ThenBy(document => document.Title, StringComparer.CurrentCultureIgnoreCase)
            .Take(Math.Clamp(request.Limit, 1, 100))
            .Select(document => new SearchResult(
                document.ObjectType,
                document.ObjectId,
                document.Title,
                DeserializeMetadata(document.MetadataJson),
                document.UpdatedAt,
                document.Version))
            .ToList();
    }

    private async Task EnsureCampAccessAsync(
        Guid actorId,
        Guid organizationId,
        Guid campId,
        CampAction action,
        CancellationToken cancellationToken)
    {
        var decision = await accessControl.AuthorizeCampAsync(
            new CampAccessRequest(actorId, organizationId, campId, action),
            cancellationToken);
        if (!decision.Allowed)
        {
            throw Rule("access_denied", "Für diese Aktion fehlt die Berechtigung.");
        }
    }

    private static ActivityEvent Map(ActivityEventEntity entity) =>
        new(
            entity.Id,
            entity.ActorId,
            entity.OrganizationId,
            entity.CampId,
            entity.Kind,
            entity.ObjectType,
            entity.ObjectId,
            entity.Title,
            entity.Timestamp,
            entity.Version);

    private static SearchProjectionResult MapProjection(SearchDocumentEntity entity, bool applied) =>
        new(entity.ObjectType, entity.ObjectId, entity.SourceVersion, entity.Version, applied, entity.IsRemoved);

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await state.SaveChangesAsync(cancellationToken);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            throw Rule("concurrency_conflict", "Die Suchprojektion wurde zwischenzeitlich geändert.");
        }
    }

    private static void EnsureSourceVersion(long sourceVersion)
    {
        if (sourceVersion <= 0)
        {
            throw Rule("invalid_source_version", "Die Quellversion muss größer als null sein.");
        }
    }

    private static Dictionary<string, string> NormalizeMetadata(IReadOnlyDictionary<string, string> metadata)
    {
        if (metadata.Count > 12)
        {
            throw Rule("too_many_metadata_fields", "Es sind höchstens zwölf Metadatenfelder erlaubt.");
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in metadata)
        {
            var key = NormalizeText(pair.Key, 40, "metadata_key_required", "Ein Metadatenschlüssel fehlt.");
            var value = NormalizeText(pair.Value, 100, "metadata_value_required", "Ein Metadatenwert fehlt.");
            if (!result.TryAdd(key.ToUpperInvariant(), value))
            {
                throw Rule("duplicate_metadata_key", "Ein Metadatenschlüssel wurde mehrfach angegeben.");
            }
        }

        return result;
    }

    private static bool ContainsMetadata(
        Dictionary<string, string> document,
        Dictionary<string, string> filters) =>
        filters.All(filter =>
            document.TryGetValue(filter.Key, out var value) &&
            string.Equals(value, filter.Value, StringComparison.OrdinalIgnoreCase));

    private static string SerializeMetadata(Dictionary<string, string> metadata) =>
        JsonSerializer.Serialize(new SortedDictionary<string, string>(metadata, StringComparer.Ordinal));

    private static Dictionary<string, string> DeserializeMetadata(string value) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(value) ?? new Dictionary<string, string>();

    private static string NormalizeOptionalText(string value, int maxLength)
    {
        var normalized = string.Join(' ', value.Normalize(NormalizationForm.FormKC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length > maxLength)
        {
            throw Rule("search_text_too_long", $"Der Suchtext darf höchstens {maxLength} Zeichen lang sein.");
        }

        return normalized;
    }

    private static string NormalizeText(string value, int maxLength, string emptyCode, string emptyMessage)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasWhitespace = true;
        foreach (var character in value.Normalize(NormalizationForm.FormKC))
        {
            if (char.IsControl(character) && !char.IsWhiteSpace(character))
            {
                throw Rule("invalid_metadata", "Die Metadaten enthalten nicht erlaubte Steuerzeichen.");
            }

            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                }

                previousWasWhitespace = true;
                continue;
            }

            builder.Append(character);
            previousWasWhitespace = false;
        }

        var normalized = builder.ToString().TrimEnd();
        if (normalized.Length == 0)
        {
            throw Rule(emptyCode, emptyMessage);
        }

        if (normalized.Length > maxLength)
        {
            throw Rule("metadata_too_long", $"Die Metadaten dürfen höchstens {maxLength} Zeichen lang sein.");
        }

        return normalized;
    }

    private static ActivityRuleException Rule(string code, string message) => new(code, message);
}
