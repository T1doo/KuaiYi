using System.Text.Json;
using KuaiYi.Contracts;
namespace KuaiYi.CaptureBroker;
internal sealed record SelectionCaptureOutput(byte[] Metadata,string Text)
{
    internal static SelectionCaptureOutput Prepare(SelectionCaptureMetadata metadata,string text,double elapsedMs)
    {
        try{return new(JsonSerializer.SerializeToUtf8Bytes(metadata),text);}
        catch(ArgumentException error)
        {
            // Prepare the complete metadata before writing any frame. Never relax JSON number rules.
            var failure=new SelectionCaptureMetadata("BrokerFailure",0,0,elapsedMs,
                Diagnostic:new(SelectionBrokerStage.MetadataSerialize,SelectionBrokerException.Argument,error.HResult));
            return new(JsonSerializer.SerializeToUtf8Bytes(failure),"");
        }
    }
}
