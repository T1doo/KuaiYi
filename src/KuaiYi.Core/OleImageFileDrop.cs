using System.Buffers.Binary;
using System.Text;
namespace KuaiYi.Core;
/// <summary>Inert CF_HDROP single wide path subset. No file opening, shell resolution or image decoding.</summary>
public static class OleImageFileDrop
{
    public const int GlobalByteLimit=16384,PathUnitLimit=4096,TransportByteLimit=PathUnitLimit*3;
    public static string DecodeGlobal(ReadOnlySpan<byte> bytes)
    {
        if(bytes.Length is <24 or >GlobalByteLimit||bytes.Length%2!=0||BinaryPrimitives.ReadUInt32LittleEndian(bytes)!=20||BinaryPrimitives.ReadUInt32LittleEndian(bytes[12..])>1||BinaryPrimitives.ReadUInt32LittleEndian(bytes[16..])!=1)throw new InvalidDataException("Unsupported single-image file list.");
        var data=bytes[20..];int end=-1;for(int i=0;i+1<data.Length;i+=2)if(data[i]==0&&data[i+1]==0){end=i;break;}
        if(end<=0||end>PathUnitLimit*2||end+4>data.Length||data[(end+2)..].IndexOfAnyExcept((byte)0)>=0)throw new InvalidDataException("Exactly one complete image file is required.");
        return ValidatePath(new UnicodeEncoding(false,false,true).GetString(data[..end]));
    }
    public static string DecodeTransport(ReadOnlySpan<byte> bytes)
    {
        if(bytes.Length is <1 or >TransportByteLimit)throw new InvalidDataException("Image file path limit.");return ValidatePath(new UTF8Encoding(false,true).GetString(bytes));
    }
    public static string ValidatePath(string path)
    {
        if(path.Length is <7 or >PathUnitLimit||!char.IsAsciiLetter(path[0])||path[1]!=':'||path[2]!='\\')throw new InvalidDataException("Ordinary local image file required.");
        for(int i=0;i<path.Length;i++)
        {
            char c=path[i];if(char.IsControl(c)||c is '/' or '"' or '<' or '>' or '|' or '?' or '*'||c==':'&&i!=1)throw new InvalidDataException("Unsupported image file path.");
            if(char.IsHighSurrogate(c)){if(++i>=path.Length||!char.IsLowSurrogate(path[i]))throw new InvalidDataException("Invalid path Unicode.");}else if(char.IsLowSurrogate(c))throw new InvalidDataException("Invalid path Unicode.");
        }
        foreach(string part in path[3..].Split('\\'))if(part.Length==0||part is "." or "..")throw new InvalidDataException("Unsupported image file path segment.");
        int dot=path.LastIndexOf('.');if(dot<=path.LastIndexOf('\\')||!new[]{".png",".jpg",".jpeg",".bmp",".webp",".gif"}.Contains(path[dot..],StringComparer.OrdinalIgnoreCase))throw new InvalidDataException("Supported single image file required.");return path;
    }
}
