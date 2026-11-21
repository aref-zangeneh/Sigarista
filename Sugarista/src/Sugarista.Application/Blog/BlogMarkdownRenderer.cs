using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sugarista.Application.Blog;

public sealed record BlogTocItem(string Id, string Text, int Level);

public sealed record BlogFaqItem(string Question, string Answer);

public sealed record BlogRenderedContent(
    string Html,
    IReadOnlyList<BlogTocItem> Toc,
    int WordCount,
    int ReadingTimeMinutes);

public static partial class BlogMarkdownRenderer
{
    private static readonly JsonSerializerOptions FaqJsonOptions = new(JsonSerializerDefaults.Web);

    [GeneratedRegex(@"^(#{2,4})\s+(.+?)\s*#*$")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^\s*[-*•]\s+(.+)$")]
    private static partial Regex UnorderedListRegex();

    [GeneratedRegex(@"^\s*\d+[\.\)]\s+(.+)$")]
    private static partial Regex OrderedListRegex();

    [GeneratedRegex(@"^\s*([-*_])\s*\1\s*\1[\s\-*_]*$")]
    private static partial Regex HorizontalRuleRegex();

    [GeneratedRegex(@"^\|(.+)\|$")]
    private static partial Regex TableRowRegex();

    [GeneratedRegex(@"^\|[\s:\-\|]+\|$")]
    private static partial Regex TableSeparatorRegex();

    [GeneratedRegex(@"!\[([^\]]*)\]\(([^)\s]+)\)")]
    private static partial Regex ImageRegex();

    [GeneratedRegex(@"\[([^\]]+)\]\(([^)\s]+)\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex BoldRegex();

    [GeneratedRegex(@"(?<!\*)\*(?!\*)([^*]+?)(?<!\*)\*(?!\*)")]
    private static partial Regex ItalicRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    public static BlogRenderedContent Render(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return new BlogRenderedContent(string.Empty, [], 0, 1);
        }

        var normalized = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        var html = new StringBuilder(normalized.Length * 2);
        var toc = new List<BlogTocItem>();
        var plainText = new StringBuilder(normalized.Length);

        var i = 0;
        var headingSeq = 0;

        while (i < lines.Length)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                i++;
                continue;
            }

            if (HorizontalRuleRegex().IsMatch(line))
            {
                html.AppendLine("<hr />");
                i++;
                continue;
            }

            var headingMatch = HeadingRegex().Match(line.Trim());
            if (headingMatch.Success)
            {
                var level = headingMatch.Groups[1].Value.Length;
                var rawText = headingMatch.Groups[2].Value.Trim();
                headingSeq++;
                var id = $"sec-{headingSeq}";
                var cleanTitle = StripInlineMarkdown(rawText);
                plainText.Append(' ').Append(cleanTitle);

                if (level is 2 or 3)
                {
                    toc.Add(new BlogTocItem(id, cleanTitle, level));
                }

                html.Append("<h").Append(level).Append(" id=\"").Append(id).Append("\">")
                    .Append(RenderInline(rawText))
                    .Append("</h").Append(level).AppendLine(">");
                i++;
                continue;
            }

            if (line.TrimStart().StartsWith('>'))
            {
                var quoteLines = new List<string>();
                while (i < lines.Length && lines[i].TrimStart().StartsWith('>'))
                {
                    var stripped = lines[i].TrimStart();
                    stripped = stripped.Length > 1 && stripped[1] == ' ' ? stripped[2..] : stripped[1..];
                    quoteLines.Add(stripped);
                    i++;
                }

                var joined = string.Join(" ", quoteLines.Where(l => !string.IsNullOrWhiteSpace(l)));
                plainText.Append(' ').Append(StripInlineMarkdown(joined));
                html.Append("<blockquote><p>").Append(RenderInline(joined)).AppendLine("</p></blockquote>");
                continue;
            }

            if (TableRowRegex().IsMatch(line.Trim()) &&
                i + 1 < lines.Length &&
                TableSeparatorRegex().IsMatch(lines[i + 1].Trim()))
            {
                var headerCells = SplitTableCells(line.Trim());
                i += 2;
                var bodyRows = new List<string[]>();
                while (i < lines.Length && TableRowRegex().IsMatch(lines[i].Trim()))
                {
                    bodyRows.Add(SplitTableCells(lines[i].Trim()));
                    i++;
                }

                html.AppendLine("<div class=\"mag-table-wrap\"><table><thead><tr>");
                foreach (var cell in headerCells)
                {
                    plainText.Append(' ').Append(StripInlineMarkdown(cell));
                    html.Append("<th>").Append(RenderInline(cell)).Append("</th>");
                }
                html.AppendLine("</tr></thead><tbody>");
                foreach (var row in bodyRows)
                {
                    html.Append("<tr>");
                    for (var c = 0; c < headerCells.Length; c++)
                    {
                        var val = c < row.Length ? row[c] : string.Empty;
                        plainText.Append(' ').Append(StripInlineMarkdown(val));
                        html.Append("<td>").Append(RenderInline(val)).Append("</td>");
                    }
                    html.AppendLine("</tr>");
                }
                html.AppendLine("</tbody></table></div>");
                continue;
            }

            if (UnorderedListRegex().IsMatch(line))
            {
                html.AppendLine("<ul>");
                while (i < lines.Length)
                {
                    var m = UnorderedListRegex().Match(lines[i]);
                    if (!m.Success) break;
                    var itemText = m.Groups[1].Value.Trim();
                    plainText.Append(' ').Append(StripInlineMarkdown(itemText));
                    html.Append("<li>").Append(RenderInline(itemText)).AppendLine("</li>");
                    i++;
                }
                html.AppendLine("</ul>");
                continue;
            }

            if (OrderedListRegex().IsMatch(line))
            {
                html.AppendLine("<ol>");
                while (i < lines.Length)
                {
                    var m = OrderedListRegex().Match(lines[i]);
                    if (!m.Success) break;
                    var itemText = m.Groups[1].Value.Trim();
                    plainText.Append(' ').Append(StripInlineMarkdown(itemText));
                    html.Append("<li>").Append(RenderInline(itemText)).AppendLine("</li>");
                    i++;
                }
                html.AppendLine("</ol>");
                continue;
            }

            // Paragraph
            var paraLines = new List<string>();
            while (i < lines.Length &&
                   !string.IsNullOrWhiteSpace(lines[i]) &&
                   !HeadingRegex().IsMatch(lines[i].Trim()) &&
                   !lines[i].TrimStart().StartsWith('>') &&
                   !UnorderedListRegex().IsMatch(lines[i]) &&
                   !OrderedListRegex().IsMatch(lines[i]) &&
                   !HorizontalRuleRegex().IsMatch(lines[i]) &&
                   !(TableRowRegex().IsMatch(lines[i].Trim()) && i + 1 < lines.Length && TableSeparatorRegex().IsMatch(lines[i + 1].Trim())))
            {
                paraLines.Add(lines[i].Trim());
                i++;
            }

            var paragraph = string.Join(" ", paraLines);
            plainText.Append(' ').Append(StripInlineMarkdown(paragraph));

            // Standalone image becomes a semantic <figure>
            var trimmedPara = paragraph.Trim();
            var singleImage = ImageRegex().Match(trimmedPara);
            if (singleImage.Success && singleImage.Length == trimmedPara.Length)
            {
                var alt = singleImage.Groups[1].Value.Trim();
                var safeSrc = SanitizeUrl(singleImage.Groups[2].Value.Trim());
                if (safeSrc is not null)
                {
                    html.Append("<figure class=\"mag-figure\"><img src=\"")
                        .Append(WebUtility.HtmlEncode(safeSrc))
                        .Append("\" alt=\"")
                        .Append(WebUtility.HtmlEncode(alt))
                        .Append("\" loading=\"lazy\" decoding=\"async\" />");
                    if (!string.IsNullOrWhiteSpace(alt))
                    {
                        html.Append("<figcaption>").Append(WebUtility.HtmlEncode(alt)).Append("</figcaption>");
                    }
                    html.AppendLine("</figure>");
                    continue;
                }
            }

            html.Append("<p>").Append(RenderInline(paragraph)).AppendLine("</p>");
        }

        var words = WhitespaceRegex()
            .Split(plainText.ToString().Trim())
            .Count(w => !string.IsNullOrWhiteSpace(w));
        var readingMinutes = Math.Max(1, (int)Math.Ceiling(words / 200.0));

        return new BlogRenderedContent(html.ToString(), toc, words, readingMinutes);
    }

