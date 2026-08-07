var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/api/available_translations.json", () => Results.Ok(new[]
{
    new { id = "deu1951", name = "Schlachter 1951" },
    new { id = "deu1912", name = "Luther 1912" },
    new { id = "deuelo", name = "Unrevidierte Elberfelder" },
    new { id = "deutkw", name = "Textbibel" }
}));
app.MapGet("/api/{translation}/{book}/{chapter:int}.json", (
    string translation,
    string book,
    int chapter) =>
{
    var knownTranslations = new HashSet<string>(StringComparer.Ordinal)
    {
        "deu_sch",
        "deu_l12",
        "deu_elo",
        "deu_tkw"
    };
    if (!knownTranslations.Contains(translation) || chapter < 1 || chapter > 150)
    {
        return Results.NotFound();
    }

    var content = Enumerable.Range(1, 50).Select(verse => new
    {
        type = "verse",
        number = verse,
        content = new[] { new { text = $"Deterministischer Bibeltext {book} {chapter},{verse}." } }
    });
    return Results.Ok(new
    {
        translation,
        chapter = new
        {
            id = $"{book}.{chapter}",
            content
        }
    });
});

app.Run();
