using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using LevelUpAccrue.Models;
using LevelUpAccrue.Services;

namespace LevelUpAccrue.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private static readonly StringComparer NameComparer = StringComparer.Create(
        CultureInfo.GetCultureInfo("zh-CN"),
        ignoreCase: true);

    private LedgerData _data;
    private LedgerPeriod? _selectedPeriod;
    private EntryRowViewModel? _selectedEntry;
    private string _searchText = string.Empty;
    private string _selectedStatusFilter = "全部状态";
    private bool _onlyChanged;
    private string _statusMessage = "数据已自动保存";

    public MainViewModel(LedgerData data)
    {
        _data = data;
        StatusFilters = ["全部状态", "待报销", "已报销"];
        EntriesView = CollectionViewSource.GetDefaultView(Entries);
        EntriesView.Filter = FilterEntry;
        RefreshPeriods();
    }

    public event EventHandler? DataChanged;

    public ObservableCollection<LedgerPeriod> Periods { get; } = [];
    public ObservableCollection<EntryRowViewModel> Entries { get; } = [];
    public ObservableCollection<EntryRowViewModel> SelectedEntries { get; } = [];
    public ICollectionView EntriesView { get; }
    public IReadOnlyList<string> StatusFilters { get; }
    public LedgerData Data => _data;

    public LedgerPeriod? SelectedPeriod
    {
        get => _selectedPeriod;
        set
        {
            if (SetProperty(ref _selectedPeriod, value) && value is not null)
            {
                LoadSelectedPeriod();
            }
        }
    }

    public EntryRowViewModel? SelectedEntry
    {
        get => _selectedEntry;
        set => SetProperty(ref _selectedEntry, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                RefreshFilter();
            }
        }
    }

    public string SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (SetProperty(ref _selectedStatusFilter, value ?? "全部状态"))
            {
                RefreshFilter();
            }
        }
    }

    public bool OnlyChanged
    {
        get => _onlyChanged;
        set
        {
            if (SetProperty(ref _onlyChanged, value))
            {
                RefreshFilter();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public decimal PeriodTotal => Entries.Sum(entry => entry.Delta);
    public decimal ReimbursedTotal => Entries.Where(entry => entry.IsReimbursed).Sum(entry => entry.Delta);
    public decimal PendingTotal => Entries.Where(entry => !entry.IsReimbursed).Sum(entry => entry.Delta);
    public decimal SelectedTotal => SelectedEntries.Sum(entry => entry.Delta);
    public int SelectedCount => SelectedEntries.Count;
    public int PeopleCount => Entries.Count;
    public string PeriodCaption => SelectedPeriod is null ? string.Empty : $"{SelectedPeriod.DisplayName} 明细";

    public string PreviewText => TextLedgerService.Format(
        SelectedEntries.Select(entry => (entry.Name, entry.Delta)));

    public DateTime SuggestedNextMonth => Periods.Count == 0
        ? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
        : Periods.Max(period => period.Month).AddMonths(1);

    public void ReplaceData(LedgerData data)
    {
        _data = data;
        RefreshPeriods();
        NotifyTotals();
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool HasPeriod(DateTime month)
    {
        var normalized = NormalizeMonth(month);
        return _data.Periods.Any(period => period.Month == normalized);
    }

    public void CreatePeriod(DateTime month, bool carryForward)
    {
        month = NormalizeMonth(month);
        if (HasPeriod(month))
        {
            SelectedPeriod = _data.Periods.Single(period => period.Month == month);
            return;
        }

        var period = new LedgerPeriod { Month = month };
        foreach (var person in _data.People.Where(person => person.IsVisibleIn(month)))
        {
            period.Entries.Add(new LedgerEntry
            {
                PersonId = person.Id,
                CumulativeAmount = carryForward
                    ? LedgerCalculations.PreviousCumulativeAmount(_data, person.Id, month)
                    : 0m
            });
        }

        _data.Periods.Add(period);
        RefreshPeriods(period.Id);
        StatusMessage = $"已创建 {period.DisplayName}";
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool DeleteSelectedPeriod()
    {
        if (SelectedPeriod is null || _data.Periods.Count <= 1)
        {
            return false;
        }

        var deletedName = SelectedPeriod.DisplayName;
        _data.Periods.Remove(SelectedPeriod);
        RefreshPeriods();
        StatusMessage = $"已删除 {deletedName}";
        DataChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void AddPerson(string name)
    {
        if (SelectedPeriod is null)
        {
            throw new InvalidOperationException("请先新建账期。");
        }

        name = name.Trim();
        if (name.Length == 0)
        {
            throw new ArgumentException("请输入人员姓名。", nameof(name));
        }

        var duplicate = _data.People.Any(person =>
            person.IsVisibleIn(SelectedPeriod.Month) && NameComparer.Equals(person.Name, name));
        if (duplicate)
        {
            throw new InvalidOperationException($"{name} 已在当前账期中。 ");
        }

        var person = new Person
        {
            Name = name,
            ActiveFromMonth = SelectedPeriod.Month
        };
        _data.People.Add(person);
        SelectedPeriod.Entries.Add(new LedgerEntry { PersonId = person.Id });
        LoadSelectedPeriod(person.Id);
        StatusMessage = $"已添加人员：{name}";
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool DeactivateSelectedPerson()
    {
        if (SelectedPeriod is null || SelectedEntry is null)
        {
            return false;
        }

        var person = SelectedEntry.Person;
        person.InactiveFromMonth = SelectedPeriod.Month;
        var name = person.Name;
        LoadSelectedPeriod();
        StatusMessage = $"已从当前及后续账期移除：{name}";
        DataChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void ImportAmounts(
        IEnumerable<ImportedAmount> importedAmounts,
        ImportAmountMode mode,
        bool markReimbursed)
    {
        if (SelectedPeriod is null)
        {
            return;
        }

        var grouped = importedAmounts
            .GroupBy(item => item.Name.Trim(), NameComparer)
            .Select(group => new ImportedAmount(
                group.Key,
                mode == ImportAmountMode.Increment
                    ? group.Sum(item => item.Amount)
                    : group.Last().Amount,
                string.Join("、", group.Select(item => item.Source).Distinct())))
            .ToList();
        var importedPersonIds = new HashSet<Guid>();

        foreach (var imported in grouped)
        {
            var person = _data.People.FirstOrDefault(candidate =>
                candidate.IsVisibleIn(SelectedPeriod.Month) && NameComparer.Equals(candidate.Name, imported.Name));
            if (person is null)
            {
                person = new Person
                {
                    Name = imported.Name,
                    ActiveFromMonth = SelectedPeriod.Month
                };
                _data.People.Add(person);
            }

            var entry = SelectedPeriod.Entries.FirstOrDefault(candidate => candidate.PersonId == person.Id);
            if (entry is null)
            {
                entry = new LedgerEntry
                {
                    PersonId = person.Id,
                    CumulativeAmount = LedgerCalculations.PreviousCumulativeAmount(
                        _data,
                        person.Id,
                        SelectedPeriod.Month)
                };
                SelectedPeriod.Entries.Add(entry);
            }

            entry.CumulativeAmount = mode == ImportAmountMode.Cumulative
                ? imported.Amount
                : entry.CumulativeAmount + imported.Amount;
            entry.IsReimbursed = markReimbursed;
            entry.ReimbursedAt = markReimbursed ? DateTimeOffset.Now : null;
            entry.Note = AppendSource(entry.Note, imported.Source);
            importedPersonIds.Add(person.Id);
        }

        LoadSelectedPeriod();
        foreach (var row in Entries.Where(row => importedPersonIds.Contains(row.PersonId)))
        {
            row.IsPreviewSelected = true;
        }

        StatusMessage = $"已导入 {grouped.Count} 人的金额";
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectVisibleEntries()
    {
        foreach (var entry in EntriesView.Cast<EntryRowViewModel>())
        {
            entry.IsPreviewSelected = true;
        }
    }

    public void ClearPreviewSelection()
    {
        foreach (var entry in Entries)
        {
            entry.IsPreviewSelected = false;
        }
    }

    public void SetSelectedReimbursed(bool reimbursed)
    {
        foreach (var entry in SelectedEntries.ToList())
        {
            entry.IsReimbursed = reimbursed;
        }

        RefreshFilter();
        StatusMessage = reimbursed ? "已将预览人员标记为已报销" : "已将预览人员标记为待报销";
    }

    public void RefreshEntriesView() => RefreshFilter();

    private void RefreshPeriods(Guid? selectPeriodId = null)
    {
        var priorSelectedId = selectPeriodId ?? SelectedPeriod?.Id;
        Periods.Clear();
        foreach (var period in _data.Periods.OrderByDescending(period => period.Month))
        {
            Periods.Add(period);
        }

        SelectedPeriod = priorSelectedId is not null
            ? Periods.FirstOrDefault(period => period.Id == priorSelectedId.Value) ?? Periods.FirstOrDefault()
            : Periods.FirstOrDefault();
    }

    private void LoadSelectedPeriod(Guid? selectPersonId = null)
    {
        if (SelectedPeriod is null)
        {
            Entries.Clear();
            SelectedEntries.Clear();
            NotifyTotals();
            return;
        }

        Entries.Clear();
        SelectedEntries.Clear();
        var people = _data.People
            .Where(person => person.IsVisibleIn(SelectedPeriod.Month))
            .OrderBy(person => person.Name, NameComparer)
            .ToList();

        foreach (var person in people)
        {
            var previous = LedgerCalculations.PreviousCumulativeAmount(_data, person.Id, SelectedPeriod.Month);
            var entry = SelectedPeriod.Entries.FirstOrDefault(item => item.PersonId == person.Id);
            if (entry is null)
            {
                entry = new LedgerEntry
                {
                    PersonId = person.Id,
                    CumulativeAmount = previous
                };
                SelectedPeriod.Entries.Add(entry);
            }

            var row = new EntryRowViewModel(
                person,
                entry,
                previous,
                isPreviewSelected: entry.CumulativeAmount - previous != 0,
                OnEntryChanged);
            Entries.Add(row);
            if (row.IsPreviewSelected)
            {
                SelectedEntries.Add(row);
            }
        }

        EntriesView.Refresh();
        SelectedEntry = selectPersonId is null
            ? Entries.FirstOrDefault()
            : Entries.FirstOrDefault(entry => entry.PersonId == selectPersonId.Value);
        OnPropertyChanged(nameof(PeriodCaption));
        NotifyTotals();
    }

    private void OnEntryChanged(EntryRowViewModel row, bool persistedChange)
    {
        if (row.IsPreviewSelected && !SelectedEntries.Contains(row))
        {
            SelectedEntries.Add(row);
        }
        else if (!row.IsPreviewSelected)
        {
            SelectedEntries.Remove(row);
        }

        NotifyTotals();
        if (persistedChange)
        {
            StatusMessage = "正在自动保存…";
            DataChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool FilterEntry(object item)
    {
        if (item is not EntryRowViewModel entry)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(SearchText) &&
            !entry.Name.Contains(SearchText.Trim(), StringComparison.CurrentCultureIgnoreCase) &&
            !entry.Note.Contains(SearchText.Trim(), StringComparison.CurrentCultureIgnoreCase))
        {
            return false;
        }

        if (SelectedStatusFilter == "待报销" && entry.IsReimbursed)
        {
            return false;
        }

        if (SelectedStatusFilter == "已报销" && !entry.IsReimbursed)
        {
            return false;
        }

        return !OnlyChanged || entry.Delta != 0;
    }

    private void RefreshFilter()
    {
        if (EntriesView is IEditableCollectionView editableView)
        {
            if (editableView.IsAddingNew)
            {
                editableView.CommitNew();
            }

            if (editableView.IsEditingItem)
            {
                editableView.CommitEdit();
            }
        }

        EntriesView.Refresh();
        OnPropertyChanged(nameof(PeopleCount));
    }

    private void NotifyTotals()
    {
        OnPropertyChanged(nameof(PeriodTotal));
        OnPropertyChanged(nameof(ReimbursedTotal));
        OnPropertyChanged(nameof(PendingTotal));
        OnPropertyChanged(nameof(SelectedTotal));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(PeopleCount));
        OnPropertyChanged(nameof(PreviewText));
    }

    private static DateTime NormalizeMonth(DateTime value) => new(value.Year, value.Month, 1);

    private static string AppendSource(string existing, string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return existing;
        }

        var label = $"来源：{source}";
        if (existing.Contains(label, StringComparison.Ordinal))
        {
            return existing;
        }

        return string.IsNullOrWhiteSpace(existing) ? label : $"{existing}；{label}";
    }
}
