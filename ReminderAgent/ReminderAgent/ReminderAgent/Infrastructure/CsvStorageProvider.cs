using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using ReminderAgent.Domain;
using ReminderAgent.Interfaces;
using System.Globalization;

namespace ReminderAgent.Infrastructure
{
    /// <summary>
    /// CSV-based implementation of <see cref="IStorageProvider"/>.
    /// Responsible for persisting assets and managing backups and embeddings.
    /// </summary>
    public class CsvStorageProvider : IStorageProvider
    {
        private readonly string _filePath;
        private readonly string _backupFolder;
        private readonly CsvConfiguration _config;
        private readonly IEmbeddingStore _embeddingStore;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Gets the file path of the CSV storage.
        /// </summary>
        /// <returns>Absolute file path of the CSV file.</returns>
        public string GetFilePath() => _filePath;

        /// <summary>
        /// Initializes a new instance of the <see cref="CsvStorageProvider"/> class.
        /// Creates required directories and configures CSV behavior.
        /// </summary>
        /// <param name="embeddingStore">Embedding storage provider.</param>
        /// <param name="fileName">Name of the CSV file (default: reminders.csv).</param>
        public CsvStorageProvider(IEmbeddingStore embeddingStore, string fileName = "reminders.csv")
        {
            _embeddingStore = embeddingStore;

            string projectRoot = FindProjectRoot();
            string docPath = Path.Combine(projectRoot, "UserData");

            if (!Directory.Exists(docPath))
            {
                Directory.CreateDirectory(docPath);
                FileLogger.Info($"UserData folder created at: {docPath}");
            }

            _backupFolder = Path.Combine(docPath, "backup");
            if (!Directory.Exists(_backupFolder))
            {
                Directory.CreateDirectory(_backupFolder);
                FileLogger.Info($"Backup folder created at: {_backupFolder}");
            }

            _filePath = Path.Combine(docPath, fileName);
            FileLogger.Info($"CsvStorageProvider initialised. File: {_filePath}");

            _config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                HeaderValidated = null,
                MissingFieldFound = null,
                ShouldSkipRecord = args => args.Row.Parser.Record?.All(string.IsNullOrWhiteSpace) == true
            };
        }