    public static int EstimateReadingTimeMinutes(string? markdown) =>
        Render(markdown).ReadingTimeMinutes;

    public static IReadOnlyList<BlogFaqItem> ParseFaqItems(string? faqJson)
    {
        if (string.IsNullOrWhiteSpace(faqJson))
        {
            return [];
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<BlogFaqItem>>(faqJson, FaqJsonOptions);
            if (items is null)
            {
                return [];
            }

            return items
                .Where(item => !string.IsNullOrWhiteSpace(item.Question) && !string.IsNullOrWhiteSpace(item.Answer))
                .Select(item => new BlogFaqItem(item.Question.Trim(), item.Answer.Trim()))
                .Take(20)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public static string? SanitizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var trimmed = url.Trim();
        if (trimmed.StartsWith('/') && !trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            return trimmed;
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) &&
            (uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ||
             uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)))
        {
            return uri.AbsoluteUri;
        }

        return null;
    }

    private static string[] SplitTableCells(string row)
    {
        var inner = row.Trim();
        if (inner.StartsWith('|')) inner = inner[1..];
        if (inner.EndsWith('|')) inner = inner[..^1];
        return inner.Split('|').Select(c => c.Trim()).ToArray();
    }

    private static string RenderInline(string raw)
    {
        var encoded = WebUtility.HtmlEncode(raw);

        encoded = ImageRegex().Replace(encoded, match =>
        {
            var alt = match.Groups[1].Value;
            var url = WebUtility.HtmlDecode(match.Groups[2].Value);
            var safe = SanitizeUrl(url);
            if (safe is null) return string.Empty;
            return $"<img src=\"{WebUtility.HtmlEncode(safe)}\" alt=\"{alt}\" loading=\"lazy\" decoding=\"async\" />";
        });

        encoded = LinkRegex().Replace(encoded, match =>
        {
            var label = match.Groups[1].Value;
            var url = WebUtility.HtmlDecode(match.Groups[2].Value);
            var safe = SanitizeUrl(url);
            if (safe is null) return label;
            var isExternal = safe.StartsWith("http", StringComparison.OrdinalIgnoreCase);
            var attrs = isExternal ? " target=\"_blank\" rel=\"noopener noreferrer\"" : string.Empty;
            return $"<a href=\"{WebUtility.HtmlEncode(safe)}\"{attrs}>{label}</a>";
        });

        encoded = InlineCodeRegex().Replace(encoded, "<code>$1</code>");
        encoded = BoldRegex().Replace(encoded, "<strong>$1</strong>");
        encoded = ItalicRegex().Replace(encoded, "<em>$1</em>");

        return encoded;
    }

    private static string StripInlineMarkdown(string raw)
    {
        var text = ImageRegex().Replace(raw, "$1");
        text = LinkRegex().Replace(text, "$1");
        text = InlineCodeRegex().Replace(text, "$1");
        text = BoldRegex().Replace(text, "$1");
        text = ItalicRegex().Replace(text, "$1");
        return text.Trim();
    }
}
