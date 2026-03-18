using System.Text.RegularExpressions;
using HtmlAgilityPack;
using OmniTactica.AppCode.Models.Core;
using OmniTactica.AppCode.Utilities;

namespace OmniTactica.AppCode.Helpers
{
    public static class DatasheetWargearOptionHelper
    {
        public static IReadOnlyList<ParsedDatasheetWeaponOptionGroup> ParseWeaponOptionGroups(DatasheetDetail detail)
        {
            if (detail.Options.Count == 0 || detail.Wargear.Count == 0)
            {
                return [];
            }

            var orderedOptions = detail.Options
                .OrderBy(option => option.Line)
                .ToList();

            var groups = new List<ParsedDatasheetWeaponOptionGroup>();
            for (var index = 0; index < orderedOptions.Count; index++)
            {
                var option = orderedOptions[index];
                if (string.Equals(option.Button, "*", StringComparison.Ordinal))
                {
                    continue;
                }

                var footnote = OptionContainsFootnoteMarker(option.Description)
                    ? FindFollowingFootnote(orderedOptions, index + 1)
                    : string.Empty;

                foreach (var group in ParseWeaponOptionGroupsFromDescription(option.Line, option.Description, footnote, detail))
                {
                    groups.Add(group);
                }
            }

            return groups;
        }

        public static bool AppliesToModel(ParsedDatasheetWeaponOptionGroup group, string modelName)
        {
            if (group.AppliesToAnyModel)
            {
                return true;
            }

            var normalizedTarget = NormalizeModelTargetName(group.TargetModelName);
            var normalizedModel = NormalizeModelTargetName(modelName);

            return normalizedTarget == normalizedModel;
        }

        public static int GetMaximumSelections(ParsedDatasheetWeaponOptionGroup group, int unitTotalModels, int modelQuantity)
        {
            if (modelQuantity <= 0)
            {
                return 0;
            }

            return group.Limit.Kind switch
            {
                ParsedDatasheetWeaponOptionLimitKind.Single => 1,
                ParsedDatasheetWeaponOptionLimitKind.AnyNumber => modelQuantity,
                ParsedDatasheetWeaponOptionLimitKind.FixedCount => Math.Min(modelQuantity, Math.Max(0, group.Limit.SelectionsPerStep)),
                ParsedDatasheetWeaponOptionLimitKind.PerModelCount => Math.Min(
                    modelQuantity,
                    Math.Max(0, unitTotalModels / Math.Max(1, group.Limit.ModelsPerStep)) * Math.Max(1, group.Limit.SelectionsPerStep)),
                _ => 0
            };
        }

        public static bool IsAvailableForUnitSize(ParsedDatasheetWeaponOptionGroup group, int unitTotalModels)
        {
            if (group.Condition == null)
            {
                return true;
            }

            if (group.Condition.MinUnitModels.HasValue && unitTotalModels < group.Condition.MinUnitModels.Value)
            {
                return false;
            }

            if (group.Condition.MaxUnitModels.HasValue && unitTotalModels > group.Condition.MaxUnitModels.Value)
            {
                return false;
            }

            return true;
        }

        public static int? GetChoiceLimitPerUnit(ParsedDatasheetWeaponOptionGroup group, int unitTotalModels)
        {
            if (group.DuplicateChoiceLimit == null)
            {
                return null;
            }

            var limit = group.DuplicateChoiceLimit;
            if (limit.ThresholdModelCount.HasValue && unitTotalModels >= limit.ThresholdModelCount.Value)
            {
                return limit.ThresholdMax;
            }

            return limit.DefaultMax;
        }

        private static IReadOnlyList<ParsedDatasheetWeaponOptionGroup> ParseWeaponOptionGroupsFromDescription(int line, string descriptionHtml, string footnoteHtml, DatasheetDetail detail)
        {
            var plainText = ConvertOptionHtmlToPatternText(descriptionHtml);
            if (string.IsNullOrWhiteSpace(plainText))
            {
                return [];
            }

            var condition = ParseCondition(ref plainText);
            plainText = ExpandCompoundOptionChoices(plainText);

            var clauses = SplitIntoClauses(plainText);
            if (clauses.Count > 1)
            {
                return clauses
                    .Select((clause, index) => ParseWeaponOptionGroup($"option-{line}-{index}", line, clause, string.Empty, footnoteHtml, detail, condition))
                    .Where(group => group != null && group.Choices.Count > 0)
                    .Cast<ParsedDatasheetWeaponOptionGroup>()
                    .ToList();
            }

            var group = ParseWeaponOptionGroup($"option-{line}", line, plainText, descriptionHtml, footnoteHtml, detail, condition);
            return group == null || group.Choices.Count == 0 ? [] : [group];
        }

