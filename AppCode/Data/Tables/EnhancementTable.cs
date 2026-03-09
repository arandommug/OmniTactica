namespace OmniTactica.AppCode.Data.Tables
{
    /// <summary>
    /// Represents the Enhancements table schema.
    /// </summary>
    public record EnhancementTable(
        int Id,
        string FactionId,
        string Name,
        int Cost,
        string Detachment,
        int DetachmentId,
        string Legend,
        string Description
    );
}
