using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Spiritual.Contracts;

namespace Spiritual.Implementation;

public sealed class HttpBiblePassageProvider(
    HttpClient httpClient,
    TimeProvider timeProvider) : IBiblePassageProvider
{
    public async Task<BiblePassageFetchResult> FetchAsync(
        BiblePassageRequest request,
        CancellationToken cancellationToken)
    {
        if (!BibleReferenceParser.TryParse(request.Reference, out var reference))
        {
            return BiblePassageFetchResult.ReferenceNotFound();
        }

        var translation = BibleTranslationCatalog.Get(request.Translation);
        try
        {
            var path = FormattableString.Invariant(
                $"api/{translation.ProviderId}/{reference.BookId}/{reference.Chapter}.json");
            using var response = await httpClient.GetAsync(
                path,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return BiblePassageFetchResult.ReferenceNotFound();
            }
            if (response.StatusCode == HttpStatusCode.RequestTimeout)
            {
                return BiblePassageFetchResult.TimedOut();
            }
            if (!response.IsSuccessStatusCode)
            {
                return response.StatusCode == HttpStatusCode.BadRequest
                    ? BiblePassageFetchResult.ReferenceNotFound()
                    : BiblePassageFetchResult.Unavailable();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);
            var text = ReadPassage(document.RootElement, reference);
            if (text is null)
            {
                return BiblePassageFetchResult.ReferenceNotFound();
            }

            return BiblePassageFetchResult.Found(new BiblePassage(
                request.Reference.Trim(),
                text,
                translation.TechnicalId,
                translation.DisplayName,
                translation.License,
                translation.Attribution,
                timeProvider.GetUtcNow()));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return BiblePassageFetchResult.TimedOut();
        }
        catch (HttpRequestException)
        {
            return BiblePassageFetchResult.Unavailable();
        }
        catch (JsonException)
        {
            return BiblePassageFetchResult.Unavailable();
        }
    }

    private static string? ReadPassage(JsonElement root, ParsedBibleReference reference)
    {
        if (!root.TryGetProperty("chapter", out var chapter)
            || !chapter.TryGetProperty("content", out var content)
            || content.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var verses = new List<string>();
        foreach (var entry in content.EnumerateArray())
        {
            if (!entry.TryGetProperty("type", out var type)
                || type.GetString() != "verse"
                || !entry.TryGetProperty("number", out var numberElement)
                || !numberElement.TryGetInt32(out var number)
                || number < reference.FirstVerse
                || number > reference.LastVerse)
            {
                continue;
            }

            if (entry.TryGetProperty("content", out var verseContent))
            {
                var value = FlattenText(verseContent);
                if (value.Length > 0)
                {
                    verses.Add($"{number} {value}");
                }
            }
        }
        return verses.Count > 0 ? string.Join(" ", verses) : null;
    }

    private static string FlattenText(JsonElement element)
    {
        var values = new List<string>();
        CollectText(element, values);
        return string.Join(" ", values.Where(value => value.Length > 0));
    }

    private static void CollectText(JsonElement element, List<string> values)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                values.Add(element.GetString()?.Trim() ?? string.Empty);
                break;
            case JsonValueKind.Array:
                foreach (var child in element.EnumerateArray())
                {
                    CollectText(child, values);
                }
                break;
            case JsonValueKind.Object:
                if (element.TryGetProperty("text", out var text))
                {
                    CollectText(text, values);
                }
                break;
        }
    }
}

internal static class BibleReferenceParser
{
    private const string Pattern =
        "^(?<book>.+?)\\s+(?<chapter>[0-9]+)(?:\\s*[:,]\\s*(?<first>[0-9]+)(?:\\s*[-–]\\s*(?<last>[0-9]+))?)?$";

    private static readonly Dictionary<string, string> BookIds = BuildBookIds();

