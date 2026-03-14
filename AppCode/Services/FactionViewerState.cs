namespace OmniTactica.AppCode.Services
{
    /// <summary>
    /// State management service for maintaining user selections across faction-related pages.
    /// Preserves keyword filters, detachment selections, and UI state during navigation.
    /// </summary>
    public class FactionViewerState
    {
        // Current faction context
        public string? CurrentFactionId { get; set; }

        // Keyword filtering state (shared across all pages)
        public List<string> SelectedKeywords { get; set; } = new();
        public List<string> ExcludedKeywords { get; set; } = new();
        public bool UseAndLogic { get; set; } = false;

        // Detachment selection (shared across pages)
        public int? SelectedDetachmentId { get; set; }

        // UI state for FactionViewer
        public int? SelectedFactionDetachmentId { get; set; }

        // UI state for UnitsViewer (scroll position, etc.)
        public string? UnitsViewerScrollPosition { get; set; }

        // UI state for UnitViewer
        public int? CurrentDatasheetId { get; set; }

        /// <summary>
        /// Resets all state when switching to a different faction.
        /// </summary>
        public void ResetForFaction(string factionId)
        {
            if (CurrentFactionId != factionId)
            {
                CurrentFactionId = factionId;
                SelectedKeywords.Clear();
                ExcludedKeywords.Clear();
                UseAndLogic = false;
                SelectedDetachmentId = null;
                SelectedFactionDetachmentId = null;
                UnitsViewerScrollPosition = null;
                CurrentDatasheetId = null;
            }
        }

        /// <summary>
        /// Updates keyword filter state.
        /// </summary>
        public void SetKeywordFilter(List<string> includeKeywords, List<string> excludeKeywords, bool useAndLogic)
        {
            SelectedKeywords = new List<string>(includeKeywords);
            ExcludedKeywords = new List<string>(excludeKeywords);
            UseAndLogic = useAndLogic;
        }

        /// <summary>
        /// Clears keyword filter.
        /// </summary>
        public void ClearKeywordFilter()
        {
            SelectedKeywords.Clear();
            ExcludedKeywords.Clear();
            UseAndLogic = false;
        }

        /// <summary>
        /// Sets the selected detachment (shared across pages).
        /// </summary>
        public void SetDetachment(int? detachmentId)
        {
            SelectedDetachmentId = detachmentId;
        }

        /// <summary>
        /// Preselects faction-scoped state before navigation.
        /// </summary>
        public void SetFactionContext(string factionId, int? detachmentId)
        {
            ResetForFaction(factionId);
            SelectedFactionDetachmentId = detachmentId;
            SelectedDetachmentId = detachmentId;
        }

        /// <summary>
        /// Checks if there are active keyword filters.
        /// </summary>
        public bool HasKeywordFilters => SelectedKeywords.Count > 0 || ExcludedKeywords.Count > 0;

        /// <summary>
        /// Checks if a detachment is selected.
        /// </summary>
        public bool HasDetachmentSelected => SelectedDetachmentId.HasValue && SelectedDetachmentId.Value > 0;
    }
}
