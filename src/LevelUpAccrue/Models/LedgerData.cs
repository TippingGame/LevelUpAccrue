using System.Text.Json.Serialization;

namespace LevelUpAccrue.Models;

public sealed class LedgerData
{
    public const int CurrentVersion = 2;

    public int Version { get; set; } = CurrentVersion;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
    public List<Person> People { get; set; } = [];
    public List<LedgerPeriod> Periods { get; set; } = [];

    public static LedgerData CreateEmpty(DateTime? month = null)
    {
        var value = month ?? DateTime.Today;
        var normalizedMonth = new DateTime(value.Year, value.Month, 1);
        return new LedgerData
        {
            Periods = [new LedgerPeriod { Month = normalizedMonth }]
        };
    }
}

public sealed class Person
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTime ActiveFromMonth { get; set; }
    public DateTime? InactiveFromMonth { get; set; }

    public bool IsVisibleIn(DateTime month)
    {
        var normalized = new DateTime(month.Year, month.Month, 1);
        var activeFrom = new DateTime(ActiveFromMonth.Year, ActiveFromMonth.Month, 1);
        var inactiveFrom = InactiveFromMonth is null
            ? (DateTime?)null
            : new DateTime(InactiveFromMonth.Value.Year, InactiveFromMonth.Value.Month, 1);

        return normalized >= activeFrom && (inactiveFrom is null || normalized < inactiveFrom.Value);
    }
}

public sealed class LedgerPeriod
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Month { get; set; }
    public List<LedgerEntry> Entries { get; set; } = [];

    [JsonIgnore]
    public string DisplayName => $"{Month:yyyy年M月}";
}

public sealed class LedgerEntry
{
    public Guid PersonId { get; set; }
    public decimal CumulativeAmount { get; set; }
    public bool IsReimbursed { get; set; }
    public DateTimeOffset? ReimbursedAt { get; set; }
    public string Note { get; set; } = string.Empty;
}

public sealed record ImportedAmount(string Name, decimal Amount, string Source);

public enum ImportAmountMode
{
    Cumulative,
    Increment
}
