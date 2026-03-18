using System.Text.RegularExpressions;
using HtmlAgilityPack;
using OmniTactica.AppCode.Models.Core;
using OmniTactica.AppCode.Utilities;

namespace OmniTactica.AppCode.Helpers
{
    public static class DatasheetParsingHelper
    {
        public static List<List<DatasheetCompositionEntry>> ParseUnitCompositionOptions(IEnumerable<DatasheetUnitComposition> composition)
        {
            var options = new List<List<DatasheetCompositionEntry>>();
            var currentOption = new List<DatasheetCompositionEntry>();

            foreach (var entry in composition)
            {
                var description = HtmlUtility.ConvertHtmlToPlainText(entry.Description).Trim();
                if (string.IsNullOrWhiteSpace(description))
                {
                    continue;
                }

                if (description.Equals("OR", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentOption.Count > 0)
                    {
                        options.Add(currentOption);
                        currentOption = new List<DatasheetCompositionEntry>();
                    }

                    continue;
                }

                currentOption.AddRange(ParseCompositionEntries(description));
            }

            if (currentOption.Count > 0)
            {
                options.Add(currentOption);
            }

            return options;
        }

        public static List<DatasheetCompositionEntry> SelectCompositionOption(List<List<DatasheetCompositionEntry>> options, string? selectedCostDescription)
        {
            if (options.Count == 0)
            {
                return new List<DatasheetCompositionEntry>();
            }

            if (!string.IsNullOrWhiteSpace(selectedCostDescription))
            {
                var costEntries = ParseCostDescriptionEntries(selectedCostDescription);
                if (costEntries.Count > 0)
                {
                    var exactMatch = options.FirstOrDefault(option => MatchesCompositionOption(option, costEntries));
                    if (exactMatch != null)
                    {
                        return exactMatch;
                    }

                    var targetTotal = costEntries.Sum(entry => entry.Quantity);
                    var countMatch = options.FirstOrDefault(option => option.Sum(entry => entry.Quantity) == targetTotal);
                    if (countMatch != null)
                    {
                        return countMatch;
                    }
                }
            }

            return options[0];
        }

        public static DatasheetModel? FindModelProfile(DatasheetDetail detail, string modelName)
        {
            var normalizedModelName = modelName.Replace(" ", string.Empty);

            return detail.Models.FirstOrDefault(model =>
                       model.Name.Equals(modelName, StringComparison.OrdinalIgnoreCase))
                   ?? detail.Models.FirstOrDefault(model =>
                       model.Name.Replace(" ", string.Empty).Contains(normalizedModelName, StringComparison.OrdinalIgnoreCase) ||
                       normalizedModelName.Contains(model.Name.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase))
                   ?? detail.Models.FirstOrDefault(model =>
                       model.Name.Equals(detail.Name, StringComparison.OrdinalIgnoreCase))
                   ?? detail.Models.FirstOrDefault();
        }

        public static List<ParsedLoadoutWeapon> ParseLoadoutForModel(string loadoutText, string modelName, IEnumerable<DatasheetWargear> allWargear, int modelQuantity = 1)
        {
            var weapons = new List<ParsedLoadoutWeapon>();
            if (string.IsNullOrWhiteSpace(loadoutText))
            {
                return weapons;
            }

            var cleanedLoadout = HtmlUtility.StripHtml(loadoutText);
            var loadoutLines = cleanedLoadout.Split(new[] { '.', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line));

            foreach (var line in loadoutLines)
            {
                if (!TryGetLoadoutWeaponsForModel(line, modelName, modelQuantity, out var weaponNames, out var appliedModelCount))
                {
                    continue;
                }

                foreach (var weaponName in weaponNames)
                {
                    var quantity = 1;
                    var cleanedWeaponName = weaponName;
                    var quantityMatch = Regex.Match(weaponName, @"^(\d+)\s+(.+)$");
                    if (quantityMatch.Success)
                    {
                        quantity = int.Parse(quantityMatch.Groups[1].Value);
                        cleanedWeaponName = quantityMatch.Groups[2].Value.Trim();
                    }

                    var wargear = FindWargear(allWargear, cleanedWeaponName);
                    if (wargear == null)
                    {
                        continue;
                    }

                    weapons.Add(new ParsedLoadoutWeapon(wargear, Math.Max(1, quantity * appliedModelCount)));
                }
            }

            return weapons
                .GroupBy(weapon => weapon.Wargear.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group => new ParsedLoadoutWeapon(group.First().Wargear, group.Sum(weapon => weapon.Quantity)))
                .ToList();
        }

