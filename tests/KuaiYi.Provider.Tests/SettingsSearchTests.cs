using KuaiYi.App;
using Xunit;
namespace KuaiYi.Provider.Tests;
public sealed class SettingsSearchTests
{
    [Theory]
    [InlineData("字号",3)]
    [InlineData("aPi",1)]
    [InlineData("隐私",6)]
    [InlineData("提示词",2)]
    [InlineData("键盘",4)]
    [InlineData("浏览器",5)]
    public void FixedKeywordsReachTheExistingPageIndex(string query,int expected)
        =>Assert.Equal(expected,Assert.Single(SettingsSearchCatalog.Search(query)).PageIndex);
    [Fact]
    public void TermsMustMatchTheSameCategoryAndUnicodeWhitespaceIsAccepted()
    {
        Assert.Equal(1,Assert.Single(SettingsSearchCatalog.Search("  API\u3000密钥 \t")).PageIndex);
        Assert.Empty(SettingsSearchCatalog.Search("API 隐私"));
    }
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\u3000")]
    [InlineData("a-private-configuration-value")]
    public void EmptyOrUnknownInputDoesNotSuggestAnUnrelatedCategory(string? query)
        =>Assert.Empty(SettingsSearchCatalog.Search(query));
    [Fact]
    public void OversizedInputIsRefusedRatherThanTruncatedIntoAMatch()
        =>Assert.Empty(SettingsSearchCatalog.Search("API"+new string(' ',SettingsSearchCatalog.MaximumQueryLength)));
    [Fact]
    public void SearchKeepsNavigationOrderAndReturnedTitlesCannotMutateTheCatalog()
    {
        Assert.Equal(new[]{1,5},SettingsSearchCatalog.Search("发送").Select(hit=>hit.PageIndex));
        var titles=SettingsSearchCatalog.Titles();titles[0]="changed";
        Assert.Equal("常规",SettingsSearchCatalog.Titles()[0]);
        Assert.Equal(7,SettingsSearchCatalog.Titles().Length);
    }
}
