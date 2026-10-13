using System.Text.Json;
namespace KuaiYi.CompatibilityProbe;

// Selection is prepared inside a real browser; this fixture never supplies captured text.
internal sealed record PublicSelectionCase(string Kind, string Text, int Start, int End)
{
    public string Expected => Text[Start..End];
}
internal static class PublicSelectionFixtures
{
    internal static readonly string[] Views = ["Html", "ReadOnly", "Input"];
    internal static PublicSelectionCase[] Cases()
    {
        var result = new List<PublicSelectionCase>();
        void Add(string kind, string selected, int position)
        {
            string before = position == 0 ? "" : "Unselected prefix. ";
            string after = position == 2 ? "" : " Unselected suffix.";
            result.Add(new(kind, before + selected + after, before.Length, before.Length + selected.Length));
        }
        for (int position = 0; position < 3; position++)
        {
            Add("Word", "translation", position);
            Add("Letter", "Q", position);
            Add("Paragraph", "A public sentence explains a simple idea. The next sentence preserves its context.", position);
            Add("Unicode", "快译 café e\u0301 Ελληνικά 😀 𝄞", position);
            Add("Formula", "E = mc²; x ≤ 7; 42.5% → result", position);
            Add("Long", string.Join(" ", Enumerable.Repeat("Public translation fixture with no private content.", 32)), position);
        }
        Add("Paragraphs", "First public paragraph.\n\nSecond public paragraph.", 1);
        Add("Mixed", "English 中文 😀\tpublic\nnext line", 1);
        return result.ToArray();
    }
    internal static string Html(string view, string marker,int count=20)
    {
        if (!Views.Contains(view) || marker.Length != 32 || marker.Any(c => !char.IsAsciiHexDigit(c)) || count is not (1 or 20))
            throw new ArgumentException("Invalid fixture identity.");
        string element = view == "Html" ? "<pre id=sample tabindex=0></pre>" :
            view == "ReadOnly" ? "<textarea id=sample readonly></textarea>" : "<textarea id=sample></textarea>";
        string cases = JsonSerializer.Serialize(Cases().Take(count));
        return "<!doctype html><meta charset=utf-8><meta http-equiv=Content-Security-Policy content=\"default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'\">" +
            "<title>KYSEL-" + marker + "-WAIT</title><style>body{margin:0;background:white;color:black}#sample{box-sizing:border-box;width:100vw;height:90vh;margin:0;padding:24px;border:0;font:18px monospace;white-space:pre-wrap;overflow:auto}</style>" + element +
            "<script>const cases=" + cases + ";const sample=document.getElementById('sample');let i=-1;function next(){i++;if(i>=cases.length){document.title='KYSEL-" + marker + "-DONE';return;}" +
            "window.getSelection().removeAllRanges();const c=cases[i];" +
            (view == "Html" ? "sample.textContent=c.Text;sample.focus();const range=document.createRange();range.setStart(sample.firstChild,c.Start);range.setEnd(sample.firstChild,c.End);window.getSelection().addRange(range);" :
            "sample.value=c.Text;sample.focus();sample.setSelectionRange(c.Start,c.End);sample.scrollTop=0;") +
            "document.title='KYSEL-" + marker + "-'+String(i).padStart(2,'0');setTimeout(next,3000);}setTimeout(next,5000);</script>";
    }
    internal static bool Matches(string actual, string expected) =>
        actual.Replace("\r\n", "\n", StringComparison.Ordinal) == expected.Replace("\r\n", "\n", StringComparison.Ordinal);
}