        /// <summary>
        /// Saves a new asset to the CSV file and stores its embedding if available.
        /// Performs duplicate detection before saving.
        /// </summary>
        /// <param name="asset">The asset to be saved.</param>
        /// <returns>
        /// True if the asset was successfully saved; false if it was skipped or failed.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the CSV file is locked (e.g., opened in Excel).
        /// </exception>
        public async Task<bool> SaveAssetAsync(Asset asset)
        {
            await _lock.WaitAsync();
            try
            {
                // Lock check: fail fast if Excel has the file open
                try
                {
                    using var fs = new FileStream(
                        _filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException)
                {
                    string lockMsg = "Cannot save: reminders.csv is open in Excel. " +
                                     "Please close Excel and try again.";
                    FileLogger.Error($"SaveAssetAsync | {lockMsg}");
                    Console.WriteLine($"\n[Storage] {lockMsg}");
                    throw new InvalidOperationException(lockMsg);
                }

                // Duplicate check
                static IEnumerable<string> Keywords(string s) =>
                        s.ToLowerInvariant()
                         .Split(new[] { ' ', '-', '_', '/' }, StringSplitOptions.RemoveEmptyEntries)
                         .Where(w => w.Length > 3);

                var existing = GetAllAssetsFromCsv();
                var newKeywords = Keywords(asset.Name).ToHashSet();

                bool isDuplicate = existing.Any(a =>

                {
                    // Exact name+category match
                    if (string.Equals(a.Name, asset.Name, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(a.Category, asset.Category, StringComparison.OrdinalIgnoreCase))
                        return true;

                    // Fuzzy keyword overlap (≥2 shared significant words)
                    if (newKeywords.Count < 1) return false;
                    var existingKeywords = Keywords(a.Name).ToHashSet();
                    int overlap = newKeywords.Count(k => existingKeywords.Contains(k));
                    int minLen = Math.Min(newKeywords.Count, existingKeywords.Count);
                    return overlap >= 2 || (minLen == 1 && overlap == 1 && newKeywords.Count <= 2);
                });

                if (isDuplicate)
                {
                    FileLogger.Warning($"Duplicate (fuzzy): {asset.Name} | {asset.Category}");
                    Console.WriteLine($"[Storage] Duplicate detected for '{asset.Name}'. Skipping save.");
                    return false;
                }

                // Save embedding to JSON (before writing CSV)
                if (asset.Embedding != null && asset.Embedding.Length > 0)
                {
                    await _embeddingStore.SaveEmbeddingAsync(asset.Id, asset.Embedding);
                    FileLogger.Info($"SaveAssetAsync | Embedding saved to JSON for: {asset.Name}");
                }

                // Write new row (append)
                bool needsHeader = !File.Exists(_filePath) ||
                                   new FileInfo(_filePath).Length == 0;

                using var writer = new StreamWriter(_filePath, append: true);
                using var csv = new CsvWriter(writer, _config);
                csv.Context.RegisterClassMap<AssetMap>();

                if (needsHeader)
                {
                    csv.WriteHeader<Asset>();
                    csv.NextRecord();
                }

                csv.WriteRecord(asset);
                csv.NextRecord();

                FileLogger.Info($"SaveAssetAsync | Saved to CSV: {asset.Name}");
                CreateBackup();
                return true;
            }
            catch (InvalidOperationException)
            {
                // Re-throw Excel-lock errors so ReminderTool can surface them
                throw;
            }
            catch (Exception ex)
            {
                FileLogger.Error($"SaveAssetAsync failed: {ex.Message}");
                Console.WriteLine($"[Storage] Save failed: {ex.Message}");
                return false;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Retrieves assets from the CSV file and merges embeddings from storage.
        /// </summary>
        /// <param name="category">Optional category filter.</param>
        /// <param name="tag">Optional tag filter (currently unused).</param>
        /// <param name="timelineState">Optional timeline state filter (currently unused).</param>
        /// <returns>A collection of assets matching the filter criteria.</returns>
        public async Task<IEnumerable<Asset>> GetAssetsAsync(
            string? category = null,
            string? tag = null,
            string? timelineState = null)
        {
            if (!File.Exists(_filePath))
                return Enumerable.Empty<Asset>();

            await _lock.WaitAsync();
            try
            {
                // 1. Load all embeddings from JSON in one round-trip
                var embeddings = await _embeddingStore.LoadAllEmbeddingsAsync();

                // 2. Read assets from CSV
                List<Asset> records;
                using (var stream = new FileStream(
                    _filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                using (var csv = new CsvReader(reader, _config))
                {
                    csv.Context.RegisterClassMap<AssetMap>();
                    records = csv.GetRecords<Asset>().ToList();
                }

                FileLogger.Info($"GetAssetsAsync | Loaded {records.Count} assets from CSV");

                // 3. Merge embeddings back into each asset
                int merged = 0;
                foreach (var asset in records)
                {
                    if (embeddings.TryGetValue(asset.Id, out var emb))
                    {
                        asset.Embedding = emb;
                        //asset.Embeddings = new List<float[]> { emb };
                        merged++;
                    }
                }

                FileLogger.Info($"GetAssetsAsync | Merged embeddings for {merged}/{records.Count} assets");

                // 4. Apply optional filters
                IEnumerable<Asset> result = records;

                if (!string.IsNullOrWhiteSpace(category))
                    result = result.Where(a =>
                        a.Category.Equals(category, StringComparison.OrdinalIgnoreCase));

                return result;
            }
            catch (Exception ex)
            {
                FileLogger.Error($"GetAssetsAsync failed: {ex.Message}");
                return Enumerable.Empty<Asset>();
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Updates an existing asset in the CSV file and its associated embedding.
        /// Uses a temporary file to ensure crash-safe updates.
        /// </summary>
        /// <param name="asset">The updated asset.</param>
        /// <returns>True if update was successful; otherwise false.</returns>
        public async Task<bool> UpdateAssetAsync(Asset asset)
        {
            await _lock.WaitAsync();
            try
            {
                FileLogger.Info($"UpdateAssetAsync | Updating: {asset.Name} ({asset.Id})");

                if (!File.Exists(_filePath))
                {
                    FileLogger.Warning("UpdateAssetAsync | CSV file not found.");
                    return false;
                }

                //1. Load all current assets from CSV
                List<Asset> allAssets;
                using (var reader = new StreamReader(_filePath))
                using (var csv = new CsvReader(reader, _config))
                {
                    csv.Context.RegisterClassMap<AssetMap>();
                    allAssets = csv.GetRecords<Asset>().ToList();
                }

                // 2. Find the asset to update
                int index = allAssets.FindIndex(a => a.Id == asset.Id);
                if (index < 0)
                {
                    FileLogger.Warning($"UpdateAssetAsync | ID not found: {asset.Id}");
                    return false;
                }

                //3. Replace in-memory
                allAssets[index] = asset;

                //4. Update embedding in JSON if provided
                if (asset.Embedding != null && asset.Embedding.Length > 0)
                    await _embeddingStore.SaveEmbeddingAsync(asset.Id, asset.Embedding);

                //5. Rewrite CSV via temp file + rename (crash-safe)
                string tempPath = _filePath + ".tmp";
                var writeConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true
                };

                await using (var stream = new FileStream(
                    tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                await using (var writer = new StreamWriter(stream))
                await using (var csvWriter = new CsvWriter(writer, writeConfig))
                {
                    csvWriter.Context.RegisterClassMap<AssetMap>();
                    csvWriter.WriteHeader<Asset>();
                    await csvWriter.NextRecordAsync();

                    foreach (var a in allAssets)
                    {
                        csvWriter.WriteRecord(a);
                        await csvWriter.NextRecordAsync();
                    }
                }

                File.Replace(tempPath, _filePath, null);
                FileLogger.Info($"UpdateAssetAsync | Done. {allAssets.Count} records written.");
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Error($"UpdateAssetAsync failed: {ex.Message}");
                return false;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Reads all assets from the CSV file synchronously.
        /// Used internally for duplicate detection.
        /// Attempts restoration from backup if file is missing or empty.
        /// </summary>
        /// <returns>List of all assets from CSV.</returns
        private List<Asset> GetAllAssetsFromCsv()
        {
            if (!File.Exists(_filePath))
            {
                FileLogger.Warning("CSV missing. Attempting restore from backup...");
                RestoreLatestBackup();
            }

            if (File.Exists(_filePath) && new FileInfo(_filePath).Length == 0)
            {
                FileLogger.Warning("CSV empty. Attempting restore from backup...");
                RestoreLatestBackup();
            }

            if (!File.Exists(_filePath))
                return new List<Asset>();

            try
            {
                using var reader = new StreamReader(_filePath);
                using var csv = new CsvReader(reader, _config);
                csv.Context.RegisterClassMap<AssetMap>();
                var records = csv.GetRecords<Asset>().ToList();
                FileLogger.Info($"GetAllAssetsFromCsv | Read {records.Count} records.");
                return records;
            }
            catch (Exception ex)
            {
                FileLogger.Error($"GetAllAssetsFromCsv failed: {ex.Message}");
                return new List<Asset>();
            }
        }

        /// <summary>
        /// Finds the project root directory by traversing upward
        /// until a .csproj file is found.
        /// </summary>
        /// <returns>Path to the project root directory.</returns>
        private static string FindProjectRoot()
        {
            string current = AppDomain.CurrentDomain.BaseDirectory;

            for (int i = 0; i < 10; i++)
            {
                if (Directory.GetFiles(current, "*.csproj").Length > 0)
                    return current;

                DirectoryInfo? parent = Directory.GetParent(current);
                if (parent == null) break;
                current = parent.FullName;
            }

            FileLogger.Warning("FindProjectRoot | No .csproj found. Falling back to BaseDirectory.");
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        /// <summary>
        /// Creates a backup of the current CSV file.
        /// Keeps only the 5 most recent backups.
        /// </summary>
        private void CreateBackup()
        {
            try
            {
                if (!File.Exists(_filePath) || new FileInfo(_filePath).Length == 0) return;

                string name = $"reminders_{DateTime.Now:ddMMyyyy_HHmmss}.bak";
                File.Copy(_filePath, Path.Combine(_backupFolder, name), overwrite: true);
                FileLogger.Info($"Backup created: {name}");

                // Keep only the 5 most recent backups
                Directory.GetFiles(_backupFolder, "*.bak")
                    .OrderByDescending(f => f).Skip(5)
                    .ToList().ForEach(File.Delete);
            }
            catch (Exception ex) { FileLogger.Error($"Backup failed: {ex.Message}"); }
        }

        /// <summary>
        /// Restores the most recent backup file if available.
        /// </summary>
        private void RestoreLatestBackup()
        {
            try
            {
                var latest = Directory.GetFiles(_backupFolder, "*.bak")
                    .OrderByDescending(f => f).FirstOrDefault();

                if (latest != null)
                {
                    File.Copy(latest, _filePath, overwrite: true);
                    FileLogger.Warning($"CSV restored from: {Path.GetFileName(latest)}");
                }
                else
                {
                    FileLogger.Warning("No backups found to restore.");
                }
            }
            catch (Exception ex) { FileLogger.Error($"RestoreLatestBackup failed: {ex.Message}"); }
        }
    }

    /// <summary>
    /// Maps CSV columns to <see cref="Asset"/> properties.
    /// </summary>
    public sealed class AssetMap : ClassMap<Asset>
    {
        public AssetMap()
        {
            Map(m => m.Id).Index(0).Name("Id");
            Map(m => m.Name).Index(1).Name("Name");
            Map(m => m.Category).Index(2).Name("Category");
            Map(m => m.TimelineState).Index(3).Name("TimelineState");
            Map(m => m.EventDate).Index(4).Name("EventDate")
    .            TypeConverter<FlexibleDateConverter>()
                 .Data.TypeConverterOptions.Formats = new[] { "dd-MM-yyyy" }; ;
            Map(m => m.CreatedAt).Index(5).Name("CreatedAt")
    .            TypeConverter<FlexibleDateConverter>()
                 .Data.TypeConverterOptions.Formats = new[] { "dd-MM-yyyy" }; ;
            Map(m => m.Tags).Index(6).Name("Tags")
                .TypeConverter<ListConverter>();
            Map(m => m.Metadata).Index(7).Name("Metadata")
                .TypeConverter<DictionaryConverter>();
            Map(m => m.UserInput).Index(8).Name("UserInput");
            Map(m => m.UserExperience).Index(9).Name("UserExperience");
            Map(m => m.PhotoRefs).Index(10).Name("PhotoRefs")
                .TypeConverter<ListConverter>();
        }
    }

    /// <summary>
    /// Converts between List&lt;string&gt; and delimited string format.
    /// Used for properties like Tags and PhotoRefs.
    /// </summary>
    public class ListConverter : DefaultTypeConverter
    {
        public override string ConvertToString(object? value, IWriterRow row, MemberMapData memberMapData)
            => value is List<string> list ? string.Join(";", list) : "";

        public override object ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
            => string.IsNullOrWhiteSpace(text)
                ? new List<string>()
                : text.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                      .Select(t => t.Trim()).ToList();
    }

    /// <summary>
    /// Converts between Dictionary&lt;string, string&gt; and string format.
    /// Format: Key=Value|Key2=Value2
    /// </summary>
    public class DictionaryConverter : DefaultTypeConverter
    {
        public override string ConvertToString(object? value, IWriterRow row, MemberMapData memberMapData)
        {
            if (value is not Dictionary<string, string> dict || dict.Count == 0) return "";
            return string.Join("|", dict.Select(kv => $"{kv.Key}={kv.Value}"));
        }

        public override object ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
        {
            if (string.IsNullOrWhiteSpace(text)) return new Dictionary<string, string>();
            return text.Split('|', StringSplitOptions.RemoveEmptyEntries)
                       .Select(p => p.Split('=', 2))
                       .Where(p => p.Length == 2)
                       .ToDictionary(p => p[0].Trim(), p => p[1].Trim());
        }
    }

    /// <summary>
    /// Handles flexible parsing of date formats during CSV deserialization.
    /// Supports multiple common date formats.
    /// </summary>
    public class FlexibleDateConverter : DefaultTypeConverter
    {
        private static readonly string[] _readFormats = new[]
        {
        "dd-MM-yyyy HH:mm:ss",   
        "dd-MM-yyyy",            
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd",
        "yyyy/MM/dd",
        "MM/dd/yyyy HH:mm:ss",   
        "MM/dd/yyyy",
        "M/d/yyyy",
        "dd/MM/yyyy HH:mm:ss",   
        "dd/MM/yyyy",
        "d/M/yyyy",
        "d-M-yyyy",
        "dd.MM.yyyy",
        "d.M.yyyy"
    };

        public override object ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
        {
            if (string.IsNullOrWhiteSpace(text)) return null!;

            if (DateTime.TryParseExact(text.Trim(), _readFormats,
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt;

            // Last resort: let the runtime try
            if (DateTime.TryParse(text.Trim(), CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var dt2))
                return dt2;

            return null!;
        }
    }
}