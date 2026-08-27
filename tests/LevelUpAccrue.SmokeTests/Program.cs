using System.ComponentModel;
using LevelUpAccrue.Models;
using LevelUpAccrue.Services;
using LevelUpAccrue.ViewModels;

var tests = new (string Name, Action Run)[]
{
    ("首次运行创建空账本", FirstRunCreatesEmptyLedger),
    ("旧版内置示例数据自动清理", BuiltInSeedMigratesToEmptyLedger),
    ("已修改的旧账本不会被误清理", ModifiedLegacyLedgerIsPreserved),
    ("空账本可以添加人员", AddPersonToEmptyLedger),
    ("损坏数据恢复为空账本", CorruptStoreRecoversEmptyLedger),
    ("解析中文冒号与忽略总计", ParseChineseLedger),
    ("计算前期累计与本期增量", CalculateDelta),
    ("新账期承接累计金额", CarryForwardPeriod),
    ("金额修改刷新汇总与预览", UpdateEntrySummary),
    ("编辑事务中刷新会先提交编辑", RefreshDuringEditCommitsTransaction),
    ("预览选择支持全选和取消全选", PreviewSelectionActions),
    ("导入新增金额并添加人员", ImportIncrement),
    ("累计导入遇到重复人员取最后快照", ImportCumulativeSnapshot),
    ("JSON 保存和载入往返", StoreRoundTrip)
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}");
    }
}

Console.WriteLine($"{tests.Length - failed}/{tests.Length} tests passed");
return failed == 0 ? 0 : 1;

static void ParseChineseLedger()
{
    var parsed = TextLedgerService.Parse("张三：150\r\n李四: 50.5元\r\n\r\n总计：200.5", "sample.txt");
    Assert(parsed.Count == 2, "应识别两条人员记录");
    Assert(parsed[0] == new ImportedAmount("张三", 150m, "sample.txt"), "张三记录不匹配");
    Assert(parsed[1].Amount == 50.5m, "小数金额不匹配");
}

static void FirstRunCreatesEmptyLedger()
{
    var directory = CreateTempDirectory();
    try
    {
        var store = new LedgerStore(directory);
        var data = store.LoadOrCreate();
        var expectedMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        Assert(data.People.Count == 0, "首次运行不应带入示例人员");
        Assert(data.Periods.Count == 1, "首次运行应创建一个空账期");
        Assert(data.Periods[0].Month == expectedMonth, "空账期应使用当前月份");
        Assert(data.Periods[0].Entries.Count == 0, "空账期不应带入金额记录");
    }
    finally
    {
        DeleteTempDirectory(directory);
    }
}

static void AddPersonToEmptyLedger()
{
    var viewModel = new MainViewModel(LedgerData.CreateEmpty(new DateTime(2026, 8, 1)));
    viewModel.AddPerson("新人员");

    Assert(viewModel.Entries.Count == 1, "空账本应允许添加人员");
    Assert(viewModel.Entries[0].Name == "新人员", "添加的人员姓名不匹配");
    Assert(viewModel.Entries[0].CurrentAmount == 0m, "新人员初始累计金额应为 0");
}

static void BuiltInSeedMigratesToEmptyLedger()
{
    var directory = CreateTempDirectory();
    try
    {
        var store = new LedgerStore(directory);
        var legacySeed = SeedData.Create();
        File.WriteAllText(
            store.DataFilePath,
            System.Text.Json.JsonSerializer.Serialize(legacySeed, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            }));

        var data = store.LoadOrCreate();

        Assert(data.People.Count == 0, "旧版内置示例数据应被清理");
        Assert(data.Periods.Count == 1, "清理后应保留一个空账期");
        Assert(store.RecoveryMessage?.Contains("内置示例数据", StringComparison.Ordinal) == true,
            "清理提示应说明处理了内置示例数据");
        Assert(Directory.GetFiles(store.BackupDirectory, "内置示例数据迁移前_*.json").Length == 1,
            "清理前应保留一份备份");
    }
    finally
    {
        DeleteTempDirectory(directory);
    }
}

