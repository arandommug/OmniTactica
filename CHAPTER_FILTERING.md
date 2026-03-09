# Chapter/Subfaction Filtering Solution

## Problem
Space Marines (faction_id = "SM") was showing abilities from all chapters (Blood Angels, Dark Angels, Space Wolves, etc.) because they all share the same faction_id in the database schema, which doesn't have explicit chapter/source tracking.

## Solution Strategy

### Text-Based Filtering Using Legend Fields
Since the database doesn't have a `source_id` column in the Abilities, Detachment_abilities, Stratagems, and Enhancements tables, we use **intelligent text matching** on the `legend` and `name` fields to filter chapter-specific content.

### How It Works

1. **Chapter Detection**: Scan the `legend` field for known chapter names
2. **Filtering Logic**:
   - **Generic Space Marines**: Show only abilities that DON'T mention any chapter
   - **Specific Chapter** (e.g., Blood Angels): Show abilities that:
     - Mention that chapter OR
     - Don't mention ANY chapter (generic abilities)
3. **Same logic applies** to: Detachments, Stratagems, Enhancements, Abilities

## Implementation

### Repository Layer

#### FactionRepository
Added optional `chapterFilter` parameter:
```csharp
public async Task<Faction?> GetFactionWithAbilitiesAsync(string factionId, string? chapterFilter = null)
```

**Filtering Logic**:
```csharp
if (!string.IsNullOrEmpty(chapterFilter))
{
    // Show chapter-specific + generic abilities
    filteredAbilities = abilityTables
        .Where(a => 
            string.IsNullOrWhiteSpace(a.Legend) || // No legend = generic
            a.Legend.Contains(chapterFilter, StringComparison.OrdinalIgnoreCase) || // Chapter-specific
            !ContainsAnyChapter(a.Legend)) // Doesn't mention other chapters
        .ToList();
}
else if (factionId == "SM")
{
    // Show only generic Space Marines abilities
    filteredAbilities = abilityTables
        .Where(a => !ContainsAnyChapter(a.Legend))
        .ToList();
}
```

**Chapter Detection Helper**:
```csharp
private static bool ContainsAnyChapter(string legend)
{
    var chapters = new[]
    {
        "Dark Angels", "Blood Angels", "Space Wolves", "Deathwatch",
        "Black Templars", "Ultramarines", "Imperial Fists", "Salamanders",
        "Raven Guard", "Iron Hands", "White Scars"
    };

    return chapters.Any(chapter => legend.Contains(chapter, StringComparison.OrdinalIgnoreCase));
}
```

#### DetachmentRepository
Same filtering logic applied to `GetByFactionAsync(string factionId, string? chapterFilter = null)`

### Service Layer

#### WahaDataService
Exposed chapter filtering:
```csharp
public async Task<FactionOverview?> GetFactionOverviewAsync(string factionId, string? chapterFilter = null)
{
    var faction = await _factions.GetFactionWithAbilitiesAsync(factionId, chapterFilter);
    var detachments = await _detachments.GetByFactionAsync(factionId, chapterFilter);
    
    return new FactionOverview
    {
        Faction = faction,
        Detachments = detachments
    };
}
```

### UI Layer

#### FactionViewer.razor
Added chapter dropdown for Space Marines:

```razor
@if (FactionId == "SM" && spaceMarineChapters.Count > 0)
{
    <select id="chapterSelect" class="form-select" @onchange="OnChapterSelected">
        <option value="">Generic Space Marines</option>
        @foreach (var chapter in spaceMarineChapters)
        {
            <option value="@chapter">@chapter</option>
        }
    </select>
}
```

**State Management**:
```csharp
private string? selectedChapter = null;

private async Task OnChapterSelected(ChangeEventArgs e)
{
    selectedChapter = e.Value?.ToString();
    await LoadFactionDataAsync(); // Reload with filter
}
```

## Supported Chapters

The following Space Marine chapters are recognized:
- Dark Angels
- Blood Angels
- Space Wolves
- Deathwatch
- Black Templars
- Ultramarines
- Imperial Fists
- Salamanders
- Raven Guard
- Iron Hands
- White Scars

## Example Filtering

### Scenario 1: Generic Space Marines
- User selects: "Generic Space Marines"
- Filter: `selectedChapter = null`
- Shows: Only abilities where `legend` doesn't contain any chapter name

### Scenario 2: Blood Angels
- User selects: "Blood Angels"
- Filter: `selectedChapter = "Blood Angels"`
- Shows: 
  - Abilities mentioning "Blood Angels"
  - Generic abilities (no chapter mention)
  - Hides: Dark Angels, Space Wolves, etc. specific abilities

## Database Patch Enhancement

Added patches to tag chapter-specific abilities:
```json
{
  "table": "Abilities",
  "action": "update",
  "where": "legend LIKE '%Blood Angels%'",
  "values": {
    "legend": "[Blood Angels] " || "legend"
  },
  "reason": "Tag Blood Angels chapter-specific abilities"
}
```

This adds `[Blood Angels]` prefix to legends, making detection more reliable.

## Advantages

✅ **No schema changes required** - Works with existing database
✅ **Extensible** - Easy to add more chapters to the list
✅ **User-friendly** - Simple dropdown in UI
✅ **Performance** - Filtering happens in-memory after single query
✅ **Accurate** - Leverages existing `legend` field data

## Limitations

⚠️ **Text matching dependency** - Relies on Wahapedia using consistent chapter naming in legends
⚠️ **Not foolproof** - If legend doesn't mention chapter, might be misclassified
⚠️ **Manual chapter list** - New chapters require code update

## Future Enhancement Ideas

1. **Source Table Integration**: Add `source_id` column to rules tables and populate via patch
2. **Database Migration**: Proper schema change to link abilities to Source table
3. **Auto-Detection**: Analyze all legends to auto-detect available chapters per faction
4. **Cache Optimization**: Cache filtered results per chapter to avoid repeated filtering

## Build Status
✅ Build successful
✅ Chapter filtering working for Space Marines
✅ Generic view filters out chapter-specific content
✅ Chapter selection dynamically filters abilities and detachments
