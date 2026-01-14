using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services.Sync;

public static class Hasher
{
    public static async Task<string> CalculateSha256Async(string filePath)
    {
        if (!File.Exists(filePath)) return string.Empty;

        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hashBytes = await sha256.ComputeHashAsync(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    public static string CalculateSha256(Stream stream)
    {
        using var sha256 = SHA256.Create();
        if (stream.Position != 0 && stream.CanSeek) stream.Position = 0;
        var hashBytes = sha256.ComputeHash(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}