    public static bool TryParse(string value, out ParsedBibleReference reference)
    {
        var match = Regex.Match(
            value.Trim(),
            Pattern,
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
        if (!match.Success
            || !int.TryParse(match.Groups["chapter"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var chapter)
            || chapter < 1)
        {
            reference = default;
            return false;
        }

        var bookKey = NormalizeBookName(match.Groups["book"].Value);
        if (!BookIds.TryGetValue(bookKey, out var bookId))
        {
            reference = default;
            return false;
        }

        var first = 1;
        var last = int.MaxValue;
        if (match.Groups["first"].Success)
        {
            if (!int.TryParse(match.Groups["first"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out first)
                || first < 1)
            {
                reference = default;
                return false;
            }
            last = first;
        }
        if (match.Groups["last"].Success
            && (!int.TryParse(match.Groups["last"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out last)
                || last < first))
        {
            reference = default;
            return false;
        }

        reference = new ParsedBibleReference(bookId, chapter, first, last);
        return true;
    }

    private static Dictionary<string, string> BuildBookIds()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        Add(values, "GEN", "Genesis", "1 Mose", "1. Mose");
        Add(values, "EXO", "Exodus", "2 Mose", "2. Mose");
        Add(values, "LEV", "Levitikus", "3 Mose", "3. Mose");
        Add(values, "NUM", "Numeri", "4 Mose", "4. Mose");
        Add(values, "DEU", "Deuteronomium", "5 Mose", "5. Mose");
        Add(values, "JOS", "Josua");
        Add(values, "JDG", "Richter");
        Add(values, "RUT", "Rut", "Ruth");
        Add(values, "1SA", "1 Samuel", "1. Samuel");
        Add(values, "2SA", "2 Samuel", "2. Samuel");
        Add(values, "1KI", "1 Könige", "1. Könige");
        Add(values, "2KI", "2 Könige", "2. Könige");
        Add(values, "1CH", "1 Chronik", "1. Chronik");
        Add(values, "2CH", "2 Chronik", "2. Chronik");
        Add(values, "EZR", "Esra");
        Add(values, "NEH", "Nehemia");
        Add(values, "EST", "Ester", "Esther");
        Add(values, "JOB", "Hiob", "Ijob");
        Add(values, "PSA", "Psalm", "Psalmen");
        Add(values, "PRO", "Sprüche", "Sprueche");
        Add(values, "ECC", "Prediger", "Kohelet");
        Add(values, "SNG", "Hohelied");
        Add(values, "ISA", "Jesaja");
        Add(values, "JER", "Jeremia");
        Add(values, "LAM", "Klagelieder");
        Add(values, "EZK", "Hesekiel", "Ezechiel");
        Add(values, "DAN", "Daniel");
        Add(values, "HOS", "Hosea");
        Add(values, "JOL", "Joel");
        Add(values, "AMO", "Amos");
        Add(values, "OBA", "Obadja");
        Add(values, "JON", "Jona");
        Add(values, "MIC", "Micha");
        Add(values, "NAM", "Nahum");
        Add(values, "HAB", "Habakuk");
        Add(values, "ZEP", "Zefanja", "Zephanja");
        Add(values, "HAG", "Haggai");
        Add(values, "ZEC", "Sacharja");
        Add(values, "MAL", "Maleachi");
        Add(values, "MAT", "Matthäus", "Matthaeus");
        Add(values, "MRK", "Markus");
        Add(values, "LUK", "Lukas");
        Add(values, "JHN", "Johannes");
        Add(values, "ACT", "Apostelgeschichte");
        Add(values, "ROM", "Römer", "Roemer");
        Add(values, "1CO", "1 Korinther", "1. Korinther");
        Add(values, "2CO", "2 Korinther", "2. Korinther");
        Add(values, "GAL", "Galater");
        Add(values, "EPH", "Epheser");
        Add(values, "PHP", "Philipper");
        Add(values, "COL", "Kolosser");
        Add(values, "1TH", "1 Thessalonicher", "1. Thessalonicher");
        Add(values, "2TH", "2 Thessalonicher", "2. Thessalonicher");
        Add(values, "1TI", "1 Timotheus", "1. Timotheus");
        Add(values, "2TI", "2 Timotheus", "2. Timotheus");
        Add(values, "TIT", "Titus");
        Add(values, "PHM", "Philemon");
        Add(values, "HEB", "Hebräer", "Hebraeer");
        Add(values, "JAS", "Jakobus");
        Add(values, "1PE", "1 Petrus", "1. Petrus");
        Add(values, "2PE", "2 Petrus", "2. Petrus");
        Add(values, "1JN", "1 Johannes", "1. Johannes");
        Add(values, "2JN", "2 Johannes", "2. Johannes");
        Add(values, "3JN", "3 Johannes", "3. Johannes");
        Add(values, "JUD", "Judas");
        Add(values, "REV", "Offenbarung");
        return values;
    }

    private static void Add(Dictionary<string, string> values, string id, params string[] names)
    {
        values[NormalizeBookName(id)] = id;
        foreach (var name in names)
        {
            values[NormalizeBookName(name)] = id;
        }
    }

    private static string NormalizeBookName(string value)
    {
        var normalized = value.Trim().ToLowerInvariant()
            .Replace("ä", "ae", StringComparison.Ordinal)
            .Replace("ö", "oe", StringComparison.Ordinal)
            .Replace("ü", "ue", StringComparison.Ordinal)
            .Replace("ß", "ss", StringComparison.Ordinal);
        var result = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                result.Append(character);
            }
        }
        return result.ToString();
    }
}

internal readonly record struct ParsedBibleReference(
    string BookId,
    int Chapter,
    int FirstVerse,
    int LastVerse);
