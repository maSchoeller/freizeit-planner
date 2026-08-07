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

app.Run();
