using System.Net;
using System.Text;
using System.Xml.Linq;
using Knowledge.Contracts;

namespace Knowledge.Implementation;

internal sealed record MarkdownResult(string RenderedHtml, string PlainText);

internal static class SafeMarkdownProcessor
{
    private static readonly HashSet<string> AllowedElements = new(StringComparer.Ordinal)
    {
        "p", "h1", "h2", "h3", "h4", "h5", "h6", "strong", "em", "ul", "ol", "li", "a"
    };

    public static MarkdownResult Process(string markdown)
    {
        if (markdown.Length > 50_000)
        {
            throw Rule("markdown_too_long", "Der Notiztext darf höchstens 50.000 Zeichen lang sein.");
        }

        if (markdown.Any(character => char.IsControl(character) && character is not '\r' and not '\n' and not '\t'))
        {
            throw Rule("invalid_markdown", "Der Notiztext enthält nicht erlaubte Steuerzeichen.");
        }

        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var html = new StringBuilder(markdown.Length + 64);
        var plainText = new StringBuilder(markdown.Length);
        var paragraph = new List<string>();
        string? openList = null;

        void FlushParagraph()
        {
            if (paragraph.Count == 0)
            {
                return;
            }

            var inline = RenderInline(string.Join(' ', paragraph));
            html.Append("<p>").Append(inline.Html).Append("</p>");
            AppendPlainText(plainText, inline.Text);
            paragraph.Clear();
        }

        void CloseList()
        {
            if (openList is null)
            {
                return;
            }

            html.Append("</").Append(openList).Append('>');
            openList = null;
        }

        foreach (var sourceLine in lines)
        {
            var line = sourceLine.TrimEnd();
            if (IsTableSeparator(line))
            {
                throw Rule("markdown_table_not_allowed", "Tabellen sind in Notizen nicht erlaubt.");
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                FlushParagraph();
                CloseList();
                continue;
            }

            if (TryHeading(line, out var level, out var heading))
            {
                FlushParagraph();
                CloseList();
                var inline = RenderInline(heading);
                html.Append("<h").Append(level).Append('>').Append(inline.Html).Append("</h").Append(level).Append('>');
                AppendPlainText(plainText, inline.Text);
                continue;
            }

            if (TryListItem(line, out var listType, out var item))
            {
                FlushParagraph();
                if (!string.Equals(openList, listType, StringComparison.Ordinal))
                {
                    CloseList();
                    html.Append('<').Append(listType).Append('>');
                    openList = listType;
                }

                var inline = RenderInline(item);
                html.Append("<li>").Append(inline.Html).Append("</li>");
                AppendPlainText(plainText, inline.Text);
                continue;
            }

            CloseList();
            paragraph.Add(line.Trim());
        }

        FlushParagraph();
        CloseList();
        return new MarkdownResult(Sanitize(html.ToString()), plainText.ToString());
    }

    private static InlineResult RenderInline(string value)
    {
        var html = new StringBuilder(value.Length + 16);
        var text = new StringBuilder(value.Length);
        var index = 0;
        while (index < value.Length)
        {
            if (value.AsSpan(index).StartsWith("![", StringComparison.Ordinal))
            {
                throw Rule("markdown_image_not_allowed", "Bilder können nicht in Notiztext eingebettet werden.");
            }

            if (value[index] == '<' && IsHtmlStart(value, index + 1))
            {
                throw Rule("raw_html_not_allowed", "HTML ist in Notizen nicht erlaubt.");
            }

            if (value.AsSpan(index).StartsWith("**", StringComparison.Ordinal))
            {
                var end = value.IndexOf("**", index + 2, StringComparison.Ordinal);
                if (end >= 0)
                {
                    var nested = RenderInline(value[(index + 2)..end]);
                    html.Append("<strong>").Append(nested.Html).Append("</strong>");
                    text.Append(nested.Text);
                    index = end + 2;
                    continue;
                }
            }

            if (value[index] == '*')
            {
                var end = value.IndexOf('*', index + 1);
                if (end >= 0)
                {
                    var nested = RenderInline(value[(index + 1)..end]);
                    html.Append("<em>").Append(nested.Html).Append("</em>");
                    text.Append(nested.Text);
                    index = end + 1;
                    continue;
                }
            }

            if (value[index] == '[' && TryLink(value, index, out var label, out var target, out var nextIndex))
            {
                if (!Uri.TryCreate(target, UriKind.Absolute, out var uri) ||
                    !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(uri.Host))
                {
                    throw Rule("markdown_link_not_allowed", "Links in Notizen müssen sichere HTTPS-Adressen verwenden.");
                }

                var nested = RenderInline(label);
                html.Append("<a href=\"")
                    .Append(WebUtility.HtmlEncode(uri.AbsoluteUri))
                    .Append("\" rel=\"noopener noreferrer\">")
                    .Append(nested.Html)
                    .Append("</a>");
                text.Append(nested.Text);
                index = nextIndex;
                continue;
            }

            var nextSpecial = FindNextSpecial(value, index + 1);
            var segment = value[index..nextSpecial];
            html.Append(WebUtility.HtmlEncode(segment));
            text.Append(segment);
            index = nextSpecial;
        }

        return new InlineResult(html.ToString(), text.ToString());
    }

