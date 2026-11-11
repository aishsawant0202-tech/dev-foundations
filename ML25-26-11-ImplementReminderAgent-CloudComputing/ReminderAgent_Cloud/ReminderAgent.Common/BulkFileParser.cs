using System.Text.Json;
using System.Globalization;
using System.IO.Compression;

namespace ReminderAgent.Common;

public static class BulkFileParser
{
    #region Parsing

    /// <summary>Parses a .csv, .json, or .zip file into a list of asset rows.</summary>
    public static List<BulkAssetRow> ParseFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        if (extension == ".zip")
        {
            var extractDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            ZipFile.ExtractToDirectory(filePath, extractDir);

            var innerFile = Directory.GetFiles(extractDir)
                .FirstOrDefault(f => f.EndsWith(".csv") || f.EndsWith(".json"))
                ?? throw new InvalidOperationException("Zip contains no .csv or .json file.");

            return ParseFile(innerFile);
        }

        if (extension == ".json")
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<List<BulkAssetRow>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new List<BulkAssetRow>();
        }

        if (extension == ".csv")
        {
            using var reader = new StreamReader(filePath);
            using var csv = new CsvHelper.CsvReader(reader, CultureInfo.InvariantCulture);
            return csv.GetRecords<BulkAssetRow>().ToList();
        }

        throw new NotSupportedException(
            $"'{extension}' isn't supported. Use .csv, .json, or .zip.");
    }

    #endregion
}