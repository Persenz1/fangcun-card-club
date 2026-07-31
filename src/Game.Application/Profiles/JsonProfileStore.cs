using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Application.Profiles;

public sealed class JsonProfileStore
{
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
    private readonly string _profilePath;

    public JsonProfileStore(string profilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profilePath);
        _profilePath = Path.GetFullPath(profilePath);
    }

    public string BackupPath => _profilePath + ".bak";

    public string TemporaryPath => _profilePath + ".tmp";

    public LocalPlayerProfile Load()
    {
        if (!File.Exists(_profilePath))
        {
            return File.Exists(BackupPath) ? Read(BackupPath) : new LocalPlayerProfile();
        }

        try
        {
            return Read(_profilePath);
        }
        catch (Exception exception) when (
            exception is IOException or JsonException or InvalidDataException
            && File.Exists(BackupPath))
        {
            return Read(BackupPath);
        }
    }

    public void Save(LocalPlayerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Validate(profile);

        var directory = Path.GetDirectoryName(_profilePath)!;
        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(profile, _options);

        try
        {
            File.WriteAllText(TemporaryPath, json);
            _ = Read(TemporaryPath);

            if (File.Exists(_profilePath))
            {
                try
                {
                    _ = Read(_profilePath);
                    File.Copy(_profilePath, BackupPath, true);
                }
                catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
                {
                    // Keep the last readable backup instead of replacing it with a damaged primary file.
                }
            }

            File.Move(TemporaryPath, _profilePath, true);
        }
        finally
        {
            if (File.Exists(TemporaryPath))
            {
                File.Delete(TemporaryPath);
            }
        }
    }

    private LocalPlayerProfile Read(string path)
    {
        var json = File.ReadAllText(path);
        var profile = JsonSerializer.Deserialize<LocalPlayerProfile>(json, _options)
            ?? throw new InvalidDataException("本地档案为空。");
        Validate(profile);
        return profile;
    }

    private static void Validate(LocalPlayerProfile profile)
    {
        if (profile.SchemaVersion != LocalPlayerProfile.CurrentSchemaVersion)
        {
            throw new NotSupportedException($"不支持的本地档案版本：{profile.SchemaVersion}。");
        }

        if (profile.DoudizhuStatistics is null
            || profile.Beans < 0
            || profile.DoudizhuStatistics.GamesPlayed < 0
            || profile.DoudizhuStatistics.GamesWon < 0
            || profile.DoudizhuStatistics.GamesWon > profile.DoudizhuStatistics.GamesPlayed)
        {
            throw new InvalidDataException("本地档案包含无效的豆子或战绩数据。");
        }

        if (profile.ActiveDoudizhu is { } recovery)
        {
            recovery.Validate();
        }
    }
}
