using System.Text;
using System.Text.Json;
using KuaiYi.CaptureBroker;
using KuaiYi.Contracts;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class SelectionCaptureOutputTests
{
    [Theory][InlineData(double.NaN)][InlineData(double.PositiveInfinity)][InlineData(double.NegativeInfinity)]
    public void Invalid_metadata_cannot_publish_text_or_nonfinite_numbers(double invalid)
    {
        var meta=new SelectionCaptureMetadata("Success",1,1,5,[1,2],[0,0,10,10],[invalid,0,10,10],789);
        var output=SelectionCaptureOutput.Prepare(meta,"PRIVATE_SENTINEL",7);
        Assert.Equal("",output.Text);Assert.DoesNotContain("PRIVATE_SENTINEL",Encoding.UTF8.GetString(output.Metadata));
        var actual=JsonSerializer.Deserialize<SelectionCaptureMetadata>(output.Metadata)!;
        Assert.Equal("BrokerFailure",actual.Status);Assert.Null(actual.ControlRectangle);Assert.Null(actual.RangeRectangles);
        Assert.Equal(SelectionBrokerStage.MetadataSerialize,actual.Diagnostic?.Stage);Assert.Equal(SelectionBrokerException.Argument,actual.Diagnostic?.Exception);
        Assert.Equal(7,actual.ElapsedMs);
    }
    [Fact]public void Valid_metadata_keeps_original_selection_and_identity()
    {
        var meta=new SelectionCaptureMetadata("Success",1,1,5,[1,2],[0,0,10,10],[0,0,100,100],789);
        var output=SelectionCaptureOutput.Prepare(meta,"public selected",7);
        Assert.Equal("public selected",output.Text);var actual=JsonSerializer.Deserialize<SelectionCaptureMetadata>(output.Metadata)!;
        Assert.Equal("Success",actual.Status);Assert.Equal(789,actual.ProcessStartUtcTicks);Assert.Equal(new double[]{0,0,100,100},actual.ControlRectangle);
    }
}
