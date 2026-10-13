using System.Text;
using System.Threading.Channels;

namespace KuaiYi.OleTransfer;

// Unique observations, never a timeline or an acceptance receipt. No identifiers or content.
public enum OleTargetObservation
{
    ControlEnabled, ControlDisabled, ControlRejected, CallbackEnter, CallbackDrop, CallbackRejected,
    EnterDisabled, EnterBusy, EnterOutside, EnterNoCopy, EnterSourceRejected,
    QueryQueued, QueryWritten, QueryRead, QueryStaleEpoch, QueryAccepted,
    DropQueued, DropWritten, DropRead, DropStaleEpoch, DropAccepted,
    ExtractionBegin, GetDataInvoking, ExtractionReturned
}
public sealed class OleTargetDiagnostics
{
    private readonly object sync = new();
    private readonly List<OleTargetObservation> items = [];
    private readonly Channel<OleTargetObservation>? output;
    public Task? OriginalOutputTask { get; }
    public OleExceptionCode OutputFailure { get; private set; }
    public OleTargetDiagnostics(Stream? destination = null)
    {
        if(destination==null)return;
        output=Channel.CreateBounded<OleTargetObservation>(new BoundedChannelOptions(32){SingleReader=true,FullMode=BoundedChannelFullMode.Wait});
        OriginalOutputTask=Task.Run(async()=>
        {
            try
            {
                await foreach(var item in output.Reader.ReadAllAsync().ConfigureAwait(false))
                {
                    byte[] bytes=Encoding.ASCII.GetBytes(Format(item)+"\n");
                    await destination.WriteAsync(bytes).ConfigureAwait(false);
                    await destination.FlushAsync().ConfigureAwait(false);
                }
            }
            catch(Exception error){OutputFailure=OleStartupDiagnostics.Code(error);throw;}
        });
    }
    public void CompleteOutput()=>output?.Writer.TryComplete();
    public OleTargetObservation[] Snapshot { get { lock(sync) return items.ToArray(); } }
    public void Observe(OleTargetObservation item)
    {
        if(!Enum.IsDefined(item))throw new ArgumentOutOfRangeException(nameof(item));
        lock(sync)
        {
            if(items.Contains(item))return;
            items.Add(item);
            _=output?.Writer.TryWrite(item); // Never waits on a pipe/Console lock. At most 24 unique records fit the 32-entry queue.
        }
    }
    public static string Format(OleTargetObservation item)
        => Enum.IsDefined(item)?"KYOT1|"+item:throw new ArgumentOutOfRangeException(nameof(item));
    public static bool TryParse(ReadOnlySpan<byte> bytes,out OleTargetObservation item)
    {
        item=default;
        if(bytes.Length is <7 or >OleStartupDiagnostics.LineLimit)return false;
        foreach(byte value in bytes)if(!(value is >=(byte)'A' and <=(byte)'Z' or >=(byte)'a' and <=(byte)'z' or (byte)'|' or (byte)'1'))return false;
        string text=Encoding.ASCII.GetString(bytes);
        return text.StartsWith("KYOT1|",StringComparison.Ordinal)&&Enum.TryParse(text[6..],out item)&&Enum.IsDefined(item)&&Format(item)==text;
    }
    // Same short-circuit order and one evaluation per reached production check.
    public static bool AdmitEnter(Func<bool> enabled,Func<bool> busy,Func<bool> hit,Func<bool> copy,Action<OleTargetObservation> observe)
    {
        if(!enabled()){observe(OleTargetObservation.EnterDisabled);return false;}
        if(busy()){observe(OleTargetObservation.EnterBusy);return false;}
        if(!hit()){observe(OleTargetObservation.EnterOutside);return false;}
        if(!copy()){observe(OleTargetObservation.EnterNoCopy);return false;}
        return true;
    }
}
