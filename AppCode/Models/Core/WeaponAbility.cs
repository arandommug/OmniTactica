namespace OmniTactica.AppCode.Models.Core
{
    /// <summary>
    /// Represents a single weapon ability with its properties.
    /// </summary>
    public class WeaponAbility
    {
        /// <summary>
        /// Unique identifier (e.g., "sustained_hits", "anti_vehicle")
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Display name (e.g., "Sustained Hits", "Anti-Vehicle")
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Numeric value for abilities with variable values (e.g., Rapid Fire X, Melta X)
        /// </summary>
        public int? Value { get; set; }

        /// <summary>
        /// Additional parameter for context (e.g., keyword for Anti abilities)
        /// </summary>
        public string? Parameter { get; set; }

        /// <summary>
        /// Whether this is a custom ability not defined in rules JSON
        /// </summary>
        public bool IsCustom { get; set; }

        /// <summary>
        /// Gets the full display name including value/parameter if applicable
        /// </summary>
        public string DisplayName
        {
            get
            {
                if (Value.HasValue)
                {
                    return $"{Name} {Value}";
                }
                else if (!string.IsNullOrEmpty(Parameter))
                {
                    return $"{Name} {Parameter}";
                }
                return Name;
            }
        }
    }
}
