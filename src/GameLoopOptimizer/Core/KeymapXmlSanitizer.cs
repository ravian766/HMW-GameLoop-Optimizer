using System.Text.RegularExpressions;

namespace GameLoopOptimizer.Core;

/// <summary>
/// Repairs well-known, pre-existing malformed constructs that GameLoop itself emits in its
/// DefaultKeyMapping.xml files.
///
/// Context: stock keymaps shipped by GameLoop (verified against the 2026-07-09 files) contain
/// three classes of defect that make the document invalid under strict XML parsing:
///   1. A stray orphan "&lt;/Item&gt;" close tag (unbalanced element nesting).
///   2. Duplicate "Raw" attributes on KeyMappingEx elements.
///   3. Unescaped '&amp;' inside attribute values (e.g. "Drag &amp; Drift Racing").
///
/// The emulator's own parser is lenient and tolerates these, but System.Xml (and therefore
/// MappingValidator.ValidateXmlFragment) rejects them. That caused every calibration to abort
/// with "Generated keymap failed XML validation" even though the geometry math was fine.
///
/// IMPORTANT IMPLEMENTATION NOTE:
/// GameLoop wraps long attribute values across physical lines (e.g.
///     &lt;SwitchOperation Description="??
///     " EnableSwitch="SetUp" .../&gt;)
/// so any line-oriented scan mis-parses those elements and can delete valid content.
/// This implementation therefore operates on whole element spans (&lt;...&gt;) rather than
/// lines. A document that contains no known defect is returned byte-for-byte unchanged.
/// </summary>
public static class KeymapXmlSanitizer
{
    // Matches a complete element span: <Tag attr="v" ...> ... </Tag> or <Tag ... />
    // Group 1 = open tag, 2 = inner, 3 = close tag, 4 = tag name
    private static readonly Regex ElementRegex = new(
        @"<(?<tag>[A-Za-z][A-Za-z0-9_]*)(?<attrs>(?:[^>""]|""[^""]*"")*)\s*(?<selfClose>/)?>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    // Matches <Tag ...>inner</Tag> including multi-line inner content.
    private static readonly Regex PairedElementRegex = new(
        @"<(?<tag>[A-Za-z][A-Za-z0-9_]*)(?<attrs>(?:[^>""]|""[^""]*"")*)\s*>(?<inner>[\s\S]*?)</(?<tag2>[A-Za-z][A-Za-z0-9_]*)\s*>",
        RegexOptions.Compiled);

    // Matches a single attribute (double-quoted value) within an attribute blob.
    private static readonly Regex AttributeRegex = new(
        @"(?<name>[A-Za-z0-9_]+)\s*=\s*""(?<value>[^""]*)""",
        RegexOptions.Compiled);

    // Ampersand that does not already begin a valid XML entity reference.
    private static readonly Regex BareAmpersandRegex = new(
        @"&(?!amp;|lt;|gt;|quot;|apos;|#\d+;|#x[0-9a-fA-F]+;)",
        RegexOptions.Compiled);

    /// <summary>
    /// Describes what the sanitizer changed, for logging and diagnostics.
    /// </summary>
    public class SanitizeResult
    {
        public string Sanitized { get; init; } = string.Empty;
        public int StrayCloseTagsRemoved { get; init; }
        public int DuplicateAttributesRemoved { get; init; }
        public int BareAmpersandsEscaped { get; init; }

        public bool Changed =>
            StrayCloseTagsRemoved > 0 || DuplicateAttributesRemoved > 0 || BareAmpersandsEscaped > 0;

        public string Summary =>
            Changed
                ? $"strayClose={StrayCloseTagsRemoved}, dupAttrs={DuplicateAttributesRemoved}, bareAmp={BareAmpersandsEscaped}"
                : "no repairs needed";
    }

    /// <summary>
    /// Repairs known GameLoop keymap defects. Returns the original string unchanged when no
    /// defect is detected, so already-valid documents are never modified.
    /// </summary>
    public static string Sanitize(string xml, out SanitizeResult result)
    {
        result = new SanitizeResult { Sanitized = xml };

        if (string.IsNullOrWhiteSpace(xml))
        {
            return xml;
        }

        int strayClosed = 0;
        int dupAttrs = 0;
        int bareAmp = 0;

        // Pass 1 - drop orphan </Item>/<ItemEx> close tags using element-depth tracking.
        string step1 = RemoveOrphanItemCloseTags(xml, ref strayClosed);

        // Pass 2 - repair duplicate attributes and escape bare ampersands inside element spans.
        string step2 = RepairElementSpans(step1, ref dupAttrs, ref bareAmp);

        result = new SanitizeResult
        {
            Sanitized = step2,
            StrayCloseTagsRemoved = strayClosed,
            DuplicateAttributesRemoved = dupAttrs,
            BareAmpersandsEscaped = bareAmp
        };

        return step2;
    }

    /// <summary>
    /// Convenience overload returning just the repaired XML.
    /// </summary>
    public static string Sanitize(string xml) => Sanitize(xml, out _);

    /// <summary>
    /// True when the content is parseable XML (wrapped for multi-root GameLoop fragments).
    /// </summary>
    public static bool IsWellFormed(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) return false;

        try
        {
            System.Xml.Linq.XDocument.Parse($"<root>{xml}</root>");
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Walks the document tracking &lt;Item&gt;/&lt;ItemEx&gt; depth and removes any closing tag
    /// that appears with no matching open tag.
    /// </summary>
    private static string RemoveOrphanItemCloseTags(string xml, ref int removed)
    {
        var sb = new System.Text.StringBuilder(xml.Length);
        int lastIndex = 0;
        int depth = 0;

        foreach (System.Text.RegularExpressions.Match m in Regex.Matches(xml, @"<[^>]*>"))
        {
            string text = m.Value;

            bool isComment = text.StartsWith("<?") || text.StartsWith("<!");
            bool isClose = !isComment && text.StartsWith("</");
            bool isOpen = !isComment && !isClose;

            string name;
            if (isComment)
            {
                name = string.Empty;
            }
            else if (isClose)
            {
                name = text[2..].TrimEnd('>').Trim();
            }
            else
            {
                name = text[1..].TrimEnd('>').TrimEnd('/').Split(' ', '\t', '\r', '\n')[0];
            }

            bool isItemOpen = isOpen &&
                              (name.Equals("Item", StringComparison.OrdinalIgnoreCase) ||
                               name.Equals("ItemEx", StringComparison.OrdinalIgnoreCase));

            bool isItemClose = isClose &&
                               (name.Equals("Item", StringComparison.OrdinalIgnoreCase) ||
                                name.Equals("ItemEx", StringComparison.OrdinalIgnoreCase));

            // Copy everything between the previous tag and this one.
            sb.Append(xml, lastIndex, m.Index - lastIndex);
            lastIndex = m.Index + m.Length;

            if (isItemClose)
            {
                if (depth <= 0)
                {
                    removed++;   // orphan close tag - drop it
                    continue;
                }

                depth--;
            }
            else if (isItemOpen)
            {
                depth++;
            }

            sb.Append(text);
        }

        sb.Append(xml, lastIndex, xml.Length - lastIndex);
        return sb.ToString();
    }

    /// <summary>
    /// Repairs duplicate attributes and bare ampersands inside each element span, leaving all
    /// surrounding text untouched.
    /// </summary>
    private static string RepairElementSpans(string xml, ref int dupAttrs, ref int bareAmp)
    {
        var sb = new System.Text.StringBuilder(xml.Length);
        int position = 0;

        foreach (System.Text.RegularExpressions.Match m in ElementRegex.Matches(xml))
        {
            string attrs = m.Groups["attrs"].Value;
            if (attrs.Length == 0)
            {
                continue;
            }

            string repaired = DedupeAttributes(attrs, ref dupAttrs);
            repaired = EscapeBareAmpersands(repaired, ref bareAmp);

            if (repaired == attrs)
            {
                continue;
            }

            // Splice the repaired attribute blob back in, preserving surrounding text exactly.
            int attrsStart = m.Groups["attrs"].Index;
            int attrsLength = attrs.Length;

            sb.Append(xml, position, attrsStart - position);
            sb.Append(repaired);
            position = attrsStart + attrsLength;
        }

        sb.Append(xml, position, xml.Length - position);
        return sb.ToString();
    }

    private static string DedupeAttributes(string attrs, ref int removed)
    {
        var matches = AttributeRegex.Matches(attrs);
        if (matches.Count < 2) return attrs;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new List<System.Text.RegularExpressions.Match>();

        foreach (System.Text.RegularExpressions.Match m in matches)
        {
            if (!seen.Add(m.Groups["name"].Value))
            {
                duplicates.Add(m);
            }
        }

        if (duplicates.Count == 0) return attrs;

        string result = attrs;

        // Remove from the end so earlier indexes remain valid.
        for (int i = duplicates.Count - 1; i >= 0; i--)
        {
            var m = duplicates[i];
            int start = m.Index;
            int length = m.Length;

            // Swallow one preceding whitespace separator so we do not leave a double space.
            if (start > 0 && char.IsWhiteSpace(result[start - 1]))
            {
                start--;
                length++;
            }

            result = result.Remove(start, length);
            removed++;
        }

        return result;
    }

    private static string EscapeBareAmpersands(string attrs, ref int escaped)
    {
        if (attrs.IndexOf('&') < 0) return attrs;

        int before = BareAmpersandRegex.Matches(attrs).Count;
        if (before == 0) return attrs;

        string result = BareAmpersandRegex.Replace(attrs, "&amp;");
        escaped += before;
        return result;
    }
}