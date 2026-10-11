namespace KuaiYi.Contracts;
/// <summary>Bounded sequential phases; confirmation is not an image grant. Ordinary acknowledgements remain short.</summary>
public static class BrowserTiming
{
    public const int HandshakeMilliseconds=5000;
    public const int ConfirmationMilliseconds=30000;
    public const int TransferMilliseconds=30000;
    public const int TotalMilliseconds=HandshakeMilliseconds+ConfirmationMilliseconds+TransferMilliseconds;
    public static TimeSpan Handshake=>TimeSpan.FromMilliseconds(HandshakeMilliseconds);
    public static TimeSpan Confirmation=>TimeSpan.FromMilliseconds(ConfirmationMilliseconds);
    public static TimeSpan Transfer=>TimeSpan.FromMilliseconds(TransferMilliseconds);
    public static TimeSpan Total=>TimeSpan.FromMilliseconds(TotalMilliseconds);
}
