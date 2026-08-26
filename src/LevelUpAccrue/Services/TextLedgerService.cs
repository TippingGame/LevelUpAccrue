using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LevelUpAccrue.Models;

namespace LevelUpAccrue.Services;

public static partial class TextLedgerService
{
    [GeneratedRegex(@"^\s*(?<name>[^:：]+?)\s*[:：]\s*(?<amount>[-+]?\s*[¥￥]?\s*[\d,]+(?:\.\d{1,2})?)\s*(?:元)?\s*$")]
    private static partial Regex AmountLineRegex();

    public static IReadOnlyList<ImportedAmount> Parse(string text, string source)
    {
        var parsed = new List<ImportedAmount>();
        foreach (var rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("总计", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("合计", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var match = AmountLineRegex().Match(line);
            if (!match.Success)
            {
                continue;
            }

            var name = match.Groups["name"].Value.Trim();
            var amountText = match.Groups["amount"].Value
                .Replace("¥", string.Empty, StringComparison.Ordinal)
                .Replace("￥", string.Empty, StringComparison.Ordinal)
                .Replace(",", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal);

            if (name.Length == 0 ||
                !decimal.TryParse(amountText, NumberStyles.Number | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out var amount))
            {
                continue;
            }

            parsed.Add(new ImportedAmount(name, amount, source));
        }

        return parsed;
    }

    public static string Format(IEnumerable<(string Name, decimal Amount)> amounts)
    {
        var items = amounts.ToList();
        var builder = new StringBuilder();
        foreach (var item in items)
        {
            builder.Append(item.Name)
                .Append('：')
                .AppendLine(FormatAmount(item.Amount));
        }

        if (items.Count > 0)
        {
            builder.AppendLine();
        }

        builder.Append("总计：").Append(FormatAmount(items.Sum(item => item.Amount)));
        return builder.ToString();
    }

    private static string FormatAmount(decimal amount)
    {
        return amount.ToString(amount == decimal.Truncate(amount) ? "0" : "0.##", CultureInfo.InvariantCulture);
    }
}