        public static List<ParsedLoadoutWeapon> ParseStartingWargearForModel(DatasheetDetail detail, string modelName, int modelQuantity = 1)
        {
            var loadoutWeapons = ParseLoadoutForModel(detail.Loadout, modelName, detail.Wargear, modelQuantity);
            var optionWeapons = ParseOptionEquippedWargearForModel(detail, modelName);

            return loadoutWeapons
                .Concat(optionWeapons)
                .GroupBy(weapon => weapon.Wargear.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group => new ParsedLoadoutWeapon(group.First().Wargear, group.Sum(weapon => weapon.Quantity)))
                .ToList();
        }

        public static string FormatComposition(IEnumerable<DatasheetCompositionEntry> entries)
            => string.Join(" and ", entries.Select(entry => $"{entry.Quantity} {entry.Name}"));

        public static List<string> GetRawCompositionDescriptions(IEnumerable<DatasheetUnitComposition> unitComposition)
        {
            return unitComposition
                .Select(entry => HtmlUtility.ConvertHtmlToPlainText(entry.Description).Trim())
                .Where(entry => !string.IsNullOrWhiteSpace(entry) && !entry.Equals("OR", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private static List<DatasheetCompositionEntry> ParseCompositionEntries(string description)
        {
            var matches = Regex.Matches(description, @"(\d+)(?:-(\d+))?\s+(.+?)(?=(?:\s+and\s+\d)|$)", RegexOptions.IgnoreCase);
            var entries = new List<DatasheetCompositionEntry>();

            foreach (Match match in matches)
            {
                if (!match.Success)
                {
                    continue;
                }

                var minQuantity = int.Parse(match.Groups[1].Value);
                var maxQuantity = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : minQuantity;
                var name = match.Groups[3].Value.Trim();
                entries.Add(new DatasheetCompositionEntry(name, minQuantity, maxQuantity));
            }

            return entries;
        }

        private static bool TryGetLoadoutWeaponsForModel(string loadoutLine, string modelName, int modelQuantity, out List<string> weaponNames, out int appliedModelCount)
        {
            weaponNames = new List<string>();
            appliedModelCount = 0;
            var line = loadoutLine.Trim();
            var appliesToModel = false;
            var weaponsPart = string.Empty;

            var theModelMatch = Regex.Match(line, @"^The (.+?) is equipped with:(.+)$", RegexOptions.IgnoreCase);
            if (theModelMatch.Success)
            {
                weaponsPart = theModelMatch.Groups[2].Value.Trim();
                var targetModel = theModelMatch.Groups[1].Value.Trim();
                appliesToModel = ModelNamesEquivalent(targetModel, modelName);
                appliedModelCount = appliesToModel ? Math.Min(1, Math.Max(1, modelQuantity)) : 0;
            }

            var everyModelMatch = Regex.Match(line, @"^(?:Every|Each) (.+?) (?:is|are) equipped with:(.+)$", RegexOptions.IgnoreCase);
            if (everyModelMatch.Success)
            {
                weaponsPart = everyModelMatch.Groups[2].Value.Trim();
                var targetModel = everyModelMatch.Groups[1].Value.Trim();
                appliesToModel = ModelNamesEquivalent(targetModel, modelName);
                appliedModelCount = appliesToModel ? Math.Max(1, modelQuantity) : 0;
            }

            var countedModelMatch = Regex.Match(line, @"^(one|two|three|four|five|six|seven|eight|nine|ten|\d+) (.+?) (?:is|are) equipped with:(.+)$", RegexOptions.IgnoreCase);
            if (countedModelMatch.Success)
            {
                weaponsPart = countedModelMatch.Groups[3].Value.Trim();
                var targetModel = countedModelMatch.Groups[2].Value.Trim();
                appliesToModel = ModelNamesEquivalent(targetModel, modelName);
                appliedModelCount = appliesToModel
                    ? Math.Min(ParseModelCount(countedModelMatch.Groups[1].Value), Math.Max(1, modelQuantity))
                    : 0;
            }

            var genericModelMatch = Regex.Match(line, @"^(?:This model|Every model) is equipped with:(.+)$", RegexOptions.IgnoreCase);
            if (genericModelMatch.Success)
            {
                weaponsPart = genericModelMatch.Groups[1].Value.Trim();
                appliesToModel = true;
                appliedModelCount = Math.Max(1, modelQuantity);
            }

            if (!appliesToModel || string.IsNullOrWhiteSpace(weaponsPart))
            {
                return false;
            }

            weaponNames = weaponsPart.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .ToList();

            return weaponNames.Count > 0;
        }

        private static bool ModelNamesEquivalent(string left, string right)
        {
            var normalizedLeft = NormalizeName(left);
            var normalizedRight = NormalizeName(right);

            return normalizedLeft == normalizedRight;
        }

        private static int ParseModelCount(string value)
            => value.Trim().ToLowerInvariant() switch
            {
                "one" => 1,
                "two" => 2,
                "three" => 3,
                "four" => 4,
                "five" => 5,
                "six" => 6,
                "seven" => 7,
                "eight" => 8,
                "nine" => 9,
                "ten" => 10,
                _ => int.TryParse(value, out var count) ? Math.Max(1, count) : 1
            };

        private static DatasheetWargear? FindWargear(IEnumerable<DatasheetWargear> allWargear, string weaponName)
        {
            var wargearList = allWargear.ToList();
            var singularWeaponName = weaponName.EndsWith('s') ? weaponName[..^1] : null;
            var normalizedWeaponName = NormalizeName(weaponName);
            var normalizedSingularWeaponName = singularWeaponName == null ? null : NormalizeName(singularWeaponName);
            var compactWeaponName = normalizedWeaponName.Replace(" ", string.Empty, StringComparison.Ordinal);
            var compactSingularWeaponName = normalizedSingularWeaponName?.Replace(" ", string.Empty, StringComparison.Ordinal);

            return wargearList.FirstOrDefault(wargear =>
                       wargear.Name.Equals(weaponName, StringComparison.OrdinalIgnoreCase))
                   ?? (singularWeaponName != null
                       ? wargearList.FirstOrDefault(wargear =>
                           wargear.Name.Equals(singularWeaponName, StringComparison.OrdinalIgnoreCase))
                       : null)
                   ?? wargearList.FirstOrDefault(wargear =>
                       NormalizeName(wargear.Name) == normalizedWeaponName)
                   ?? (normalizedSingularWeaponName != null
                       ? wargearList.FirstOrDefault(wargear =>
                           NormalizeName(wargear.Name) == normalizedSingularWeaponName)
                       : null)
                   ?? wargearList.FirstOrDefault(wargear =>
                       NormalizeName(wargear.Name).Replace(" ", string.Empty, StringComparison.Ordinal) == compactWeaponName)
                   ?? (compactSingularWeaponName != null
                       ? wargearList.FirstOrDefault(wargear =>
                           NormalizeName(wargear.Name).Replace(" ", string.Empty, StringComparison.Ordinal) == compactSingularWeaponName)
                       : null)
                   ?? wargearList.FirstOrDefault(wargear =>
                       weaponName.Contains(wargear.Name, StringComparison.OrdinalIgnoreCase));
        }

        private static List<ParsedLoadoutWeapon> ParseOptionEquippedWargearForModel(DatasheetDetail detail, string modelName)
        {
            if (detail.Options.Count == 0)
            {
                return [];
            }

            var aliases = GetModelAliases(detail, modelName);
            var weapons = new List<ParsedLoadoutWeapon>();

            foreach (var option in detail.Options)
            {
                var text = HtmlUtility.StripHtml(option.Description).Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var match = Regex.Match(text, @"^(?<target>.+?) (?:is|are) equipped with:(?<weapons>.+)$", RegexOptions.IgnoreCase);
                if (!match.Success)
                {
                    continue;
                }

                var target = match.Groups["target"].Value.Trim();
                if (!aliases.Contains(NormalizeName(target)))
                {
                    continue;
                }

                var optionWeaponNames = match.Groups["weapons"].Value
                    .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => part.Trim())
                    .Where(part => !string.IsNullOrWhiteSpace(part));

                foreach (var optionWeaponName in optionWeaponNames)
                {
                    var wargear = FindWargear(detail.Wargear, optionWeaponName);
                    if (wargear == null)
                    {
                        continue;
                    }

                    weapons.Add(new ParsedLoadoutWeapon(wargear, 1));
                }
            }

            return weapons;
        }

        private static HashSet<string> GetModelAliases(DatasheetDetail detail, string modelName)
        {
            var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                NormalizeName(modelName)
            };

            foreach (var compositionEntry in detail.UnitComposition)
            {
                var plainText = HtmlUtility.ConvertHtmlToPlainText(compositionEntry.Description).Trim();
                if (string.IsNullOrWhiteSpace(plainText) || plainText.Equals("OR", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var genericNameMatch = Regex.Match(plainText, @"^\d+(?:-\d+)?\s+(?<name>[^:(]+)");
                if (!genericNameMatch.Success)
                {
                    continue;
                }

                var genericName = genericNameMatch.Groups["name"].Value.Trim();
                if (!string.Equals(NormalizeName(genericName), NormalizeName(modelName), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (Match parentheticalMatch in Regex.Matches(plainText, @"\((?<name>[^\)]+)\)"))
                {
                    aliases.Add(NormalizeName(parentheticalMatch.Groups["name"].Value.Trim()));
                }

                if (compositionEntry.Description.Contains("<li>", StringComparison.OrdinalIgnoreCase))
                {
                    var document = new HtmlDocument();
                    document.LoadHtml(compositionEntry.Description);
                    var listNodes = document.DocumentNode.SelectNodes("//li");
                    if (listNodes == null)
                    {
                        continue;
                    }

                    foreach (var node in listNodes)
                    {
                        var alias = HtmlUtility.ConvertHtmlToPlainText(node.InnerHtml).Trim();
                        if (!string.IsNullOrWhiteSpace(alias))
                        {
                            aliases.Add(NormalizeName(alias));
                        }
                    }
                }
            }

            return aliases;
        }

        private static bool MatchesCompositionOption(IEnumerable<DatasheetCompositionEntry> option, IEnumerable<CostDescriptionEntry> costEntries)
        {
            var optionList = option.ToList();
            var costList = costEntries.ToList();

            if (costList.Count == 1 && costList[0].IsGenericModelCount)
            {
                return optionList.Sum(entry => entry.Quantity) == costList[0].Quantity;
            }

            if (optionList.Count != costList.Count)
            {
                return false;
            }

            foreach (var costEntry in costList)
            {
                var matchingEntry = optionList.FirstOrDefault(entry => NamesMatch(entry.Name, costEntry.Name));
                if (matchingEntry == null || matchingEntry.Quantity != costEntry.Quantity)
                {
                    return false;
                }
            }

            return true;
        }

        private static List<CostDescriptionEntry> ParseCostDescriptionEntries(string description)
        {
            return description.Split(new[] { " and ", "," }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Select(ParseCostDescriptionEntry)
                .Where(entry => entry != null)
                .Cast<CostDescriptionEntry>()
                .ToList();
        }

        private static CostDescriptionEntry? ParseCostDescriptionEntry(string text)
        {
            var match = Regex.Match(text, @"^(\d+)\s+(.+)$", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }

            var quantity = int.Parse(match.Groups[1].Value);
            var name = match.Groups[2].Value.Trim();
            var normalizedName = NormalizeName(name);

            return new CostDescriptionEntry(quantity, name, normalizedName is "model" or "models");
        }

        private static bool NamesMatch(string modelName, string costName)
        {
            var normalizedModel = NormalizeName(modelName);
            var normalizedCost = NormalizeName(costName);

            return normalizedModel == normalizedCost
                   || normalizedModel.Contains(normalizedCost, StringComparison.OrdinalIgnoreCase)
                   || normalizedCost.Contains(normalizedModel, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeName(string value)
        {
            var normalizedWords = Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9\s]", " ")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeWord);

            return string.Join(' ', normalizedWords);
        }

        private static string NormalizeWord(string word)
        {
            if (word.Length > 3 && word.EndsWith("men", StringComparison.OrdinalIgnoreCase))
            {
                return $"{word[..^3]}man";
            }

            if (word.Length > 3 && word.EndsWith("ies", StringComparison.OrdinalIgnoreCase))
            {
                return $"{word[..^3]}y";
            }

            if (word.Length > 3 && word.EndsWith('s'))
            {
                return word[..^1];
            }

            return word;
        }

        private sealed record CostDescriptionEntry(int Quantity, string Name, bool IsGenericModelCount);
    }

    public sealed record DatasheetCompositionEntry(string Name, int MinQuantity, int MaxQuantity)
    {
        public int Quantity => MinQuantity;
    }

    public sealed record ParsedLoadoutWeapon(DatasheetWargear Wargear, int Quantity);
}
