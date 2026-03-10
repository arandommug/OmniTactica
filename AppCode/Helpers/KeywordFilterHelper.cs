namespace OmniTactica.AppCode.Helpers;

public enum KeywordFilterState
{
    None = 0,
    Include = 1,
    Exclude = 2
}

/// <summary>
/// Helper methods for keyword filtering functionality.
/// </summary>
public static class KeywordFilterHelper
{
    /// <summary>
    /// Checks if the given text matches the specified keyword filters.
    /// Returns true only if there are include keywords AND the text matches them (and is not excluded).
    /// Use PassesFilter for broader filtering that includes "no filter" scenarios.
    /// </summary>
    public static bool MatchesKeywordFilter(string text, List<string> includeKeywords, List<string> excludeKeywords, bool useAndLogic)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        // If no include keywords, return false (nothing to actively match)
        if (includeKeywords.Count == 0)
            return false;

        // Check excludes - if any excluded keyword is present, filter out
        if (excludeKeywords.Count > 0)
        {
            if (excludeKeywords.Any(kw => text.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        if (useAndLogic)
        {
            // AND logic: must contain ALL include keywords
            return includeKeywords.All(kw => text.Contains(kw, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            // OR logic: must contain ANY include keyword
            return includeKeywords.Any(kw => text.Contains(kw, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Checks if an item passes the filter criteria (for determining if it should be shown).
    /// This is different from MatchesKeywordFilter - it returns true when there are no filters,
    /// or when the item matches include logic and is not excluded.
    /// </summary>
    public static bool PassesFilter(string text, List<string> includeKeywords, List<string> excludeKeywords, bool useAndLogic)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        // Check excludes first - if any excluded keyword is present, it doesn't pass
        if (excludeKeywords.Count > 0)
        {
            if (excludeKeywords.Any(kw => text.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        // If no include keywords, it passes (as long as it's not excluded, which we checked above)
        if (includeKeywords.Count == 0)
            return true;

        // Apply include logic
        if (useAndLogic)
        {
            // AND logic: must contain ALL include keywords
            return includeKeywords.All(kw => text.Contains(kw, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            // OR logic: must contain ANY include keyword
            return includeKeywords.Any(kw => text.Contains(kw, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Gets the list of matched keywords in the given text.
    /// </summary>
    public static List<string> GetMatchedKeywords(string text, List<string> keywords)
    {
        if (keywords.Count == 0 || string.IsNullOrWhiteSpace(text))
            return new List<string>();

        return keywords
            .Where(kw => text.Contains(kw, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
