using System.Buffers;
using System.Text;
using System.Text.Json;
namespace KuaiYi.Providers;
/// <summary>Only recognizable envelope/selected text progress counts; whitespace, unknown values and partial invalid scalars do not.</summary>
internal sealed class JsonProtocolProgress(ApiProtocol protocol)
{
    private enum Role{Root,Choices,Choice,Message,Content,Block,Other}
    private sealed class Selection{public long? Index;public bool Text;public bool Candidate;}
    private sealed class Frame(Role role,Selection? selection=null){public Role Role=role;public Selection? Selection=selection;public string Property="";public HashSet<string> Seen=[];}
    private readonly List<Frame> stack=[];private JsonReaderState state=new(new(){MaxDepth=32,CommentHandling=JsonCommentHandling.Disallow});private int consumed;private bool envelope;
    private int stringStart=-1,stringOffset;private int hexDigits,hexValue;private bool escaping;private char? high;
    public bool Scan(ReadOnlySpan<byte> all,bool final)
    {
        var reader=new Utf8JsonReader(all[consumed..],final,state);bool effective=false;
        while(reader.Read())
        {
            if(stack.Count==0)
            {if(reader.TokenType==JsonTokenType.StartObject){stack.Add(new(Role.Root));continue;}throw new JsonException("Expected one protocol object.");}
            var frame=stack[^1];
            if(reader.TokenType==JsonTokenType.PropertyName){frame.Property=reader.GetString()!;if(!frame.Seen.Add(frame.Property))throw new JsonException("Duplicate protocol property.");continue;}
            if(reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
            {
                Role role=Role.Other;Selection? selection=frame.Selection;
                if(protocol==ApiProtocol.OpenAIChat)
                {
                    if(frame.Role==Role.Root&&frame.Property=="choices"&&reader.TokenType==JsonTokenType.StartArray){role=Role.Choices;if(!envelope){effective=true;envelope=true;}}
                    else if(frame.Role==Role.Choices&&reader.TokenType==JsonTokenType.StartObject){role=Role.Choice;selection=new();}
                    else if(frame.Role==Role.Choice&&frame.Property=="message"&&reader.TokenType==JsonTokenType.StartObject)role=Role.Message;
                }
                else
                {
                    if(frame.Role==Role.Root&&frame.Property=="content"&&reader.TokenType==JsonTokenType.StartArray)role=Role.Content;
                    else if(frame.Role==Role.Content&&reader.TokenType==JsonTokenType.StartObject){role=Role.Block;selection=new();}
                }
                frame.Property="";stack.Add(new(role,selection));continue;
            }
            if(reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
            {if(stack.Count==1&&envelope)effective=true;stack.RemoveAt(stack.Count-1);continue;}
            if(frame.Role==Role.Root&&protocol==ApiProtocol.AnthropicMessages&&frame.Property=="type"&&reader.TokenType==JsonTokenType.String&&reader.ValueTextEquals("message")&&!envelope){effective=true;envelope=true;}
            if(frame.Role==Role.Choice&&frame.Property=="index"&&reader.TokenType==JsonTokenType.Number)
            {frame.Selection!.Index=reader.TryGetInt64(out long index)?index:null;if(frame.Selection.Index==0&&!envelope){effective=true;envelope=true;}}
            if(frame.Role==Role.Block&&frame.Property=="type"&&reader.TokenType==JsonTokenType.String)
            {frame.Selection!.Text=reader.ValueTextEquals("text");if(frame.Selection.Text&&!envelope){effective=true;envelope=true;}}
            if(IsText(frame)&&reader.TokenType==JsonTokenType.String&&reader.GetString()!.Length>0)
            {frame.Selection!.Candidate=true;if(Selected(frame))effective=true;}
            frame.Property="";
        }
        consumed+=checked((int)reader.BytesConsumed);state=reader.CurrentState;
        if(stack.Count>0&&IsText(stack[^1])&&Selected(stack[^1]))effective|=PartialText(all,consumed);
        else{stringStart=-1;stringOffset=0;escaping=false;hexDigits=hexValue=0;high=null;}
        return effective;
    }
    private static bool IsText(Frame frame)=>frame.Role==Role.Message&&frame.Property=="content"||frame.Role==Role.Block&&frame.Property=="text";
    private static bool Selected(Frame frame)=>frame.Role==Role.Message?frame.Selection?.Index==0:frame.Selection?.Text==true;
    private bool PartialText(ReadOnlySpan<byte> data,int start)
    {
        // Utf8JsonReader leaves an incomplete value token unconsumed. Locate the quote after optional separators/whitespace.
        while(start<data.Length&&data[start] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)':')start++;
        if(start>=data.Length||data[start]!=(byte)'"')return false;
        if(stringStart!=start){stringStart=start;stringOffset=1;escaping=false;hexDigits=hexValue=0;high=null;}
        bool changed=false;
        while(start+stringOffset<data.Length)
        {
            byte value=data[start+stringOffset];
            if(hexDigits>0)
            {int digit=value is >=(byte)'0' and <=(byte)'9'?value-'0':value is >=(byte)'a' and <=(byte)'f'?value-'a'+10:value is >=(byte)'A' and <=(byte)'F'?value-'A'+10:-1;if(digit<0)throw new JsonException("Invalid Unicode escape.");hexValue=(hexValue<<4)|digit;stringOffset++;if(--hexDigits==0){char scalar=(char)hexValue;hexValue=0;if(high is {} prior){if(!char.IsLowSurrogate(scalar))throw new JsonException("Unpaired Unicode surrogate.");high=null;changed=true;}else if(char.IsHighSurrogate(scalar))high=scalar;else if(char.IsLowSurrogate(scalar))throw new JsonException("Unpaired Unicode surrogate.");else changed=true;}continue;}
            if(escaping)
            {escaping=false;stringOffset++;if(value==(byte)'u'){hexDigits=4;continue;}if(high!=null||value is not ((byte)'"' or (byte)'\\' or (byte)'/' or (byte)'b' or (byte)'f' or (byte)'n' or (byte)'r' or (byte)'t'))throw new JsonException("Invalid string escape.");changed=true;continue;}
            if(value==(byte)'"'){if(high!=null)throw new JsonException("Unpaired Unicode surrogate.");break;}
            if(value==(byte)'\\'){escaping=true;stringOffset++;continue;}
            if(value<32||high!=null)throw new JsonException("Invalid string character.");
            var result=Rune.DecodeFromUtf8(data[(start+stringOffset)..],out _,out int bytes);
            if(result==OperationStatus.NeedMoreData)break;if(result!=OperationStatus.Done)throw new JsonException("Invalid UTF8 in text.");stringOffset+=bytes;changed=true;
        }
        return changed;
    }
}
