using System.Text.Json;
using System.Text.Json.Serialization;

namespace RogueEngine.Core.Runtime.Content;

public sealed record AssetRecord(
    Guid Id,
    string LogicalPath,
    string SourcePath,
    string Importer,
    DateTime LastImportedUtc,
    string Fingerprint,
    IReadOnlyList<Guid> Dependencies);

/// <summary>
/// Simple GUID-based asset registry with JSON persistence.
/// </summary>
public sealed class AssetDatabase
{
    private readonly Dictionary<Guid, AssetRecord> _assetsById = [];
    private readonly Dictionary<string, Guid> _assetIdByPath = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public IReadOnlyCollection<AssetRecord> Assets => _assetsById.Values;

    public AssetRecord RegisterOrUpdate(
        string logicalPath,
        string sourcePath,
        string importer,
        string fingerprint,
        IEnumerable<Guid>? dependencies = null)
    {
        if (_assetIdByPath.TryGetValue(logicalPath, out var existingId))
        {
            var updated = new AssetRecord(
                existingId,
                logicalPath,
                sourcePath,
                importer,
                DateTime.UtcNow,
                fingerprint,
                (dependencies ?? []).ToList());
            _assetsById[existingId] = updated;
            return updated;
        }

        var id = Guid.NewGuid();
        var record = new AssetRecord(
            id,
            logicalPath,
            sourcePath,
            importer,
            DateTime.UtcNow,
            fingerprint,
            (dependencies ?? []).ToList());
        _assetsById[id] = record;
        _assetIdByPath[logicalPath] = id;
        return record;
    }

    public bool TryGetByPath(string logicalPath, out AssetRecord? record)
    {
        record = null;
        if (!_assetIdByPath.TryGetValue(logicalPath, out var id))
            return false;

        if (!_assetsById.TryGetValue(id, out var found))
            return false;

        record = found;
        return true;
    }

    public string Serialize()
    {
        var dto = new AssetDatabaseDto { Assets = _assetsById.Values.OrderBy(a => a.LogicalPath).ToList() };
        return JsonSerializer.Serialize(dto, Options);
    }

    public static AssetDatabase Deserialize(string json)
    {
        var dto = JsonSerializer.Deserialize<AssetDatabaseDto>(json, Options)
            ?? throw new JsonException("Asset database JSON deserialized to null.");

        var db = new AssetDatabase();
        foreach (var asset in dto.Assets)
        {
            db._assetsById[asset.Id] = asset;
            db._assetIdByPath[asset.LogicalPath] = asset.Id;
        }

        return db;
    }

    private sealed class AssetDatabaseDto
    {
        public List<AssetRecord> Assets { get; init; } = [];
    }
}
