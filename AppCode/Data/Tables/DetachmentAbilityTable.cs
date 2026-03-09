namespace OmniTactica.AppCode.Data.Tables
{
    /// <summary>
    /// Represents the Detachment_abilities table schema.
    /// This is separate from the general Abilities table.
    /// </summary>
    public record DetachmentAbilityTable(
        int Id,
        string FactionId,
        string Name,
        string Legend,
        string Description,
        string Detachment,
        int DetachmentId
    );
}
