using OmniTactica.AppCode.Models.Core;
using OmniTactica.AppCode.Repositories;

namespace OmniTactica.AppCode.Services
{
    /// <summary>
    /// High-level service for accessing Warhammer 40K game data.
    /// This is the primary API that UI components should use.
    /// </summary>
    public class WahaDataService
    {
        private readonly FactionRepository _factions;
        private readonly DetachmentRepository _detachments;

        public WahaDataService(
            FactionRepository factions,
            DetachmentRepository detachments)
        {
            _factions = factions;
            _detachments = detachments;
        }

        /// <summary>
        /// Gets all factions for display in faction list.
        /// </summary>
        public Task<List<Faction>> GetAllFactionsAsync()
            => _factions.GetAllFactionsAsync();

        /// <summary>
        /// Gets all keywords used in datasheets for a faction.
        /// </summary>
        public Task<List<string>> GetFactionKeywordsAsync(string factionId)
            => _factions.GetAllKeywordsForFactionAsync(factionId);

        /// <summary>
        /// Gets keywords grouped by frequency (common vs unique usage).
        /// </summary>
        public Task<(List<string> CommonKeywords, List<string> UniqueKeywords)> GetFactionKeywordsGroupedAsync(string factionId)
            => _factions.GetKeywordsGroupedByFrequencyAsync(factionId);

        /// <summary>
        /// Gets a faction with all its abilities for the faction viewer page.
        /// Optionally filters by selected keywords with AND/OR logic.
        /// </summary>
        public Task<Faction?> GetFactionWithAbilitiesAsync(string factionId, List<string>? keywordFilters = null, bool useAndLogic = false)
            => _factions.GetFactionWithAbilitiesAsync(factionId, keywordFilters, useAndLogic);

        /// <summary>
        /// Gets a complete faction overview including detachments.
        /// Optionally filters by selected keywords with AND/OR logic.
        /// Optimized to minimize database queries.
        /// </summary>
        public async Task<FactionOverview?> GetFactionOverviewAsync(string factionId, List<string>? keywordFilters = null, bool useAndLogic = false)
        {
            var faction = await _factions.GetFactionWithAbilitiesAsync(factionId, keywordFilters, useAndLogic);
            if (faction == null)
                return null;

            var detachments = await _detachments.GetByFactionAsync(factionId, keywordFilters, useAndLogic);

            return new FactionOverview
            {
                Faction = faction,
                Detachments = detachments
            };
        }

        /// <summary>
        /// Gets complete detachment details including all abilities, stratagems, and enhancements.
        /// </summary>
        public Task<Detachment?> GetDetachmentDetailsAsync(int detachmentId)
            => _detachments.GetDetachmentWithDetailsAsync(detachmentId);
    }

    /// <summary>
    /// View model for faction overview page containing faction and its detachments.
    /// </summary>
    public class FactionOverview
    {
        public Faction Faction { get; set; } = new();
        public List<Detachment> Detachments { get; set; } = new();
    }
}