static void ModifiedLegacyLedgerIsPreserved()
{
    var directory = CreateTempDirectory();
    try
    {
        var store = new LedgerStore(directory);
        var legacySeed = SeedData.Create();
        legacySeed.People[0].Name = "已修改人员";
        File.WriteAllText(
            store.DataFilePath,
            System.Text.Json.JsonSerializer.Serialize(legacySeed, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            }));

        var data = store.LoadOrCreate();

        Assert(data.People.Count == 21, "已修改的旧账本应保留原有人员");
        Assert(data.People.Any(person => person.Name == "已修改人员"), "修改后的人员姓名应保留");
        Assert(store.RecoveryMessage is null, "普通旧账本不应触发示例数据迁移提示");
    }
    finally
    {
        DeleteTempDirectory(directory);
    }
}

static void CorruptStoreRecoversEmptyLedger()
{
    var directory = CreateTempDirectory();
    try
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "ledger.json");
        File.WriteAllText(path, "{ this is not valid json }");

        var store = new LedgerStore(directory);
        var data = store.LoadOrCreate();

        Assert(data.People.Count == 0, "损坏数据恢复不应重新载入示例人员");
        Assert(data.Periods.Count == 1, "损坏数据恢复应创建一个空账期");
        Assert(store.RecoveryMessage?.Contains("空账本", StringComparison.Ordinal) == true,
            "恢复提示应说明已载入空账本");
        Assert(Directory.GetFiles(directory, "ledger.corrupt.*.json").Length == 1,
            "损坏文件应被保留");
    }
    finally
    {
        DeleteTempDirectory(directory);
    }
}

static void CalculateDelta()
{
    var person = new Person { Name = "测试人员", ActiveFromMonth = new DateTime(2026, 7, 1) };
    var data = new LedgerData
    {
        People = [person],
        Periods =
        [
            new LedgerPeriod
            {
                Month = new DateTime(2026, 7, 1),
                Entries = [new LedgerEntry { PersonId = person.Id, CumulativeAmount = 100m }]
            },
            new LedgerPeriod
            {
                Month = new DateTime(2026, 8, 1),
                Entries = [new LedgerEntry { PersonId = person.Id, CumulativeAmount = 175m }]
            }
        ]
    };

    var previous = LedgerCalculations.PreviousCumulativeAmount(data, person.Id, new DateTime(2026, 8, 1));
    Assert(previous == 100m, "前期累计应为 100");
    Assert(LedgerCalculations.Delta(175m, previous) == 75m, "本期新增应为 75");
}

static void CarryForwardPeriod()
{
    var data = SeedData.Create();
    var viewModel = new MainViewModel(data);
    viewModel.CreatePeriod(new DateTime(2026, 9, 1), carryForward: true);
    Assert(viewModel.Entries.Count == 21, "应承接 21 人");
    Assert(viewModel.PeriodTotal == 0m, "承接后的初始增量应为 0");
    Assert(viewModel.Entries.All(entry => entry.CurrentAmount == entry.PreviousAmount), "累计金额应与上期相同");
}

static void ImportIncrement()
{
    var data = SeedData.Create();
    var viewModel = new MainViewModel(data);
    viewModel.ImportAmounts(
        [new ImportedAmount("示例人员01", 25m, "new.txt"), new ImportedAmount("新人员", 40m, "new.txt")],
        ImportAmountMode.Increment,
        markReimbursed: true);

    var existing = viewModel.Entries.Single(entry => entry.Name == "示例人员01");
    var added = viewModel.Entries.Single(entry => entry.Name == "新人员");
    Assert(existing.CurrentAmount == 175m, "已有人员应累加 25");
    Assert(added.CurrentAmount == 40m, "新人员金额应为 40");
    Assert(existing.IsReimbursed && added.IsReimbursed, "导入记录应标为已报销");
}

