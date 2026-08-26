using LevelUpAccrue.Models;

namespace LevelUpAccrue.ViewModels;

public sealed class EntryRowViewModel : ObservableObject
{
    private readonly LedgerEntry _entry;
    private readonly Action<EntryRowViewModel, bool> _changed;
    private bool _isPreviewSelected;

    public EntryRowViewModel(
        Person person,
        LedgerEntry entry,
        decimal previousAmount,
        bool isPreviewSelected,
        Action<EntryRowViewModel, bool> changed)
    {
        Person = person;
        _entry = entry;
        PreviousAmount = previousAmount;
        _isPreviewSelected = isPreviewSelected;
        _changed = changed;
    }

    public Person Person { get; }
    public Guid PersonId => Person.Id;
    public string Name => Person.Name;
    public decimal PreviousAmount { get; }
    public decimal Delta => _entry.CumulativeAmount - PreviousAmount;
    public bool IsNegative => Delta < 0;

    public decimal CurrentAmount
    {
        get => _entry.CumulativeAmount;
        set
        {
            var rounded = decimal.Round(value, 2);
            if (_entry.CumulativeAmount == rounded)
            {
                return;
            }

            _entry.CumulativeAmount = rounded;
            if (Delta != 0)
            {
                _isPreviewSelected = true;
                OnPropertyChanged(nameof(IsPreviewSelected));
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(Delta));
            OnPropertyChanged(nameof(IsNegative));
            _changed(this, true);
        }
    }

    public bool IsReimbursed
    {
        get => _entry.IsReimbursed;
        set
        {
            if (_entry.IsReimbursed == value)
            {
                return;
            }

            _entry.IsReimbursed = value;
            _entry.ReimbursedAt = value ? DateTimeOffset.Now : null;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
            _changed(this, true);
        }
    }

    public string StatusText => IsReimbursed ? "已报销" : "待报销";

    public string Note
    {
        get => _entry.Note;
        set
        {
            var normalized = value ?? string.Empty;
            if (_entry.Note == normalized)
            {
                return;
            }

            _entry.Note = normalized;
            OnPropertyChanged();
            _changed(this, true);
        }
    }

    public bool IsPreviewSelected
    {
        get => _isPreviewSelected;
        set
        {
            if (SetProperty(ref _isPreviewSelected, value))
            {
                _changed(this, false);
            }
        }
    }
}
