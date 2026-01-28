using System.Security.Cryptography;
using System.Text;

namespace Mozaiks.ApiKeys;

internal static class ApiKeyHashing
{
    public static byte[] ComputeSha256(string value)
        => SHA256.HashData(Encoding.UTF8.GetBytes(value));

    public static bool FixedTimeEqualsBase64Hash(string apiKey, string storedHashBase64)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(storedHashBase64))
        {
            return false;
        }

        byte[] storedHashBytes;
        try
        {
            storedHashBytes = Convert.FromBase64String(storedHashBase64);
        }
        catch (FormatException)
        {
            return false;
        }

        var apiKeyHashBytes = ComputeSha256(apiKey);

        return apiKeyHashBytes.Length == storedHashBytes.Length
               && CryptographicOperations.FixedTimeEquals(apiKeyHashBytes, storedHashBytes);
    }
}

