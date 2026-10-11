using System.Text;
using KuaiYi.Core;
using Xunit;

namespace KuaiYi.Core.Tests;

public sealed class OleTransferTests
{
    [Theory]
    [InlineData(true, 1u, true, 1u)] [InlineData(true, 3u, true, 1u)]
    [InlineData(true, 2u, true, 0u)] [InlineData(true, 4u, true, 0u)]
    [InlineData(true, 0u, true, 0u)] [InlineData(false, 3u, true, 0u)]
    [InlineData(true, 3u, false, 0u)] [InlineData(true, 0x80000003u, true, 1u)]
    public void OnlyCopyIsEverAccepted(bool enabled, uint offered, bool format, uint expected) =>
        Assert.Equal((OleEffect)expected, OleTransferPolicy.Preview(enabled, (OleEffect)offered, format));
    [Theory]
    [InlineData(false, false, false)] [InlineData(false, true, true)]
    [InlineData(true, false, false)] [InlineData(true, true, false)]
    public void AbsentDisabledOrFailedAsyncUsesSynchronousLifetime(bool available, bool mode, bool start) =>
        Assert.Equal(OleExtractionPath.SynchronousBeforeDropReturn, OleTransferPolicy.Extraction(available, mode, start));
    [Fact] public void SuccessfulHandshakeUsesAsync() =>
        Assert.Equal(OleExtractionPath.NegotiatedAsync, OleTransferPolicy.Extraction(true, true, true));
    [Fact] public void LeaveOffAndRepeatedDropCannotExtract()
    {
        var transaction = new OleDropTransaction(); Guid abandoned = transaction.Enter(); transaction.Leave();
        Assert.False(transaction.TryConsume(abandoned, true, OleEffect.Copy, true));
        Guid current = transaction.Enter(); Assert.False(transaction.TryConsume(current, false, OleEffect.Copy, true));
        Assert.False(transaction.TryConsume(current, true, OleEffect.Move, true));
        Assert.False(transaction.TryConsume(Guid.NewGuid(), true, OleEffect.Copy, true));
        Assert.True(transaction.TryConsume(current, true, OleEffect.Copy | OleEffect.Move, true));
        Assert.False(transaction.TryConsume(current, true, OleEffect.Copy, true));
        Guid next = transaction.Enter(); Assert.False(transaction.TryConsume(current, true, OleEffect.Copy, true));
        Assert.True(transaction.TryConsume(next, true, OleEffect.Copy, true));
    }
    [Fact] public void ProgressAndOtherEpochCannotExtendFixedDeadline()
    {
        var time = new TestClock(); var deadline = new OleCallDeadline(time); Guid operation = Guid.NewGuid();
        Assert.False(deadline.Complete(Guid.Empty, 0));
        Assert.True(deadline.Begin(operation, 4, TimeSpan.FromMilliseconds(1500))); time.Advance(1400);
        Assert.True(deadline.Begin(operation, 4, TimeSpan.FromSeconds(10))); Assert.False(deadline.Begin(operation, 5, TimeSpan.FromSeconds(10)));
        Assert.False(deadline.Complete(operation, 5)); Assert.False(deadline.Complete(Guid.NewGuid(), 4)); Assert.False(deadline.Expired);
        time.Advance(100); Assert.True(deadline.Expired); Assert.True(deadline.Complete(operation, 4)); Assert.False(deadline.Expired);
        Assert.True(deadline.Begin(operation, 5, TimeSpan.FromMilliseconds(1500))); Assert.False(deadline.Expired); deadline.Reset(); Assert.False(deadline.IsArmed);
    }
    [Fact] public void UnicodeBoundIsScalarsWithTerminatorAndPadding()
    {
        string text = string.Concat(Enumerable.Repeat("🌍", 50_000));
        byte[] global = Encoding.Unicode.GetBytes(text + "\0\0"); Assert.Equal(text, OleUnicodeText.Decode(global));
        Assert.Throws<InvalidDataException>(() => OleUnicodeText.Decode(Encoding.Unicode.GetBytes(text + "x\0")));
        Assert.Equal("中文🌍", OleUnicodeText.Decode(Encoding.Unicode.GetBytes(" \t中文🌍\n\0ignored")));
        Assert.Throws<InvalidDataException>(() => OleUnicodeText.Decode(Encoding.Unicode.GetBytes(new string(' ', 50_001) + "x\0")));
    }
    [Theory]
    [InlineData(new byte[] { })] [InlineData(new byte[] { 0 })] [InlineData(new byte[] { 0, 0 })]
    [InlineData(new byte[] { 0x61, 0 })] [InlineData(new byte[] { 0, 0xD8, 0, 0 })]
    [InlineData(new byte[] { 0, 0xDC, 0, 0 })] [InlineData(new byte[] { 0x20, 0, 0, 0 })]
    public void RejectsMalformedEmptyOrUnterminatedMedium(byte[] bytes) => Assert.Throws<InvalidDataException>(() => OleUnicodeText.Decode(bytes));
    [Fact] public void RejectsGlobalSizeBeforeDecoding() => Assert.Throws<InvalidDataException>(() => OleUnicodeText.Decode(new byte[OleUnicodeText.MaximumGlobalBytes + 2]));
    private sealed class TestClock : TimeProvider
    {
        private long value;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => value;
        public void Advance(long milliseconds) => value += milliseconds;
    }
}
