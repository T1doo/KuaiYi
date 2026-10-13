using System.Diagnostics;
using System.Security.Cryptography;
using KuaiYi.SelectionReusePrototype;
using KuaiYi.SelectionReuseDiagnostic;

// Real synthetic child; the resource bits below are fixture declarations, not COM objects.
if(args.Length!=2||args[0]!="--synthetic-child"||args[1] is not ("normal" or "fragmented" or "truncated" or "disconnect" or "extra" or "hang" or "late" or "crash" or "reset-failed" or "pending" or "mismatch"))return 2;
string mode=args[1];using var input=Console.OpenStandardInput();using var output=Console.OpenStandardOutput();
Guid previous=Guid.Empty;ReuseTarget? identity=null;
for(byte sequence=1;sequence<=2;sequence++)
{
    byte[] bytes=new byte[ReuseFrameCodec.Size];ReuseFrame? request=null;
    try
    {
        if(!await ReuseWireStream.FillAsync(input,bytes,default))return 0;
        request=ReuseFrameCodec.Decode(bytes);CryptographicOperations.ZeroMemory(bytes);
        if(request.Kind!=ReuseKind.Begin||request.Sequence!=sequence||request.Nonce==previous||request.Reused!=(sequence==2)||identity!=null&&identity!=request.Target)return 3;
        previous=request.Nonce;identity=request.Target;var watch=Stopwatch.StartNew();
        if(mode=="crash")return 23;
        if(mode=="disconnect")return 0;
        if(mode=="hang")await Task.Delay(Timeout.Infinite);
        if(mode=="late")await Task.Delay(900); // Deliberately ignores cancellation; host must kill held process.
        var frame=request with{Persistent=PersistentResources.WindowlessMta|PersistentResources.Automation};
        if(sequence==1)await Send(frame with{Kind=ReuseKind.Initialized});
        if(mode=="truncated")
        {var partial=ReuseFrameCodec.Encode(frame with{Kind=ReuseKind.Completed,Outcome=ReuseOutcome.Success});try{await output.WriteAsync(partial.AsMemory(0,48));await output.FlushAsync();}finally{CryptographicOperations.ZeroMemory(partial);}return 0;}
        byte[] payload=mode=="mismatch"?[4,5,6]:[1,2,3];
        try{await Send(frame with{Kind=ReuseKind.Completed,Outcome=ReuseOutcome.Success,Payload=payload});}finally{CryptographicOperations.ZeroMemory(payload);}
        await Send(frame with{Kind=ReuseKind.Quiescent,Outcome=ReuseOutcome.Success,ResetOk=mode!="reset-failed",PendingOperations=(byte)(mode=="pending"?1:0)});
        if(mode=="extra")await Send(frame with{Kind=ReuseKind.Initialized});
        async Task Send(ReuseFrame value)=>await ReuseWireStream.WriteAsync(output,value with{ElapsedMs=watch.ElapsedMilliseconds},default,mode=="fragmented");
    }
    catch(IOException){return 4;}
    finally{CryptographicOperations.ZeroMemory(bytes);if(request?.Payload!=null)CryptographicOperations.ZeroMemory(request.Payload);}
}
// After two framesets, wait for parent closure instead of self-authorizing a third request.
byte[] tail=new byte[ReuseFrameCodec.Size];try{return await ReuseWireStream.FillAsync(input,tail,default)?3:0;}finally{CryptographicOperations.ZeroMemory(tail);}
