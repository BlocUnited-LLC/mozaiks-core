using System.Security.Cryptography;
using System.Text;

namespace AuthServer.Api.Shared;

public static class ApiKeyCrypto
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

    public static ApiKeyMaterial Generate(ApiKeyOptions options)
    {
        var environment = NormalizeEnvironment(options.Environment);
        var keyLength = options.KeyLength <= 0 ? 32 : options.KeyLength;

        var random = GenerateRandomString(keyLength);
        var apiKey = $"moz_{environment}_{random}";
        var prefixRandomLength = Math.Min(4, random.Length);
        var prefix = $"moz_{environment}_{random[..prefixRandomLength]}";
        var hash = ComputeSha256Base64(apiKey);

        return new ApiKeyMaterial(apiKey, hash, prefix);
    }

    private static string NormalizeEnvironment(string? environment)
    {
        var env = (environment ?? string.Empty).Trim().ToLowerInvariant();
        return env switch
        {
            "live" => "live",
            "test" => "test",
            _ => "test"
        };
    }

    private static string GenerateRandomString(int length)
    {
        if (length <= 0)
        {
            return string.Empty;
        }

        var bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);

        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        }

        return new string(chars);
    }

    private static string ComputeSha256Base64(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToBase64String(bytes);
    }
}

