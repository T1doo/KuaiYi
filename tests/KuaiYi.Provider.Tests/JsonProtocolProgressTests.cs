using System.Text;
using KuaiYi.Providers;
using Xunit;
namespace KuaiYi.Provider.Tests;
public sealed class JsonProtocolProgressTests
{
    [Theory] [InlineData(ApiProtocol.OpenAIChat)] [InlineData(ApiProtocol.AnthropicMessages)]
    public void EveryUtf8AndUnicodeEscapeFragmentParsesWithoutInventingText(ApiProtocol protocol)
    {
        string json=protocol==ApiProtocol.OpenAIChat?"{\"choices\":[{\"index\":0,\"message\":{\"content\":\"你🌍\\uD83C\\uDF0D\\nPUBLIC\"},\"finish_reason\":\"stop\"}]}":"{\"type\":\"message\",\"content\":[{\"type\":\"text\",\"text\":\"你🌍\\uD83C\\uDF0D\\nPUBLIC\"}],\"stop_reason\":\"end_turn\"}";
        byte[] bytes=Encoding.UTF8.GetBytes(json);for(int chunk=1;chunk<=9;chunk++){var progress=new JsonProtocolProgress(protocol);int changes=0;for(int n=chunk;n<bytes.Length;n+=chunk)if(progress.Scan(bytes.AsSpan(0,n),false))changes++;progress.Scan(bytes,true);Assert.True(changes>0);}
    }
    [Theory] [InlineData("{\"choices\":[],\"choices\":[]}")] [InlineData("{\"choices\":[{\"index\":0,\"message\":{\"content\":\"\\q\"}}]}")] [InlineData("{\"choices\":[{\"index\":0,\"message\":{\"content\":\"\\uD800X\"}}]}")]
    public void InvalidDuplicateAndEscapedContentDoesNotQualifyAsValidActivity(string json)
    {var progress=new JsonProtocolProgress(ApiProtocol.OpenAIChat);Assert.ThrowsAny<System.Text.Json.JsonException>(()=>{byte[] bytes=Encoding.UTF8.GetBytes(json);for(int n=1;n<bytes.Length;n++)progress.Scan(bytes.AsSpan(0,n),false);progress.Scan(bytes,true);});}
    [Fact] public void UnknownCompleteTokensAndWhitespaceDoNotRefreshActivity()
    {var progress=new JsonProtocolProgress(ApiProtocol.OpenAIChat);string data="{\"UNKNOWN\":\"PUBLIC\"";Assert.False(progress.Scan(Encoding.UTF8.GetBytes(data),false));Assert.False(progress.Scan(Encoding.UTF8.GetBytes(data+"  "),false));}
}
