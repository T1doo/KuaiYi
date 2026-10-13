using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace KuaiYi.App;

// CI-only pipe for the actual owned public fault source, not a production diagnostic parser.
internal static class OleCiSourcePipe
{
    internal const int LineLimit = 8192;
    internal static async Task ReadMetadataAsync(Stream stream, Action<string> consume)
    {
        byte[] buffer = new byte[4096], line = new byte[LineLimit]; int used = 0; bool failed = false, oversized = false;
        try
        {
            int count;
            while ((count = await stream.ReadAsync(buffer)) != 0)
            {
                for (int i = 0; i < count; i++)
                {
                    byte value = buffer[i];
                    if (value == 10)
                    {
                        if (!failed && !oversized)
                            try { int length = used > 0 && line[used - 1] == 13 ? used - 1 : used; consume(new UTF8Encoding(false, true).GetString(line, 0, length)); }
                            catch (Exception) { failed = true; } // Retain only failure; continue draining the original pipe to EOF.
                        CryptographicOperations.ZeroMemory(line); used = 0; oversized = false;
                    }
                    else if (!failed && !oversized)
                    {
                        if (used == line.Length) { oversized = failed = true; CryptographicOperations.ZeroMemory(line); used = 0; }
                        else line[used++] = value;
                    }
                }
                CryptographicOperations.ZeroMemory(buffer);
            }
            if (failed || oversized || used != 0) throw new InvalidDataException("Owned public source metadata failed.");
        }
        finally { CryptographicOperations.ZeroMemory(buffer); CryptographicOperations.ZeroMemory(line); }
    }
    internal static async Task<bool> DrainErrorsAsync(Stream stream)
    {
        byte[] buffer = new byte[4096]; bool empty = true;
        try { while (await stream.ReadAsync(buffer) != 0) { empty = false; CryptographicOperations.ZeroMemory(buffer); } return empty; }
        catch (Exception) { return false; }
        finally { CryptographicOperations.ZeroMemory(buffer); }
    }
}
