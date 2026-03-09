namespace OmniTactica.AppCode.Data.Tables
{
    /// <summary>
    /// Represents the Source table schema.
    /// Contains information about codexes, supplements, and expansions.
    /// </summary>
    public record SourceTable(
        int Id,
        string Name,
        string Type,
        string Edition,
        string Version,
        string ErrataDate,
        string ErrataLink
    );
}
