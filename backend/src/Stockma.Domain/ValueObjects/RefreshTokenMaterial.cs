using System.Security.Cryptography;
using System.Text;

namespace Stockma.Domain.ValueObjects;

public static class RefreshTokenMaterial
{
    public const int TokenSizeBytes = 32;

    public static string GenerateToken() => Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenSizeBytes));

    public static string Hash(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("El token es obligatorio.", nameof(token));
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
