using Knowledge.Contracts;
using Knowledge.Implementation;
using Xunit;

namespace Knowledge.Tests;

public sealed class SafeMarkdownProcessorTests
{
    [Theory]
    [InlineData("# Überschrift")]
    [InlineData("###### Überschrift")]
    [InlineData("#")]
    [InlineData("####### Keine Überschrift")]
    [InlineData("#Keine Überschrift")]
    [InlineData("+ Punkt\n* Punkt\n- Punkt")]
    [InlineData("12. Punkt\n1.Punkt\n. Punkt")]
    [InlineData("Unvollständig **fett und *kursiv")]
    [InlineData("[ohne Ziel]\n[ohne Ende](https://example.test")]
    [InlineData("<\n<3\nText!")]
    [InlineData("A | B\n-- | ---")]
    [InlineData("A | B\n--- | x--")]
    [InlineData("A | B\n---")]
    public void SafeSubsetHandlesBoundarySyntaxWithoutProducingUnsafeHtml(string markdown)
    {
        var result = SafeMarkdownProcessor.Process(markdown);

        Assert.DoesNotContain("<script", result.RenderedHtml, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.PlainText);
    }

    [Theory]
    [InlineData("markdown_too_long", null)]
    [InlineData("invalid_markdown", "Text\u0001")]
    [InlineData("raw_html_not_allowed", "<!doctype html>")]
    [InlineData("raw_html_not_allowed", "<?xml version='1.0'?>")]
    [InlineData("markdown_link_not_allowed", "[leer](https:///pfad)")]
    [InlineData("markdown_link_not_allowed", "[relativ](/pfad)")]
    public void UnsafeBoundarySyntaxIsRejected(string expectedCode, string? markdown)
    {
        markdown ??= new string('a', 50_001);

        var exception = Assert.Throws<KnowledgeRuleException>(() =>
            SafeMarkdownProcessor.Process(markdown));

        Assert.Equal(expectedCode, exception.ErrorCode);
    }
}