    private static string Sanitize(string renderedHtml)
    {
        var root = XElement.Parse($"<root>{renderedHtml}</root>", LoadOptions.PreserveWhitespace);
        foreach (var element in root.Descendants().ToList())
        {
            if (!AllowedElements.Contains(element.Name.LocalName))
            {
                element.ReplaceWith(new XText(element.Value));
                continue;
            }

            if (element.Name.LocalName == "a")
            {
                var href = element.Attribute("href")?.Value;
                if (!Uri.TryCreate(href, UriKind.Absolute, out var uri) ||
                    !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    element.ReplaceWith(new XText(element.Value));
                    continue;
                }

                element.RemoveAttributes();
                element.SetAttributeValue("href", uri.AbsoluteUri);
                element.SetAttributeValue("rel", "noopener noreferrer");
                continue;
            }

            element.RemoveAttributes();
        }

        return string.Concat(root.Nodes().Select(node => node.ToString(SaveOptions.DisableFormatting)));
    }

    private static bool TryHeading(string line, out int level, out string content)
    {
        level = 0;
        while (level < line.Length && level < 6 && line[level] == '#')
        {
            level++;
        }

        if (level == 0 || level >= line.Length || line[level] != ' ')
        {
            content = string.Empty;
            return false;
        }

        content = line[(level + 1)..].Trim();
        return true;
    }

    private static bool TryListItem(string line, out string listType, out string content)
    {
        var trimmed = line.TrimStart();
        if (trimmed.Length >= 2 && trimmed[1] == ' ' && trimmed[0] is '-' or '*' or '+')
        {
            listType = "ul";
            content = trimmed[2..].Trim();
            return true;
        }

        var digitCount = 0;
        while (digitCount < trimmed.Length && char.IsAsciiDigit(trimmed[digitCount]))
        {
            digitCount++;
        }

        if (digitCount > 0 && digitCount + 1 < trimmed.Length && trimmed[digitCount] == '.' && trimmed[digitCount + 1] == ' ')
        {
            listType = "ol";
            content = trimmed[(digitCount + 2)..].Trim();
            return true;
        }

        listType = string.Empty;
        content = string.Empty;
        return false;
    }

    private static bool IsTableSeparator(string line)
    {
        var trimmed = line.Trim().Trim('|');
        if (!trimmed.Contains('|', StringComparison.Ordinal))
        {
            return false;
        }

        var cells = trimmed.Split('|');
        return cells.Length >= 2 && cells.All(IsTableSeparatorCell);
    }

    private static bool IsTableSeparatorCell(string value)
    {
        var cell = value.Trim().Trim(':');
        return cell.Length >= 3 && cell.All(character => character == '-');
    }

    private static bool IsHtmlStart(string value, int index) =>
        index < value.Length && (char.IsLetter(value[index]) || value[index] is '/' or '!' or '?');

    private static bool TryLink(
        string value,
        int start,
        out string label,
        out string target,
        out int nextIndex)
    {
        var labelEnd = value.IndexOf("](", start + 1, StringComparison.Ordinal);
        if (labelEnd < 0)
        {
            label = string.Empty;
            target = string.Empty;
            nextIndex = start;
            return false;
        }

        var targetEnd = value.IndexOf(')', labelEnd + 2);
        if (targetEnd < 0)
        {
            label = string.Empty;
            target = string.Empty;
            nextIndex = start;
            return false;
        }

        label = value[(start + 1)..labelEnd];
        target = value[(labelEnd + 2)..targetEnd];
        nextIndex = targetEnd + 1;
        return true;
    }

    private static int FindNextSpecial(string value, int start)
    {
        for (var index = start; index < value.Length; index++)
        {
            if (value[index] is '<' or '*' or '[' or '!')
            {
                return index;
            }
        }

        return value.Length;
    }

    private static void AppendPlainText(StringBuilder builder, string value)
    {
        if (builder.Length > 0 && value.Length > 0)
        {
            builder.Append(' ');
        }

        builder.Append(value);
    }

    private static KnowledgeRuleException Rule(string code, string message) => new(code, message);

    private sealed record InlineResult(string Html, string Text);
}
