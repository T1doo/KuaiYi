using KuaiYi.CompatibilityProbe;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class PublicSelectionFixtureTests
{
    [Fact]
    public void Fixed_denominator_contains_subsets_and_unicode_without_split_surrogates()
    {
        var cases = PublicSelectionFixtures.Cases(); Assert.Equal(20, cases.Length);
        foreach (var c in cases)
        {
            Assert.True(c.Start >= 0 && c.End > c.Start && c.End <= c.Text.Length);
            Assert.NotEqual(c.Text, c.Expected);
            Assert.False(char.IsLowSurrogate(c.Expected[0])); Assert.False(char.IsHighSurrogate(c.Expected[^1]));
        }
        Assert.Contains(cases, c => c.Expected.Contains("😀", StringComparison.Ordinal));
        Assert.Contains(cases, c => c.Expected.Contains("\n\n", StringComparison.Ordinal));
        Assert.Contains(cases, c => c.Expected.Length > 1000);
        Assert.Contains(cases, c => c.Expected.Length == 1);
    }
    [Theory]
    [InlineData("Html")]
    [InlineData("ReadOnly")]
    [InlineData("Input")]
    public void Browser_fixture_sets_selection_but_never_exports_selection_as_actual(string view)
    {
        string html = PublicSelectionFixtures.Html(view, new string('a', 32));
        Assert.Contains("default-src 'none'", html);
        Assert.Contains("sample.focus()", html); Assert.Contains("removeAllRanges()", html);
        Assert.DoesNotContain("fetch(", html); Assert.DoesNotContain("XMLHttpRequest", html);
        Assert.DoesNotContain("getSelection().toString", html);
        Assert.DoesNotContain("clipboard", html); Assert.DoesNotContain("localhost", html);
        Assert.Contains("padStart(2,'0')", html);
    }
    [Fact]
    public void Comparisons_do_not_trim_or_accept_whole_document()
    {
        Assert.True(PublicSelectionFixtures.Matches("one\r\ntwo", "one\ntwo"));
        Assert.False(PublicSelectionFixtures.Matches(" word ", "word"));
        Assert.False(PublicSelectionFixtures.Matches("prefix word suffix", "word"));
        Assert.Throws<ArgumentException>(() => PublicSelectionFixtures.Html("Html", "private marker"));
    }
}
