namespace Spiritual.Contracts;

public interface IBiblePassageProvider
{
    Task<BiblePassageFetchResult> FetchAsync(
        BiblePassageRequest request,
        CancellationToken cancellationToken);
}

public sealed record RefreshBibleSnapshot(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid DevotionId,
    long ExpectedVersion);

public sealed record SaveManualBibleSnapshot(
    Guid ActorId,
    Guid OrganizationId,
    Guid CampId,
    Guid DevotionId,
    string Reference,
    BibleTranslation Translation,
    string TextExcerpt,
    long ExpectedVersion);

public sealed record BibleSnapshot(
    string Reference,
    string TextExcerpt,
    string TechnicalTranslationId,
    string TranslationDisplayName,
    string License,
    string Attribution,
    DateTimeOffset RetrievedAt,
    BibleSnapshotOrigin Origin);

public sealed record BibleTranslationView(
    BibleTranslation Translation,
    string TechnicalId,
    string DisplayName,
    string License,
    string Attribution,
    bool IsDefault);

public sealed record BibleSnapshotRefreshResult(
    BibleSnapshotRefreshStatus Status,
    DevotionDetails Devotion);

public sealed record BiblePassageRequest(BibleTranslation Translation, string Reference);

public sealed record BiblePassage(
    string Reference,
    string TextExcerpt,
    string TechnicalTranslationId,
    string TranslationDisplayName,
    string License,
    string Attribution,
    DateTimeOffset RetrievedAt);

public sealed record BiblePassageFetchResult(
    BiblePassageFetchStatus Status,
    BiblePassage? Passage)
{
    public static BiblePassageFetchResult Found(BiblePassage passage) =>
        new(BiblePassageFetchStatus.Found, passage);

    public static BiblePassageFetchResult ReferenceNotFound() =>
        new(BiblePassageFetchStatus.ReferenceNotFound, null);

    public static BiblePassageFetchResult Unavailable() =>
        new(BiblePassageFetchStatus.Unavailable, null);

    public static BiblePassageFetchResult TimedOut() =>
        new(BiblePassageFetchStatus.TimedOut, null);
}

public enum BibleTranslation
{
    Schlachter1951,
    Luther1912,
    ElberfelderUnrevised,
    Textbibel
}

public enum BibleSnapshotOrigin
{
    Provider,
    Manual
}

public enum BibleSnapshotRefreshStatus
{
    Refreshed,
    ReferenceNotFound,
    ProviderUnavailable,
    TimedOut
}

public enum BiblePassageFetchStatus
{
    Found,
    ReferenceNotFound,
    Unavailable,
    TimedOut
}
