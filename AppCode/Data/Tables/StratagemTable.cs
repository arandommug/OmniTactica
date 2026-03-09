namespace OmniTactica.AppCode.Data.Tables
{
    /// <summary>
    /// Represents the Stratagems table schema.
    /// </summary>
    public record StratagemTable(
        int Id,
        string FactionId,
        string Name,
        string Type,
        string CpCost,
        string Legend,
        string Turn,
        string Phase,
        string Detachment,
        int DetachmentId,
        string Description
    );
}
