using OmniTactica.AppCode.Data.Database;

namespace OmniTactica.AppCode.Data.Tables
{
    /// <summary>
    /// Represents a keyword associated with a datasheet.
    /// </summary>
    public class DatasheetKeywordTable : TableBase
    {
        public int DatasheetId { get; set; }
        public string Keyword { get; set; } = string.Empty;
        public string? Model { get; set; }
        public bool IsFactionKeyword { get; set; }

        public DatasheetKeywordTable(WahaSQLiteService db) : base(db) { }
    }
}
