namespace AuthServer.Api.Shared;

public static class ZipBundleExtractor
{
    public static IReadOnlyDictionary<string, byte[]> ExtractFiles(byte[] zipBytes)
    {
        using var ms = new MemoryStream(zipBytes);
        using var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read);

        var entryPaths = archive.Entries
            .Where(e => !string.IsNullOrWhiteSpace(e.FullName) && !e.FullName.EndsWith("/", StringComparison.Ordinal))
            .Select(e => NormalizeZipPath(e.FullName))
            .ToList();

        var prefix = FindCommonRootPrefix(entryPaths);

        var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.FullName) || entry.FullName.EndsWith("/", StringComparison.Ordinal))
            {
                continue;
            }

            var normalized = NormalizeZipPath(entry.FullName);
            if (!string.IsNullOrWhiteSpace(prefix) && normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[prefix.Length..];
            }

            if (string.IsNullOrWhiteSpace(normalized) || normalized.StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var entryStream = entry.Open();
            using var outStream = new MemoryStream();
            entryStream.CopyTo(outStream);
            result[normalized] = outStream.ToArray();
        }

        return result;
    }

    private static string NormalizeZipPath(string path)
        => path.Replace('\\', '/').TrimStart('/');

    private static string FindCommonRootPrefix(List<string> paths)
    {
        if (paths.Count == 0)
        {
            return string.Empty;
        }

        var firstSegments = paths
            .Select(p => p.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (firstSegments.Count != 1)
        {
            return string.Empty;
        }

        return $"{firstSegments[0]}/";
    }
}

