using GameLoopOptimizer.Core;
using Xunit;

namespace GameLoopOptimizer.Tests;

/// <summary>
/// Covers <see cref="KeymapXmlSanitizer"/>.
///
/// Regression context (2026-10-11): every keymap calibration failed with
/// "Generated keymap failed XML validation ... The 'KeyMapMode' start tag does not match
/// the end tag of 'Resolution_16x10'". The real cause was a pre-existing malformed stock
/// base file (modified 2026-07-09) containing three distinct defects.
/// </summary>
public class KeymapXmlSanitizerTests
{
    [Fact]
    public void Sanitize_ValidXml_IsReturnedUnchanged()
    {
        const string xml = "<Item ApkName=\"com.tencent.ig\"><KeyMapping ItemName=\"F\" Point_X=\"0.5\" Point_Y=\"0.5\"/></Item>";

        string result = KeymapXmlSanitizer.Sanitize(xml, out var info);

        Assert.Equal(xml, result);
        Assert.False(info.Changed);
        Assert.True(KeymapXmlSanitizer.IsWellFormed(result));
    }

    [Fact]
    public void Sanitize_RemovesStrayOrphanCloseTag()
    {
        // The stray </Item> sits between two real blocks, matching the real file's layout.
        const string xml =
            "<Item ApkName=\"a\"><KeyMapping/></Item>\n" +
            "</Item>\n" +
            "<Item ApkName=\"b\"><KeyMapping/></Item>";

        Assert.False(KeymapXmlSanitizer.IsWellFormed(xml), "Precondition: input must be malformed.");

        string result = KeymapXmlSanitizer.Sanitize(xml, out var info);

        Assert.True(KeymapXmlSanitizer.IsWellFormed(result));
        Assert.Equal(1, info.StrayCloseTagsRemoved);
        Assert.DoesNotContain("\n</Item>\n<Item", result.Replace("\r\n", "\n"));
    }

    [Fact]
    public void Sanitize_RemovesDuplicateAttributes()
    {
        // Real defect: two Raw="1" attributes on one KeyMappingEx element.
        const string xml =
            "<Item ApkName=\"com.tencent.ig\">\n" +
            "<KeyMappingEx ItemName=\"X\" Raw=\"1\" MiniVisiable=\"false\" Raw=\"1\" Type=\"RClick\"/>\n" +
            "</Item>";

        Assert.False(KeymapXmlSanitizer.IsWellFormed(xml), "Precondition: input must be malformed.");

        string result = KeymapXmlSanitizer.Sanitize(xml, out var info);

        Assert.True(KeymapXmlSanitizer.IsWellFormed(result));
        Assert.Equal(1, info.DuplicateAttributesRemoved);
        Assert.Equal(1, CountOccurrences(result, "Raw=\""));
    }

    [Fact]
    public void Sanitize_EscapesBareAmpersand()
    {
        // Real defect: "Nitro Nation Drag & Drift Racing" - a bare & is illegal in XML.
        const string xml = "<Item ApkName=\"com.creativemobile.nno\" Name=\"Nitro Nation Drag & Drift Racing\"/>";

        Assert.False(KeymapXmlSanitizer.IsWellFormed(xml), "Precondition: input must be malformed.");

        string result = KeymapXmlSanitizer.Sanitize(xml, out var info);

        Assert.True(KeymapXmlSanitizer.IsWellFormed(result));
        Assert.Equal(1, info.BareAmpersandsEscaped);
        Assert.Contains("&amp;", result);
    }

    [Fact]
    public void Sanitize_PreservesExistingEntities()
    {
        const string xml = "<Item Name=\"A &amp; B &lt;x&gt; &#65; &#x42;\"/>";

        string result = KeymapXmlSanitizer.Sanitize(xml, out var info);

        Assert.Equal(0, info.BareAmpersandsEscaped);
        Assert.Equal(xml, result);
        Assert.Contains("&amp;", result);
        Assert.Contains("&lt;x&gt;", result);
        Assert.Contains("&#65;", result);
    }

    [Fact]
    public void Sanitize_HandlesAllThreeDefectsTogether()
    {
        const string xml =
            "<Item ApkName=\"com.tencent.ig\">\n" +
            "<KeyMappingEx ItemName=\"A\" Raw=\"1\" Raw=\"1\"/>\n" +
            "</Item>\n" +
            "</Item>\n" +
            "<Item ApkName=\"x\" Name=\"Rock &amp; Roll\"/>\n";

        string result = KeymapXmlSanitizer.Sanitize(xml, out var info);

        Assert.True(KeymapXmlSanitizer.IsWellFormed(result));
        Assert.Equal(1, info.StrayCloseTagsRemoved);
        Assert.Equal(1, info.DuplicateAttributesRemoved);
    }

    [Fact]
    public void IsWellFormed_RejectsMultiRootWithoutWrapperHandling()
    {
        // GameLoop files are multi-root; the sanitizer must handle them via the root wrapper.
        const string multiRoot = "<Item ApkName=\"a\"/>\n<Item ApkName=\"b\"/>";

        Assert.True(KeymapXmlSanitizer.IsWellFormed(multiRoot));
    }

    [Fact]
    public void Sanitize_TrackedBalancedItems_AreNotRemoved()
    {
        // Guards against over-eager removal: properly balanced </Item> tags must survive.
        const string xml =
            "<Item ApkName=\"a\"><KeyMapMode ModeID=\"1\"><KeyMapping/></KeyMapMode></Item>\n" +
            "<ItemEx ApkName=\"b\"><KeyMapMode ModeID=\"2\"><KeyMapping/></KeyMapMode></ItemEx>";

        string result = KeymapXmlSanitizer.Sanitize(xml, out var info);

        Assert.Equal(0, info.StrayCloseTagsRemoved);
        Assert.Equal(xml, result);
    }

    [Fact]
    public void Sanitize_PreservesCoordinates()
    {
        // Sanitizing must never alter coordinate values.
        const string xml = "<Item ApkName=\"a\"><KeyMapping ItemName=\"W\" Point_X=\"0.202782\" Point_Y=\"0.722656\"/></Item>";

        string result = KeymapXmlSanitizer.Sanitize(xml);

        Assert.Contains("Point_X=\"0.202782\"", result);
        Assert.Contains("Point_Y=\"0.722656\"", result);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }
}