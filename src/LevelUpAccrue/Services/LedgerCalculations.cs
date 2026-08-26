using LevelUpAccrue.Models;

namespace LevelUpAccrue.Services;

public static class LedgerCalculations
{
    public static decimal PreviousCumulativeAmount(
        LedgerData data,
        Guid personId,
        DateTime beforeMonth)
    {
        return data.Periods
            .Where(period => period.Month < beforeMonth)
            .OrderByDescending(period => period.Month)
            .Select(period => period.Entries.FirstOrDefault(entry => entry.PersonId == personId))
            .FirstOrDefault(entry => entry is not null)
            ?.CumulativeAmount ?? 0m;
    }

    public static decimal Delta(decimal current, decimal previous) => current - previous;
}
