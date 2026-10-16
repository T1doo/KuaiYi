using System.Text;
using System.Text.Json;
using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Provider.Tests;

public sealed class DisplayPreferencesTests
{
    private static readonly DisplayPreferences Value=new(16.5,480,ThemePreference.Dark,MotionPreference.Reduce);
    private static string Document=>Encoding.UTF8.GetString(DisplayPreferencesDocument.Write(Value));
    public static IEnumerable<object[]> AllChoices()
    {foreach(var theme in Enum.GetValues<ThemePreference>())foreach(var motion in Enum.GetValues<MotionPreference>())foreach(bool high in new[]{false,true})yield return [theme,motion,high];}
    [Theory][MemberData(nameof(AllChoices))]
    public void ExactFourFieldRoundTripsHaveAnIndependentVersionedWhitelist(ThemePreference theme,MotionPreference motion,bool high)
    {
        var value=new DisplayPreferences(high?22:12,high?960:240,theme,motion);var bytes=DisplayPreferencesDocument.Write(value);Assert.InRange(bytes.Length,2,4096);Assert.Equal(value,DisplayPreferencesDocument.Read(bytes));
        using var json=JsonDocument.Parse(bytes);Assert.Equal(new[]{"format","schemaVersion","fontSizeDip","preferredWidthDip","theme","reduceMotion"},json.RootElement.EnumerateObject().Select(p=>p.Name));Assert.Equal(DisplayPreferencesDocument.Format,json.RootElement.GetProperty("format").GetString());Assert.Equal(1,json.RootElement.GetProperty("schemaVersion").GetInt32());
    }
    [Theory][InlineData("format")][InlineData("schemaVersion")][InlineData("fontSizeDip")][InlineData("preferredWidthDip")][InlineData("theme")][InlineData("reduceMotion")]
    public void MissingAndDuplicateDecodedKeysAreRejected(string key)
    {
        using var doc=JsonDocument.Parse(Document);var fields=doc.RootElement.EnumerateObject().ToArray();string missing="{"+string.Join(',',fields.Where(p=>p.Name!=key).Select(p=>JsonSerializer.Serialize(p.Name)+":"+p.Value.GetRawText()))+"}";
        Assert.Equal(DisplayPreferencesIssue.MissingField,Error(missing).Issue);
        string duplicate=Document.TrimEnd()[..^1]+",\""+key+"\":"+doc.RootElement.GetProperty(key).GetRawText()+"}";Assert.Equal(DisplayPreferencesIssue.DuplicateField,Error(duplicate).Issue);
        string escaped=Document.TrimEnd()[..^1]+",\"\\u"+((int)key[0]).ToString("x4")+key[1..]+"\":"+doc.RootElement.GetProperty(key).GetRawText()+"}";Assert.Equal(DisplayPreferencesIssue.DuplicateField,Error(escaped).Issue);
    }
    [Theory][InlineData("profiles")][InlineData("apiKey")][InlineData("endpoint")][InlineData("consent")][InlineData("hotkeys")][InlineData("triggerMode")][InlineData("images")][InlineData("launchAtSignIn")][InlineData("lastPosition")][InlineData("monitorId")][InlineData("PUBLIC_PRIVATE_SENTINEL")]
    public void UnknownIncludingForbiddenConfigurationFieldsNeverEchoTheirNamesOrValues(string key)
    {
        string text=Document.TrimEnd()[..^1]+","+JsonSerializer.Serialize(key)+":\"PUBLIC_PRIVATE_SENTINEL\"}";var error=Error(text);Assert.Equal(DisplayPreferencesIssue.UnknownField,error.Issue);Assert.DoesNotContain("PUBLIC_PRIVATE_SENTINEL",error.ToString());Assert.DoesNotContain(key,error.Message,StringComparison.Ordinal);
    }
    public static IEnumerable<object[]> InvalidValues()
    {
        foreach(var (key,values) in new[]{("schemaVersion",new[]{"0","2","1.5","\"1\"","null","true"}),("fontSizeDip",new[]{"11.9","22.1","1e10000","\"16\"","null","false","{}"}),("preferredWidthDip",new[]{"239.9","960.1","-1","\"480\"","[]"}),("theme",new[]{"0","\"dark\"","\"999\"","\"PUBLIC_PRIVATE_SENTINEL\"","null"}),("reduceMotion",new[]{"false","1","\"reduce\"","\"999\"","{}"}),("format",new[]{"1","null","\"KuaiYi.Settings\""})})foreach(string value in values)yield return [key,value];
    }
    [Theory][MemberData(nameof(InvalidValues))]
    public void VersionTypesFiniteRangesAndCanonicalChoicesAreStrict(string key,string value)
    {
        using var doc=JsonDocument.Parse(Document);string json="{"+string.Join(',',doc.RootElement.EnumerateObject().Select(p=>JsonSerializer.Serialize(p.Name)+":"+(p.Name==key?value:p.Value.GetRawText())))+"}";Assert.DoesNotContain("PUBLIC_PRIVATE_SENTINEL",Error(json).ToString());
    }
    [Theory][InlineData("[]")][InlineData("null")][InlineData("true")][InlineData("{}")][InlineData("{\"format\":")][InlineData("{\"format\":\"\\uD800\"}")][InlineData("{\"\\uD800\":1}")]
    public void MalformedDocumentsHaveOnlySafeErrors(string text)=>Assert.DoesNotContain("PUBLIC_PRIVATE_SENTINEL",Error(text).ToString());
    [Fact]public async Task SizeBoundaryAndStreamingReadStayBoundedEvenForUnknownLength()
    {
        var bytes=DisplayPreferencesDocument.Write(Value);byte[] padded=Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes)+new string(' ',4096-bytes.Length));Assert.Equal(Value,DisplayPreferencesDocument.Read(padded));
        await using var exact=new MemoryStream(padded);Assert.Equal(Value,await DisplayPreferencesDocument.ReadAsync(exact));
        await using var tooLong=new CountingStream(new byte[20000]);var error=await Assert.ThrowsAsync<DisplayPreferencesException>(()=>DisplayPreferencesDocument.ReadAsync(tooLong));Assert.Equal(DisplayPreferencesIssue.FileSize,error.Issue);Assert.Equal(4097,tooLong.BytesRead);
        Assert.Equal(DisplayPreferencesIssue.FileSize,Assert.Throws<DisplayPreferencesException>(()=>DisplayPreferencesDocument.Read(new byte[4097])).Issue);
    }
    [Fact]public void MalformedUtf8AndUnpairedEscapesDoNotEscapeAsRawParserExceptions()
    {
        byte[] utf8=Encoding.UTF8.GetBytes(Document.Replace("Dark","PUBLIC_PRIVATE_SENTINEL"));int index=Array.IndexOf(utf8,(byte)'P');utf8[index]=0xff;Assert.Throws<DisplayPreferencesException>(()=>DisplayPreferencesDocument.Read(utf8));
        Assert.Throws<DisplayPreferencesException>(()=>DisplayPreferencesDocument.Read(Encoding.UTF8.GetBytes(Document.Replace("Dark","\\uD800"))));
    }
    [Fact]public void InvalidInMemoryValuesCannotBeExported()
    {foreach(var value in new[]{Value with{FontSizeDip=double.NaN},Value with{PreferredWidthDip=double.PositiveInfinity},Value with{Theme=(ThemePreference)99},Value with{ReduceMotion=(MotionPreference)99}})Assert.Throws<DisplayPreferencesException>(()=>DisplayPreferencesDocument.Write(value));}
    private static DisplayPreferencesException Error(string text)=>Assert.Throws<DisplayPreferencesException>(()=>DisplayPreferencesDocument.Read(Encoding.UTF8.GetBytes(text)));
    private sealed class CountingStream(byte[] bytes):MemoryStream(bytes)
    {public int BytesRead;public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellation=default){int n=await base.ReadAsync(buffer,cancellation);BytesRead+=n;return n;}}
}