static void UpdateEntrySummary()
{
    var data = SeedData.Create();
    var viewModel = new MainViewModel(data);
    viewModel.CreatePeriod(new DateTime(2026, 9, 1), carryForward: true);
    var row = viewModel.Entries.First();
    row.CurrentAmount += 75m;

    Assert(row.Delta == 75m, "行增量应为 75");
    Assert(viewModel.PeriodTotal == 75m, "账期汇总应刷新为 75");
    Assert(viewModel.SelectedCount == 1, "发生变化的人员应自动加入预览");
    Assert(viewModel.SelectedTotal == 75m, "预览汇总应刷新为 75");
    Assert(viewModel.PreviewText.Contains("总计：75", StringComparison.Ordinal), "预览文本应包含 75 元合计");

    viewModel.SetSelectedReimbursed(true);
    Assert(row.IsReimbursed, "预览人员应能批量标记为已报销");
    Assert(viewModel.ReimbursedTotal == 75m, "已报销汇总应为 75");
    Assert(viewModel.PendingTotal == 0m, "待报销汇总应为 0");
}

static void RefreshDuringEditCommitsTransaction()
{
    var viewModel = new MainViewModel(SeedData.Create());
    var editableView = viewModel.EntriesView as IEditableCollectionView
        ?? throw new InvalidOperationException("明细视图应支持编辑事务");

    editableView.EditItem(viewModel.Entries.First());
    Assert(editableView.IsEditingItem, "测试前置条件：明细视图应处于编辑事务中");

    viewModel.RefreshEntriesView();

    Assert(!editableView.IsEditingItem, "刷新前应提交明细视图的编辑事务");
}

static void PreviewSelectionActions()
{
    var viewModel = new MainViewModel(SeedData.Create());
    viewModel.ClearPreviewSelection();
    Assert(viewModel.SelectedCount == 0, "取消全选后不应保留预览人员");

    viewModel.SearchText = "示例";
    var visibleCount = viewModel.EntriesView.Cast<EntryRowViewModel>().Count();
    viewModel.SelectVisibleEntries();
    Assert(visibleCount > 0, "筛选结果应包含人员");
    Assert(viewModel.SelectedCount == visibleCount, "全选应选择当前筛选结果");

    viewModel.SearchText = string.Empty;
    viewModel.SelectVisibleEntries();
    Assert(viewModel.SelectedCount == viewModel.Entries.Count, "清除筛选后全选应选择所有人员");

    viewModel.ClearPreviewSelection();
    Assert(viewModel.SelectedCount == 0, "取消全选应清空所有人员");
}

static void ImportCumulativeSnapshot()
{
    var data = SeedData.Create();
    var viewModel = new MainViewModel(data);
    viewModel.ImportAmounts(
        [
            new ImportedAmount("示例人员01", 175m, "first.txt"),
            new ImportedAmount("示例人员01", 190m, "latest.txt")
        ],
        ImportAmountMode.Cumulative,
        markReimbursed: false);

    var row = viewModel.Entries.Single(entry => entry.Name == "示例人员01");
    Assert(row.CurrentAmount == 190m, "累计导入应采用最后一条快照，而不是相加");
}

static void StoreRoundTrip()
{
    var directory = CreateTempDirectory();
    try
    {
        var store = new LedgerStore(directory);
        var data = SeedData.Create();
        store.Save(data);
        var loaded = store.LoadOrCreate();
        Assert(loaded.People.Count == 21, "保存后人员数不匹配");
        Assert(loaded.Periods.Single().Entries.Sum(entry => entry.CumulativeAmount) == 3400m, "保存后合计不匹配");
    }
    finally
    {
        DeleteTempDirectory(directory);
    }
}

static string CreateTempDirectory()
{
    var directory = Path.Combine(Path.GetTempPath(), $"LevelUpAccrueTests_{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    return directory;
}

static void DeleteTempDirectory(string directory)
{
    if (Directory.Exists(directory))
    {
        Directory.Delete(directory, recursive: true);
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
