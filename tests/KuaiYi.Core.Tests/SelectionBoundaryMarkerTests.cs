using KuaiYi.Contracts;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class SelectionBoundaryMarkerTests
{
    [Theory][InlineData(SelectionBoundary.GetSelection)][InlineData(SelectionBoundary.GetBoundingRectangles)][InlineData(SelectionBoundary.GetText)][InlineData(SelectionBoundary.MetadataPrepared)][InlineData(SelectionBoundary.MainReturning)]
    public void AtomicAnonymousMappingRetainsEnterThenReturnedAndRejectsOtherRequest(SelectionBoundary boundary)
    {
        var id=Guid.NewGuid();using var map=SelectionBoundaryMap.CreateAnonymousForTest(id);
        Assert.Null(map.Read(id));map.Reach(boundary,SelectionBoundaryPosition.Enter);
        Assert.Equal(new SelectionReachedBoundary(boundary.ToString(),"Enter",1),map.Read(id));
        map.Reach(boundary,SelectionBoundaryPosition.Returned);Assert.Equal(new SelectionReachedBoundary(boundary.ToString(),"Returned",2),map.Read(id));Assert.Null(map.Read(Guid.NewGuid()));
    }
    [Theory][InlineData("Magic")][InlineData("Version")][InlineData("Nonce")][InlineData("Reserved")][InlineData("Length")][InlineData("Guard")][InlineData("Boundary")][InlineData("Position")][InlineData("Zero")][InlineData("Sequence")]
    public void CorruptOrStaleFixedRecordCannotBecomeReachedEvidence(string corruption)
    {
        Guid id=Guid.NewGuid();byte[] header=SelectionBoundaryWire.Header(id);long word=SelectionBoundaryWire.Encode(SelectionBoundary.GetText,SelectionBoundaryPosition.Enter,1);
        switch(corruption)
        {
            case "Magic":header[0]^=1;break;case "Version":header[4]=2;break;case "Nonce":header[8]^=1;break;case "Reserved":header[32]=1;break;case "Length":header=header[..63];break;
            case "Guard":word^=1L<<16;break;case "Boundary":word=(word&~255L)|255;break;case "Position":word=(word&~(255L<<8))|(255L<<8);break;case "Zero":word=0;break;case "Sequence":word=(word&0xffffffffL)|((long)16385<<32);break;
        }
        Assert.Null(SelectionBoundaryWire.Decode(header,word,id));
    }
    [Fact]public void MarkerHasNoPersistentFileAndCannotPublishAfterDispose()
    {
        var id=Guid.NewGuid();var map=SelectionBoundaryMap.CreateAnonymousForTest(id);map.Reach(SelectionBoundary.FramesWritten,SelectionBoundaryPosition.Returned);map.Dispose();map.Dispose();
        Assert.Throws<ObjectDisposedException>(()=>map.Read(id));Assert.Throws<ObjectDisposedException>(()=>map.Reach(SelectionBoundary.GetText,SelectionBoundaryPosition.Enter));
        if(!OperatingSystem.IsWindows())Assert.Null(SelectionBoundaryMap.TryCreate(id));
    }
    [Fact]public void SequenceOverflowAndUndefinedEnumInvalidateInsteadOfReusingPreviousWord()
    {
        var id=Guid.NewGuid();using var map=SelectionBoundaryMap.CreateAnonymousForTest(id);
        map.Reach(SelectionBoundary.GetText,SelectionBoundaryPosition.Enter);map.Reach((SelectionBoundary)255,SelectionBoundaryPosition.Enter);Assert.Null(map.Read(id));
        Assert.Equal(0,SelectionBoundaryWire.Encode(SelectionBoundary.GetText,SelectionBoundaryPosition.Enter,16385));
    }
}
