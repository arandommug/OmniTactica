namespace OmniTactica.AppCode.Models.Core
{
    /// <summary>
    /// Represents a user bookmark for quick navigation.
    /// Supports different types of game content.
    /// </summary>
    public class Bookmark
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public BookmarkType Type { get; set; }
        public string EntityId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? FactionId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Navigation context for proper routing and UI state.
        /// </summary>
        public BookmarkNavigationContext? NavigationContext { get; set; }
    }

    /// <summary>
    /// Context information for navigating to a bookmarked item.
    /// </summary>
    public class BookmarkNavigationContext
    {
        /// <summary>
        /// Detachment ID if the bookmark is within a detachment context.
        /// </summary>
        public int? DetachmentId { get; set; }

        /// <summary>
        /// Datasheet ID if the bookmark is within a unit context.
        /// </summary>
        public int? DatasheetId { get; set; }

        /// <summary>
        /// Page route to navigate to.
        /// </summary>
        public string? Route { get; set; }

        /// <summary>
        /// Accordion element ID to expand (e.g., "ability-123").
        /// </summary>
        public string? AccordionId { get; set; }
    }

    public enum BookmarkType
    {
        Datasheet,
        Detachment,
        Stratagem,
        Ability,
        Enhancement,
        FactionRule
    }
}
