using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace LevelUpAccrue.Models;

// Kept as an anonymous fixture for local smoke tests. New installations never load it.
public static class SeedData
{
    private const string LegacySeedFingerprint =
        "3CD1DDB83B064E0B07BD5FB52B2A93E940CA370125D726E49E4366A3071AC19B";

    private static readonly (string Name, decimal Amount, string Source)[] Samples =
    [
        ("示例人员01", 150m, "sample-01.txt"),
        ("示例人员02", 150m, "sample-01.txt"),
        ("示例人员03", 100m, "sample-01.txt"),
        ("示例人员04", 150m, "sample-01.txt"),
        ("示例人员05", 300m, "sample-01.txt"),
        ("示例人员06", 500m, "sample-02.txt"),
        ("示例人员07", 100m, "sample-03.txt"),
        ("示例人员08", 50m, "sample-03.txt"),
        ("示例人员09", 50m, "sample-03.txt"),
        ("示例人员10", 200m, "sample-03.txt"),
        ("示例人员11", 200m, "sample-03.txt"),
        ("示例人员12", 200m, "sample-03.txt"),
        ("示例人员13", 200m, "sample-03.txt"),
        ("示例人员14", 50m, "sample-03.txt"),
        ("示例人员15", 50m, "sample-03.txt"),
        ("示例人员16", 100m, "sample-03.txt"),
        ("示例人员17", 150m, "sample-03.txt"),
        ("示例人员18", 150m, "sample-03.txt"),
        ("示例人员19", 50m, "sample-03.txt"),
        ("示例人员20", 150m, "sample-03.txt"),
        ("示例人员21", 350m, "sample-03.txt")
    ];

    public static LedgerData Create()
    {
        var month = new DateTime(2026, 8, 1);
        var people = Samples
            .Select(item => new Person
            {
                Id = Guid.NewGuid(),
                Name = item.Name,
                ActiveFromMonth = month
            })
            .ToList();

        var period = new LedgerPeriod
        {
            Month = month,
            Entries = people.Select(person =>
            {
                var sample = Samples.Single(item => item.Name == person.Name);
                return new LedgerEntry
                {
                    PersonId = person.Id,
                    CumulativeAmount = sample.Amount,
                    IsReimbursed = false,
                    Note = $"来源：{sample.Source}"
                };
            }).ToList()
        };

        return new LedgerData
        {
            Version = 1,
            People = people,
            Periods = [period]
        };
    }

    public static bool IsBuiltInSeed(LedgerData data)
    {
        if (data.Version >= LedgerData.CurrentVersion)
        {
            return false;
        }

        return Fingerprint(data) == LegacySeedFingerprint || MatchesAnonymousFixture(data);
    }

    private static bool MatchesAnonymousFixture(LedgerData data)
    {
        if (data.People.Count != Samples.Length || data.Periods.Count != 1)
        {
            return false;
        }

        var period = data.Periods[0];
        if (period.Month != new DateTime(2026, 8, 1) || period.Entries.Count != Samples.Length)
        {
            return false;
        }

        foreach (var sample in Samples)
        {
            var person = data.People.SingleOrDefault(item => item.Name == sample.Name);
            if (person is null || person.ActiveFromMonth != period.Month || person.InactiveFromMonth is not null)
            {
                return false;
            }

            var entry = period.Entries.SingleOrDefault(item => item.PersonId == person.Id);
            if (entry is null || entry.CumulativeAmount != sample.Amount ||
                entry.IsReimbursed || entry.ReimbursedAt is not null || entry.Note != $"来源：{sample.Source}")
            {
                return false;
            }
        }

        return true;
    }

    private static string Fingerprint(LedgerData data)
    {
        if (data.People.Count != 21 || data.Periods.Count != 1)
        {
            return string.Empty;
        }

        var period = data.Periods[0];
        var comparer = StringComparer.Create(CultureInfo.GetCultureInfo("zh-CN"), ignoreCase: false);
        var canonical = data.People
            .OrderBy(person => person.Name, comparer)
            .Select(person =>
            {
                var entry = period.Entries.FirstOrDefault(item => item.PersonId == person.Id);
                return entry is null
                    ? string.Empty
                    : string.Join(
                        '|',
                        person.Name,
                        person.ActiveFromMonth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        person.InactiveFromMonth?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                        entry.CumulativeAmount.ToString(CultureInfo.InvariantCulture),
                        entry.IsReimbursed,
                        entry.Note);
            });

        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(string.Join('\n', canonical));
        return Convert.ToHexString(sha256.ComputeHash(bytes));
    }
}
