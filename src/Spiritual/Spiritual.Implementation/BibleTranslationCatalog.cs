using Spiritual.Contracts;

namespace Spiritual.Implementation;

internal static class BibleTranslationCatalog
{
    private const string ProviderAttribution = "Bereitgestellt durch eBible.org und Free Use Bible API.";

    private static readonly IReadOnlyList<BibleTranslationDefinition> Definitions =
    [
        new(
            BibleTranslation.Schlachter1951,
            "deu1951",
            "deu_sch",
            "Schlachter 1951",
            "Creative Commons Attribution 4.0 (CC BY 4.0)",
            "© 1951 Genfer Bibelgesellschaft. " + ProviderAttribution,
            true),
        new(
            BibleTranslation.Luther1912,
            "deu1912",
            "deu_l12",
            "Luther 1912",
            "Public Domain",
            "Lutherbibel 1912 (Martin Luther). " + ProviderAttribution,
            false),
        new(
            BibleTranslation.ElberfelderUnrevised,
            "deuelo",
            "deu_elo",
            "Unrevidierte Elberfelder",
            "Public Domain",
            "Darby Unrevidierte Elberfelder. " + ProviderAttribution,
            false),
        new(
            BibleTranslation.Textbibel,
            "deutkw",
            "deu_tkw",
            "Textbibel",
            "Public Domain",
            "Textbibel von Kautzsch und Weizsäcker. " + ProviderAttribution,
            false)
    ];

    public static IReadOnlyList<BibleTranslationView> Views { get; } = Array.AsReadOnly(
        Definitions.Select(item => item.ToView()).ToArray());

    public static BibleTranslationDefinition Get(BibleTranslation translation) =>
        Definitions.SingleOrDefault(item => item.Translation == translation)
        ?? throw new SpiritualRuleException(
            "translation_not_supported",
            "Diese Bibelübersetzung wird nicht unterstützt.");
}

internal sealed record BibleTranslationDefinition(
    BibleTranslation Translation,
    string TechnicalId,
    string ProviderId,
    string DisplayName,
    string License,
    string Attribution,
    bool IsDefault)
{
    public BibleTranslationView ToView() => new(
        Translation,
        TechnicalId,
        DisplayName,
        License,
        Attribution,
        IsDefault);
}