        private static string ConvertOptionHtmlToPatternText(string descriptionHtml)
        {
            if (string.IsNullOrWhiteSpace(descriptionHtml))
            {
                return string.Empty;
            }

            var html = Regex.Replace(descriptionHtml, @"<(br|/p|/div)\b[^>]*>", " ", RegexOptions.IgnoreCase);
            var listMatch = Regex.Match(html, @"<(ul|ol)\b", RegexOptions.IgnoreCase);
            if (listMatch.Success)
            {
                html = html[..listMatch.Index];
            }

            return NormalizeText(HtmlUtility.ConvertHtmlToPlainText(html));
        }

        private static ParsedDatasheetWeaponOptionGroup? ParseWeaponOptionGroup(string id, int line, string plainText, string choiceSourceHtml, string footnoteHtml, DatasheetDetail detail, ParsedDatasheetWeaponOptionCondition? condition)
        {

            foreach (var pattern in GetPatterns())
            {
                var match = pattern.Regex.Match(plainText);
                if (!match.Success)
                {
                    continue;
                }

                var sourceWeapons = new List<ParsedDatasheetWeaponOptionItem>();
                if (!string.IsNullOrWhiteSpace(pattern.SourceGroup))
                {
                    var sourceText = CleanWeaponClause(match.Groups[pattern.SourceGroup].Value);
                    if (string.IsNullOrWhiteSpace(sourceText) || sourceText.Contains(" or ", StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }

                    sourceWeapons = ParseWeaponSet(sourceText, detail);
                    if (sourceWeapons.Count == 0)
                    {
                        return null;
                    }
                }

                var choiceTexts = GetChoiceTexts(choiceSourceHtml, plainText, match, pattern.ChoiceGroup);
                var choices = choiceTexts
                    .Select((choiceText, choiceIndex) => ParseChoice(line, choiceIndex, choiceText, detail))
                    .Where(choice => choice != null)
                    .Cast<ParsedDatasheetWeaponOptionChoice>()
                    .ToList();

                if (choices.Count == 0)
                {
                    return null;
                }

                return new ParsedDatasheetWeaponOptionGroup(
                    id,
                    line,
                    CleanTargetModelName(match.Groups[pattern.TargetGroup].Value),
                    IsGenericModelTarget(match.Groups[pattern.TargetGroup].Value),
                    sourceWeapons,
                    choices,
                    pattern.CreateLimit(match),
                    condition,
                    ParseDuplicateChoiceLimit(footnoteHtml),
                    plainText,
                    string.IsNullOrWhiteSpace(footnoteHtml) ? string.Empty : NormalizeText(HtmlUtility.ConvertHtmlToPlainText(footnoteHtml)));
            }

            return null;
        }

        private static ParsedDatasheetWeaponOptionCondition? ParseCondition(ref string text)
        {
            var patterns = new[]
            {
                (Regex: new Regex(@"^If this unit contains only (?<count>\d+) models[,|:]\s*(?<rest>.+)$", RegexOptions.IgnoreCase), Builder: new Func<Match, ParsedDatasheetWeaponOptionCondition>(match => new ParsedDatasheetWeaponOptionCondition(int.Parse(match.Groups["count"].Value), int.Parse(match.Groups["count"].Value), $"Only available for {match.Groups["count"].Value} models."))),
                (Regex: new Regex(@"^If this unit contains (?<count>\d+) models[,|:]\s*(?<rest>.+)$", RegexOptions.IgnoreCase), Builder: new Func<Match, ParsedDatasheetWeaponOptionCondition>(match => new ParsedDatasheetWeaponOptionCondition(int.Parse(match.Groups["count"].Value), int.Parse(match.Groups["count"].Value), $"Only available for {match.Groups["count"].Value} models."))),
                (Regex: new Regex(@"^If this unit contains (?<count>\d+) or fewer models[,|:]\s*(?<rest>.+)$", RegexOptions.IgnoreCase), Builder: new Func<Match, ParsedDatasheetWeaponOptionCondition>(match => new ParsedDatasheetWeaponOptionCondition(null, int.Parse(match.Groups["count"].Value), $"Only available for {match.Groups["count"].Value} or fewer models."))),
                (Regex: new Regex(@"^If this unit contains (?<count>\d+) or more models[,|:]\s*(?<rest>.+)$", RegexOptions.IgnoreCase), Builder: new Func<Match, ParsedDatasheetWeaponOptionCondition>(match => new ParsedDatasheetWeaponOptionCondition(int.Parse(match.Groups["count"].Value), null, $"Only available for {match.Groups["count"].Value} or more models.")))
            };

            foreach (var pattern in patterns)
            {
                var match = pattern.Regex.Match(text);
                if (!match.Success)
                {
                    continue;
                }

                text = NormalizeText(match.Groups["rest"].Value);
                return pattern.Builder(match);
            }

            return null;
        }

        private static string ExpandCompoundOptionChoices(string text)
        {
            var match = Regex.Match(text, @"^The (?<target>.+?) can do one of the following:\s*(?<choices>.+)$", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return text;
            }

            var target = NormalizeText(match.Groups["target"].Value);
            var clauses = match.Groups["choices"].Value
                .Split('.', StringSplitOptions.RemoveEmptyEntries)
                .Select(clause => NormalizeText(clause))
                .Where(clause => !string.IsNullOrWhiteSpace(clause))
                .Select(clause => clause.StartsWith("Replace its ", StringComparison.OrdinalIgnoreCase)
                    ? $"The {target} can {char.ToLowerInvariant(clause[0])}{clause[1..]}"
                    : clause.StartsWith("Be equipped with ", StringComparison.OrdinalIgnoreCase)
                        ? $"The {target} can {char.ToLowerInvariant(clause[0])}{clause[1..]}"
                        : clause)
                .ToList();

            return clauses.Count == 0 ? text : string.Join(" ", clauses);
        }

        private static List<string> SplitIntoClauses(string text)
        {
            var matches = Regex.Matches(text, @"(?<clause>(?:Up to|For every|Any number of|The |This model|One |one |\d+\s+).*?\bcan\b.*?)(?=(?:\s+(?:Up to|For every|Any number of|The |This model|One |one |\d+\s+)[^.]*?\bcan\b)|$)", RegexOptions.IgnoreCase);
            if (matches.Count <= 1)
            {
                return [text.Trim()];
            }

            return matches
                .Select(match => NormalizeText(match.Groups["clause"].Value).Trim('.', ' '))
                .Where(clause => !string.IsNullOrWhiteSpace(clause))
                .ToList();
        }

        private static IEnumerable<(Regex Regex, string TargetGroup, string SourceGroup, string? ChoiceGroup, Func<Match, ParsedDatasheetWeaponOptionLimit> CreateLimit)> GetPatterns()
        {
            yield return (
                new Regex(@"^For every (?<step>one|two|three|four|five|six|seven|eight|nine|ten|\d+) models in (?:this )?unit, up to (?<count>one|two|three|four|five|six|seven|eight|nine|ten|\d+) (?<target>.+?) can each have their (?<source>.+?) replaced with one of the following\*?:$", RegexOptions.IgnoreCase),
                "target",
                "source",
                null,
                match => new ParsedDatasheetWeaponOptionLimit(
                    ParsedDatasheetWeaponOptionLimitKind.PerModelCount,
                    ParseNumber(match.Groups["step"].Value),
                    ParseNumber(match.Groups["count"].Value)));

            yield return (
                new Regex(@"^For every (?<step>one|two|three|four|five|six|seven|eight|nine|ten|\d+) models in (?:this )?unit, (?<count>one|two|three|four|five|six|seven|eight|nine|ten|\d+) (?<target>.+?)'?s (?<source>.+?) can be replaced with one of the following\*?:$", RegexOptions.IgnoreCase),
                "target",
                "source",
                null,
                match => new ParsedDatasheetWeaponOptionLimit(
                    ParsedDatasheetWeaponOptionLimitKind.PerModelCount,
                    ParseNumber(match.Groups["step"].Value),
                    ParseNumber(match.Groups["count"].Value)));

            yield return (
                new Regex(@"^For every (?<step>one|two|three|four|five|six|seven|eight|nine|ten|\d+) models in (?:this )?unit, up to (?<count>one|two|three|four|five|six|seven|eight|nine|ten|\d+) (?<target>.+?) can each replace their (?<source>.+?) with one of the following\*?:$", RegexOptions.IgnoreCase),
                "target",
                "source",
                null,
                match => new ParsedDatasheetWeaponOptionLimit(
                    ParsedDatasheetWeaponOptionLimitKind.PerModelCount,
                    ParseNumber(match.Groups["step"].Value),
                    ParseNumber(match.Groups["count"].Value)));

            yield return (
                new Regex(@"^Up to (?<count>one|two|three|four|five|six|seven|eight|nine|ten|\d+) (?<target>.+?) can each have their (?<source>.+?) replaced with one of the following\*?:$", RegexOptions.IgnoreCase),
                "target",
                "source",
                null,
                match => new ParsedDatasheetWeaponOptionLimit(
                    ParsedDatasheetWeaponOptionLimitKind.FixedCount,
                    0,
                    ParseNumber(match.Groups["count"].Value)));

            yield return (
                new Regex(@"^Up to (?<count>one|two|three|four|five|six|seven|eight|nine|ten|\d+) (?<target>.+?) can each replace their (?<source>.+?) with one of the following\*?:$", RegexOptions.IgnoreCase),
                "target",
                "source",
                null,
                match => new ParsedDatasheetWeaponOptionLimit(
                    ParsedDatasheetWeaponOptionLimitKind.FixedCount,
                    0,
                    ParseNumber(match.Groups["count"].Value)));

            yield return (
                new Regex(@"^For every (?<step>one|two|three|four|five|six|seven|eight|nine|ten|\d+) models in (?:this )?unit, up to (?<count>one|two|three|four|five|six|seven|eight|nine|ten|\d+) (?<target>.+?) can each have their (?<source>.+?) replaced with (?<choice>.+)$", RegexOptions.IgnoreCase),
                "target",
                "source",
                "choice",
                match => new ParsedDatasheetWeaponOptionLimit(
                    ParsedDatasheetWeaponOptionLimitKind.PerModelCount,
                    ParseNumber(match.Groups["step"].Value),
                    ParseNumber(match.Groups["count"].Value)));

            yield return (
                new Regex(@"^For every (?<step>one|two|three|four|five|six|seven|eight|nine|ten|\d+) models in (?:this )?unit, (?<count>one|two|three|four|five|six|seven|eight|nine|ten|\d+) (?<target>.+?)'?s (?<source>.+?) can be replaced with (?<choice>.+)$", RegexOptions.IgnoreCase),
                "target",
                "source",
                "choice",
                match => new ParsedDatasheetWeaponOptionLimit(
                    ParsedDatasheetWeaponOptionLimitKind.PerModelCount,
                    ParseNumber(match.Groups["step"].Value),
                    ParseNumber(match.Groups["count"].Value)));

            yield return (
                new Regex(@"^For every (?<step>one|two|three|four|five|six|seven|eight|nine|ten|\d+) models in (?:this )?unit, up to (?<count>one|two|three|four|five|six|seven|eight|nine|ten|\d+) (?<target>.+?) can each replace their (?<source>.+?) with (?<choice>.+)$", RegexOptions.IgnoreCase),
                "target",
                "source",
                "choice",
                match => new ParsedDatasheetWeaponOptionLimit(
                    ParsedDatasheetWeaponOptionLimitKind.PerModelCount,
                    ParseNumber(match.Groups["step"].Value),
                    ParseNumber(match.Groups["count"].Value)));

            yield return (
                new Regex(@"^Up to (?<count>one|two|three|four|five|six|seven|eight|nine|ten|\d+) (?<target>.+?) can each have their (?<source>.+?) replaced with (?<choice>.+)$", RegexOptions.IgnoreCase),
                "target",
                "source",
                "choice",
                match => new ParsedDatasheetWeaponOptionLimit(
                    ParsedDatasheetWeaponOptionLimitKind.FixedCount,
                    0,
                    ParseNumber(match.Groups["count"].Value)));

            yield return (
                new Regex(@"^Up to (?<count>one|two|three|four|five|six|seven|eight|nine|ten|\d+) (?<target>.+?) can each replace their (?<source>.+?) with (?<choice>.+)$", RegexOptions.IgnoreCase),
                "target",
                "source",
                "choice",
                match => new ParsedDatasheetWeaponOptionLimit(
                    ParsedDatasheetWeaponOptionLimitKind.FixedCount,
                    0,
                    ParseNumber(match.Groups["count"].Value)));

            yield return (
                new Regex(@"^(?<count>one|two|three|four|five|six|seven|eight|nine|ten|\d+) (?<target>.+?)'?s (?<source>.+?) can be replaced with one of the following:?$", RegexOptions.IgnoreCase),
                "target",
                "source",
                null,
                match => new ParsedDatasheetWeaponOptionLimit(
                    ParsedDatasheetWeaponOptionLimitKind.FixedCount,
                    0,
                    ParseNumber(match.Groups["count"].Value)));

            yield return (
                new Regex(@"^(?<count>one|two|three|four|five|six|seven|eight|nine|ten|\d+) (?<target>.+?)'?s (?<source>.+?) can be replaced with (?<choice>.+)$", RegexOptions.IgnoreCase),
                "target",
                "source",
                "choice",
                match => new ParsedDatasheetWeaponOptionLimit(
                    ParsedDatasheetWeaponOptionLimitKind.FixedCount,
                    0,
                    ParseNumber(match.Groups["count"].Value)));

            yield return (
                new Regex(@"^(?<count>one|two|three|four|five|six|seven|eight|nine|ten|\d+) (?<target>.+?) can replace its (?<source>.+?) with one of the following:?$", RegexOptions.IgnoreCase),
                "target",
                "source",
                null,
                match => new ParsedDatasheetWeaponOptionLimit(
                    ParsedDatasheetWeaponOptionLimitKind.FixedCount,
                    0,
                    ParseNumber(match.Groups["count"].Value)));

            yield return (
                new Regex(@"^(?<count>one|two|three|four|five|six|seven|eight|nine|ten|\d+) (?<target>.+?) can replace its (?<source>.+?) with (?<choice>.+)$", RegexOptions.IgnoreCase),
                "target",
                "source",
                "choice",
                match => new ParsedDatasheetWeaponOptionLimit(
                    ParsedDatasheetWeaponOptionLimitKind.FixedCount,
                    0,
                    ParseNumber(match.Groups["count"].Value)));

            yield return (
                new Regex(@"^Any number of (?<target>.+?) can each have their (?<source>.+?) replaced with one of the following\*?:$", RegexOptions.IgnoreCase),
                "target",
                "source",
                null,
                _ => new ParsedDatasheetWeaponOptionLimit(ParsedDatasheetWeaponOptionLimitKind.AnyNumber, 0, 0));

            yield return (
                new Regex(@"^Any number of (?<target>.+?) can each replace their (?<source>.+?) with one of the following\*?:$", RegexOptions.IgnoreCase),
                "target",
                "source",
                null,
                _ => new ParsedDatasheetWeaponOptionLimit(ParsedDatasheetWeaponOptionLimitKind.AnyNumber, 0, 0));

            yield return (
                new Regex(@"^Any number of (?<target>.+?) can each have their (?<source>.+?) replaced with (?<choice>.+)$", RegexOptions.IgnoreCase),
                "target",
                "source",
                "choice",
                _ => new ParsedDatasheetWeaponOptionLimit(ParsedDatasheetWeaponOptionLimitKind.AnyNumber, 0, 0));

            yield return (
                new Regex(@"^Any number of (?<target>.+?) can each replace their (?<source>.+?) with (?<choice>.+)$", RegexOptions.IgnoreCase),
                "target",
                "source",
                "choice",
                _ => new ParsedDatasheetWeaponOptionLimit(ParsedDatasheetWeaponOptionLimitKind.AnyNumber, 0, 0));

            yield return (
                new Regex(@"^The (?<target>.+?)'?s (?<source>.+?) can be replaced with one of the following:?$", RegexOptions.IgnoreCase),
                "target",
                "source",
                null,
                _ => new ParsedDatasheetWeaponOptionLimit(ParsedDatasheetWeaponOptionLimitKind.Single, 0, 0));

            yield return (
                new Regex(@"^The (?<target>.+?) can replace its (?<source>.+?) with one of the following:?$", RegexOptions.IgnoreCase),
                "target",
                "source",
                null,
                _ => new ParsedDatasheetWeaponOptionLimit(ParsedDatasheetWeaponOptionLimitKind.Single, 0, 0));

            yield return (
                new Regex(@"^The (?<target>.+?)'?s (?<source>.+?) can be replaced with (?<choice>.+)$", RegexOptions.IgnoreCase),
                "target",
                "source",
                "choice",
                _ => new ParsedDatasheetWeaponOptionLimit(ParsedDatasheetWeaponOptionLimitKind.Single, 0, 0));

            yield return (
                new Regex(@"^The (?<target>.+?) can replace its (?<source>.+?) with (?<choice>.+)$", RegexOptions.IgnoreCase),
                "target",
                "source",
                "choice",
                _ => new ParsedDatasheetWeaponOptionLimit(ParsedDatasheetWeaponOptionLimitKind.Single, 0, 0));

            yield return (
                new Regex(@"^For every (?<step>one|two|three|four|five|six|seven|eight|nine|ten|\d+) models in (?:this )?unit, (?<count>one|two|three|four|five|six|seven|eight|nine|ten|\d+) (?<target>.+?) can be equipped with one of the following:?$", RegexOptions.IgnoreCase),
                "target",
                string.Empty,
                null,
                match => new ParsedDatasheetWeaponOptionLimit(
                    ParsedDatasheetWeaponOptionLimitKind.PerModelCount,
                    ParseNumber(match.Groups["step"].Value),
                    ParseNumber(match.Groups["count"].Value)));

            yield return (
                new Regex(@"^For every (?<step>one|two|three|four|five|six|seven|eight|nine|ten|\d+) models in (?:this )?unit, (?<count>one|two|three|four|five|six|seven|eight|nine|ten|\d+) (?<target>.+?) can be equipped with (?<choice>.+)$", RegexOptions.IgnoreCase),
                "target",
                string.Empty,
                "choice",
                match => new ParsedDatasheetWeaponOptionLimit(
                    ParsedDatasheetWeaponOptionLimitKind.PerModelCount,
                    ParseNumber(match.Groups["step"].Value),
                    ParseNumber(match.Groups["count"].Value)));

            yield return (
                new Regex(@"^One (?<target>.+?) can be equipped with one of the following:?$", RegexOptions.IgnoreCase),
                "target",
                string.Empty,
                null,
                _ => new ParsedDatasheetWeaponOptionLimit(ParsedDatasheetWeaponOptionLimitKind.Single, 0, 0));

            yield return (
                new Regex(@"^One (?<target>.+?) can be equipped with (?<choice>.+)$", RegexOptions.IgnoreCase),
                "target",
                string.Empty,
                "choice",
                _ => new ParsedDatasheetWeaponOptionLimit(ParsedDatasheetWeaponOptionLimitKind.Single, 0, 0));

            yield return (
                new Regex(@"^(?<count>one|two|three|four|five|six|seven|eight|nine|ten|\d+) (?<target>.+?) can be equipped with one of the following:?$", RegexOptions.IgnoreCase),
                "target",
                string.Empty,
                null,
                match => new ParsedDatasheetWeaponOptionLimit(
                    ParsedDatasheetWeaponOptionLimitKind.FixedCount,
                    0,
                    ParseNumber(match.Groups["count"].Value)));

            yield return (
                new Regex(@"^(?<count>one|two|three|four|five|six|seven|eight|nine|ten|\d+) (?<target>.+?) can be equipped with (?<choice>.+)$", RegexOptions.IgnoreCase),
                "target",
                string.Empty,
                "choice",
                match => new ParsedDatasheetWeaponOptionLimit(
                    ParsedDatasheetWeaponOptionLimitKind.FixedCount,
                    0,
                    ParseNumber(match.Groups["count"].Value)));

            yield return (
                new Regex(@"^The (?<target>.+?) can be equipped with one of the following:?$", RegexOptions.IgnoreCase),
                "target",
                string.Empty,
                null,
                _ => new ParsedDatasheetWeaponOptionLimit(ParsedDatasheetWeaponOptionLimitKind.Single, 0, 0));

            yield return (
                new Regex(@"^The (?<target>.+?) can be equipped with (?<choice>.+)$", RegexOptions.IgnoreCase),
                "target",
                string.Empty,
                "choice",
                _ => new ParsedDatasheetWeaponOptionLimit(ParsedDatasheetWeaponOptionLimitKind.Single, 0, 0));

            yield return (
                new Regex(@"^(?<target>This model)'?s (?<source>.+?) can be replaced with one of the following:?$", RegexOptions.IgnoreCase),
                "target",
                "source",
                null,
                _ => new ParsedDatasheetWeaponOptionLimit(ParsedDatasheetWeaponOptionLimitKind.Single, 0, 0));

            yield return (
                new Regex(@"^(?<target>This model)'?s (?<source>.+?) can be replaced with (?<choice>.+)$", RegexOptions.IgnoreCase),
                "target",
                "source",
                "choice",
                _ => new ParsedDatasheetWeaponOptionLimit(ParsedDatasheetWeaponOptionLimitKind.Single, 0, 0));

            yield return (
                new Regex(@"^(?<target>This model) can be equipped with one of the following:?$", RegexOptions.IgnoreCase),
                "target",
                string.Empty,
                null,
                _ => new ParsedDatasheetWeaponOptionLimit(ParsedDatasheetWeaponOptionLimitKind.Single, 0, 0));

            yield return (
                new Regex(@"^(?<target>This model) can be equipped with (?<choice>.+)$", RegexOptions.IgnoreCase),
                "target",
                string.Empty,
                "choice",
                _ => new ParsedDatasheetWeaponOptionLimit(ParsedDatasheetWeaponOptionLimitKind.Single, 0, 0));
        }

        private static ParsedDatasheetWeaponOptionChoice? ParseChoice(int line, int choiceIndex, string choiceText, DatasheetDetail detail)
        {
            var weapons = ParseWeaponSet(choiceText, detail);
            if (weapons.Count == 0)
            {
                return null;
            }

            return new ParsedDatasheetWeaponOptionChoice(
                $"option-{line}-choice-{choiceIndex}",
                FormatChoiceDisplay(weapons),
                weapons);
        }

        private static List<ParsedDatasheetWeaponOptionItem> ParseWeaponSet(string text, DatasheetDetail detail)
        {
            var cleanedText = CleanWeaponClause(text);
            if (string.IsNullOrWhiteSpace(cleanedText))
            {
                return [];
            }

            var items = cleanedText.Split(new[] { " and ", "," }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => ParseWeaponItem(item, detail))
                .Where(item => item != null)
                .Cast<ParsedDatasheetWeaponOptionItem>()
                .ToList();

            return items;
        }

        private static ParsedDatasheetWeaponOptionItem? ParseWeaponItem(string text, DatasheetDetail detail)
        {
            var match = Regex.Match(text, @"^(?:up to\s+)?(?:(?<count>one|two|three|four|five|six|seven|eight|nine|ten|\d+)\s+)?(?<name>.+)$", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }

            var quantity = match.Groups["count"].Success
                ? ParseNumber(match.Groups["count"].Value)
                : 1;
            var name = CleanWeaponName(match.Groups["name"].Value);
            if (quantity <= 0 || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var matchingNames = ResolveMatchingItemNames(detail, name);
            if (matchingNames.Count == 0)
            {
                return null;
            }

            return new ParsedDatasheetWeaponOptionItem(name, quantity, matchingNames);
        }

        private static List<string> GetChoiceTexts(string descriptionHtml, string plainText, Match match, string? choiceGroup)
        {
            var listItems = string.IsNullOrWhiteSpace(descriptionHtml)
                ? []
                : ExtractListItems(descriptionHtml);
            if (listItems.Count > 0)
            {
                return listItems;
            }

            if (string.IsNullOrWhiteSpace(choiceGroup) || !match.Groups.ContainsKey(choiceGroup))
            {
                return [];
            }

            var inlineChoice = CleanWeaponClause(match.Groups[choiceGroup].Value);
            return string.IsNullOrWhiteSpace(inlineChoice) ? [] : [inlineChoice];
        }

        private static List<string> ExtractListItems(string html)
        {
            var document = new HtmlDocument();
            document.LoadHtml(html);
            return document.DocumentNode
                .SelectNodes("//li")?
                .Select(node => NormalizeText(HtmlUtility.ConvertHtmlToPlainText(node.InnerHtml)))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList()
                ?? [];
        }

        private static List<string> ResolveMatchingItemNames(DatasheetDetail detail, string itemName)
        {
            var normalizedItemName = NormalizeLookupName(itemName);
            var compactItemName = normalizedItemName.Replace(" ", string.Empty, StringComparison.Ordinal);
            var wargear = detail.Wargear;
            var wargearAbilityNames = detail.Abilities
                .Where(ability => string.Equals(ability.Type, "Wargear", StringComparison.OrdinalIgnoreCase))
                .Select(ability => ability.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var exactMatches = wargear
                .Where(entry => NormalizeLookupName(entry.Name) == normalizedItemName)
                .Select(entry => entry.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            exactMatches.AddRange(wargearAbilityNames
                .Where(name => NormalizeLookupName(name) == normalizedItemName));
            exactMatches = exactMatches.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            if (exactMatches.Count > 0)
            {
                return exactMatches;
            }

            var prefixMatches = wargear
                .Where(entry =>
                {
                    var normalizedEntry = NormalizeLookupName(entry.Name);
                    return normalizedEntry.StartsWith(normalizedItemName + " ", StringComparison.OrdinalIgnoreCase)
                           || normalizedEntry.Replace(" ", string.Empty, StringComparison.Ordinal).StartsWith(compactItemName, StringComparison.OrdinalIgnoreCase);
                })
                .Select(entry => entry.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            prefixMatches.AddRange(wargearAbilityNames.Where(name =>
            {
                var normalizedName = NormalizeLookupName(name);
                return normalizedName.StartsWith(normalizedItemName + " ", StringComparison.OrdinalIgnoreCase)
                       || normalizedName.Replace(" ", string.Empty, StringComparison.Ordinal).StartsWith(compactItemName, StringComparison.OrdinalIgnoreCase);
            }));
            prefixMatches = prefixMatches.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            if (prefixMatches.Count > 0)
            {
                return prefixMatches;
            }

            var containsMatches = wargear
                .Where(entry => itemName.Contains(entry.Name, StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            containsMatches.AddRange(wargearAbilityNames.Where(name => itemName.Contains(name, StringComparison.OrdinalIgnoreCase)));
            return containsMatches.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static ParsedDatasheetWeaponOptionDuplicateLimit? ParseDuplicateChoiceLimit(string footnoteHtml)
        {
            var footnote = NormalizeText(HtmlUtility.ConvertHtmlToPlainText(footnoteHtml));
            if (string.IsNullOrWhiteSpace(footnote))
            {
                return null;
            }

            var thresholdMatch = Regex.Match(footnote, @"more than (?<default>once|twice|thrice|\d+) per unit unless it contains (?<threshold>\d+) models, in which case .* more than (?<thresholdMax>once|twice|thrice|\d+) per unit", RegexOptions.IgnoreCase);
            if (thresholdMatch.Success)
            {
                return new ParsedDatasheetWeaponOptionDuplicateLimit(
                    ParseFrequency(thresholdMatch.Groups["default"].Value),
                    int.Parse(thresholdMatch.Groups["threshold"].Value),
                    ParseFrequency(thresholdMatch.Groups["thresholdMax"].Value));
            }

            var singleMatch = Regex.Match(footnote, @"more than (?<default>once|twice|thrice|\d+) per unit", RegexOptions.IgnoreCase);
            if (singleMatch.Success)
            {
                return new ParsedDatasheetWeaponOptionDuplicateLimit(
                    ParseFrequency(singleMatch.Groups["default"].Value),
                    null,
                    ParseFrequency(singleMatch.Groups["default"].Value));
            }

            return null;
        }

        private static bool OptionContainsFootnoteMarker(string description)
            => !string.IsNullOrWhiteSpace(description) && description.Contains('*');

        private static string FindFollowingFootnote(IReadOnlyList<DatasheetOption> options, int startIndex)
        {
            for (var index = startIndex; index < options.Count; index++)
            {
                var option = options[index];
                if (string.Equals(option.Button, "*", StringComparison.Ordinal))
                {
                    return option.Description;
                }

                if (!string.IsNullOrWhiteSpace(option.Button))
                {
                    break;
                }
            }

            return string.Empty;
        }

        private static string FormatChoiceDisplay(IEnumerable<ParsedDatasheetWeaponOptionItem> weapons)
            => string.Join(" + ", weapons.Select(weapon => weapon.Name));

        private static string CleanTargetModelName(string value)
            => Regex.Replace(NormalizeText(value), @"\s+equipped with.+$", string.Empty, RegexOptions.IgnoreCase)
                .Trim();

        private static bool IsGenericModelTarget(string value)
        {
            var plainText = NormalizeText(value).ToLowerInvariant();
            if (plainText.StartsWith("this model", StringComparison.OrdinalIgnoreCase)
                || plainText.StartsWith("one model", StringComparison.OrdinalIgnoreCase)
                || plainText.StartsWith("every model", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var normalized = NormalizeModelTargetName(value);
            return normalized is "model" or "models";
        }

        private static string NormalizeModelTargetName(string value)
            => NormalizeLookupName(value)
                .Replace("this ", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("one ", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("every ", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace(" equipped with", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace(" model", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim();

        private static string CleanWeaponClause(string value)
        {
            var cleaned = NormalizeText(value)
                .TrimEnd(':', '.', '*')
                .Trim();

            var parentheticalIndex = cleaned.IndexOf('(');
            if (parentheticalIndex >= 0)
            {
                cleaned = cleaned[..parentheticalIndex].Trim();
            }

            return cleaned;
        }

        private static string CleanWeaponName(string value)
            => CleanWeaponClause(value)
                .Replace("additional ", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("that model’s", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("that model's", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim();

        private static string NormalizeText(string value)
            => Regex.Replace(value.Replace('’', '\''), @"\s+", " ").Trim();

        private static string NormalizeLookupName(string value)
        {
            var normalizedWords = Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9\s]", " ")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => word.Length > 3 && word.EndsWith('s') ? word[..^1] : word);

            return string.Join(' ', normalizedWords);
        }

        private static int ParseNumber(string value)
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
                _ => int.TryParse(value, out var parsed) ? parsed : 0
            };

        private static int ParseFrequency(string value)
            => value.Trim().ToLowerInvariant() switch
            {
                "once" => 1,
                "twice" => 2,
                "thrice" => 3,
                _ => ParseNumber(value)
            };
    }

    public sealed record ParsedDatasheetWeaponOptionGroup(
        string Id,
        int Line,
        string TargetModelName,
        bool AppliesToAnyModel,
        IReadOnlyList<ParsedDatasheetWeaponOptionItem> SourceWeapons,
        IReadOnlyList<ParsedDatasheetWeaponOptionChoice> Choices,
        ParsedDatasheetWeaponOptionLimit Limit,
        ParsedDatasheetWeaponOptionCondition? Condition,
        ParsedDatasheetWeaponOptionDuplicateLimit? DuplicateChoiceLimit,
        string Description,
        string Footnote);

    public sealed record ParsedDatasheetWeaponOptionChoice(
        string Id,
        string DisplayName,
        IReadOnlyList<ParsedDatasheetWeaponOptionItem> AddedWeapons);

    public sealed record ParsedDatasheetWeaponOptionItem(
        string Name,
        int Quantity,
        IReadOnlyList<string> MatchingWargearNames);

    public sealed record ParsedDatasheetWeaponOptionLimit(
        ParsedDatasheetWeaponOptionLimitKind Kind,
        int ModelsPerStep,
        int SelectionsPerStep);

    public sealed record ParsedDatasheetWeaponOptionDuplicateLimit(
        int DefaultMax,
        int? ThresholdModelCount,
        int ThresholdMax);

    public sealed record ParsedDatasheetWeaponOptionCondition(
        int? MinUnitModels,
        int? MaxUnitModels,
        string Description);

    public enum ParsedDatasheetWeaponOptionLimitKind
    {
        Single,
        AnyNumber,
        FixedCount,
        PerModelCount
    }
}
