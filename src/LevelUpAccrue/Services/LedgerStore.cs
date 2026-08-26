using System.IO;
using System.Text.Json;
using LevelUpAccrue.Models;

namespace LevelUpAccrue.Services;

public sealed class LedgerStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public LedgerStore(string? dataDirectory = null)
    {
        var overriddenDirectory = Environment.GetEnvironmentVariable("LEVELUP_ACCRUE_DATA_DIR");
        DataDirectory = dataDirectory
            ?? (!string.IsNullOrWhiteSpace(overriddenDirectory)
                ? overriddenDirectory
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LevelUpAccrue"));
        DataFilePath = Path.Combine(DataDirectory, "ledger.json");
        BackupDirectory = Path.Combine(DataDirectory, "Backups");
    }

    public string DataDirectory { get; }
    public string DataFilePath { get; }
    public string BackupDirectory { get; }
    public string? RecoveryMessage { get; private set; }

    public LedgerData LoadOrCreate()
    {
        Directory.CreateDirectory(DataDirectory);
        if (!File.Exists(DataFilePath))
        {
            var empty = LedgerData.CreateEmpty();
            Save(empty);
            return empty;
        }

        LedgerData loaded;
        try
        {
            loaded = LoadFromFile(DataFilePath);
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidDataException)
        {
            var corruptPath = Path.Combine(DataDirectory, $"ledger.corrupt.{DateTime.Now:yyyyMMdd_HHmmss}.json");
            File.Copy(DataFilePath, corruptPath, overwrite: true);
            RecoveryMessage = $"原数据文件无法读取，已保留为 {Path.GetFileName(corruptPath)}，并载入空账本。";
            var empty = LedgerData.CreateEmpty();
            Save(empty);
            return empty;
        }

        if (!SeedData.IsBuiltInSeed(loaded))
        {
            return loaded;
        }

        Directory.CreateDirectory(BackupDirectory);
        var backupPath = Path.Combine(
            BackupDirectory,
            $"内置示例数据迁移前_{DateTime.Now:yyyyMMdd_HHmmssfff}.json");
        ExportBackup(loaded, backupPath);

        var migrated = LedgerData.CreateEmpty();
        RecoveryMessage = $"已将内置示例数据备份为 {Path.GetFileName(backupPath)}，并切换为空账本。";
        Save(migrated);
        return migrated;
    }

    public LedgerData LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        var data = JsonSerializer.Deserialize<LedgerData>(json, JsonOptions)
                   ?? throw new InvalidDataException("备份文件中没有可用数据。");
        NormalizeAndValidate(data);
        return data;
    }

    public void Save(LedgerData data)
    {
        Directory.CreateDirectory(DataDirectory);
        data.UpdatedAt = DateTimeOffset.Now;
        data.Version = LedgerData.CurrentVersion;
        NormalizeAndValidate(data);

        var tempPath = DataFilePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(data, JsonOptions));
        File.Move(tempPath, DataFilePath, overwrite: true);
        CreateDailyBackup(data);
    }

    public void ExportBackup(LedgerData data, string destinationPath)
    {
        data.UpdatedAt = DateTimeOffset.Now;
        File.WriteAllText(destinationPath, JsonSerializer.Serialize(data, JsonOptions));
    }

    private void CreateDailyBackup(LedgerData data)
    {
        Directory.CreateDirectory(BackupDirectory);
        var backupPath = Path.Combine(BackupDirectory, $"ledger_{DateTime.Now:yyyyMMdd}.json");
        if (!File.Exists(backupPath))
        {
            File.WriteAllText(backupPath, JsonSerializer.Serialize(data, JsonOptions));
        }
    }

    private static void NormalizeAndValidate(LedgerData data)
    {
        data.People ??= [];
        data.Periods ??= [];

        foreach (var person in data.People)
        {
            person.Name = person.Name?.Trim() ?? string.Empty;
            person.ActiveFromMonth = new DateTime(person.ActiveFromMonth.Year, person.ActiveFromMonth.Month, 1);
            if (person.InactiveFromMonth is not null)
            {
                person.InactiveFromMonth = new DateTime(
                    person.InactiveFromMonth.Value.Year,
                    person.InactiveFromMonth.Value.Month,
                    1);
            }
        }

        if (data.People.Any(person => person.Id == Guid.Empty || string.IsNullOrWhiteSpace(person.Name)))
        {
            throw new InvalidDataException("人员数据不完整。");
        }

        if (data.People.GroupBy(person => person.Id).Any(group => group.Count() > 1))
        {
            throw new InvalidDataException("人员编号存在重复。");
        }

        foreach (var period in data.Periods)
        {
            period.Month = new DateTime(period.Month.Year, period.Month.Month, 1);
            period.Entries ??= [];
            foreach (var entry in period.Entries)
            {
                entry.Note ??= string.Empty;
                entry.CumulativeAmount = decimal.Round(entry.CumulativeAmount, 2);
            }
        }

        if (data.Periods.GroupBy(period => period.Month).Any(group => group.Count() > 1))
        {
            throw new InvalidDataException("同一个月份存在多个账期。");
        }
    }
}
