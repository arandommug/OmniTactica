# Keyword Filtering System

## Overview
Replaced the subfaction detection system with a flexible keyword-based filtering approach that allows users to filter Army Abilities and Detachments by selecting keywords from the faction's datasheets.

## Features

### 1. **Keyword Selection Modal**
- Click "Filter by Keywords" button to open modal
- Displays all keywords used in the faction's datasheets as clickable badges
- Selected keywords are highlighted with a checkmark
- Shows count of selected keywords

### 2. **AND vs OR Logic**
- **OR Logic** (default): Shows abilities/detachments that mention **ANY** of the selected keywords
- **AND Logic**: Shows abilities/detachments that mention **ALL** selected keywords
- Toggle between modes using radio buttons in the modal

### 3. **Smart Filtering**

#### Army Abilities Filtering
- Searches both `legend` and `description` fields for keyword mentions
- Case-insensitive matching
- Debug logging shows which keywords matched for each ability

#### Detachment Filtering
- Checks if detachment's abilities, stratagems, OR enhancements mention the keywords
- Only shows detachments that have relevant content for the selected keywords
- Helps narrow down which detachments are useful for your army composition

### 4. **UI Feedback**
- Badge counter shows how many keywords are selected
- Displays selected keywords as badges below the filter button
- Shows "AND" or "OR" logic indicator
- "Clear Filter" button to quickly reset
- Detachment dropdown automatically resets when filter changes

## Technical Implementation

### Architecture Changes

**Removed:**
- `SubfactionDetector` class and dependency injection
- Subfaction-specific filtering logic
- Text pattern matching for subfaction ownership

**Added:**
- `GetFactionKeywordsAsync()` - Retrieves all distinct keywords for a faction
- `GetAllKeywordsForFactionAsync()` in `FactionRepository`
- AND/OR logic parameters throughout the filtering chain

### Database Queries

**Get Keywords:**
```sql
SELECT DISTINCT dk.keyword
FROM Datasheets_keywords dk
JOIN Datasheets d ON dk.datasheet_id = d.id
WHERE d.faction_id = @factionId
ORDER BY dk.keyword
```

**Filter Detachments:**
```sql
SELECT description FROM Detachment_abilities WHERE detachment_id = @id
UNION ALL
SELECT description FROM Stratagems WHERE detachment_id = @id
UNION ALL
SELECT description FROM Enhancements WHERE detachment_id = @id
```

### Method Signatures Changed

**WahaDataService:**
```csharp
Task<List<string>> GetFactionKeywordsAsync(string factionId)
Task<Faction?> GetFactionWithAbilitiesAsync(string factionId, List<string>? keywordFilters, bool useAndLogic)
Task<FactionOverview?> GetFactionOverviewAsync(string factionId, List<string>? keywordFilters, bool useAndLogic)
```

**FactionRepository:**
```csharp
Task<Faction?> GetFactionWithAbilitiesAsync(string factionId, List<string>? keywordFilters, bool useAndLogic)
Task<List<string>> GetAllKeywordsForFactionAsync(string factionId)
```

**DetachmentRepository:**
```csharp
Task<List<Detachment>> GetByFactionAsync(string factionId, List<string>? keywordFilters, bool useAndLogic)
```

## Usage Examples

### Example 1: Dark Angels Deathwing Army
1. Select "Space Marines" faction
2. Click "Filter by Keywords"
3. Select keywords: `DARK ANGELS`, `DEATHWING`, `TERMINATOR`
4. Choose "AND" logic
5. Apply filter
6. Result: Only shows abilities and detachments relevant to Dark Angels Terminator units

### Example 2: Drukhari Mixed Army
1. Select "Drukhari" faction
2. Click "Filter by Keywords"
3. Select keywords: `Kabal of the Black Heart`, `Wych Cult`
4. Choose "OR" logic
5. Apply filter
6. Result: Shows abilities and detachments relevant to either Kabal or Wych Cult units

### Example 3: View All (No Filter)
1. Don't select any keywords, or click "Clear Filter"
2. Result: Shows all abilities and all detachments

## Benefits

### 1. **Data-Driven**
- No hardcoded faction names or subfaction lists
- Automatically works for all factions based on actual datasheet keywords
- Keywords come directly from the game data

### 2. **Flexible**
- Users can mix and match keywords to explore different army compositions
- AND/OR logic provides precision or breadth as needed
- Works for simple subfaction filtering (e.g., "Dark Angels only")
- Also works for complex queries (e.g., "TERMINATOR and PSYKER")

### 3. **Scalable**
- Will be reused for datasheet filtering (future feature)
- Easy to extend with additional filtering options
- No maintenance needed when new codexes or datasheets are added

### 4. **Accurate**
- No false positives from text pattern matching
- No missed results from incomplete pattern libraries
- Based on actual game mechanics (keywords)

## Future Enhancements

### Planned Features:
1. **Search/Filter Keywords** - Add search box in modal to filter the keyword list
2. **Keyword Categories** - Group keywords by type (faction, battlefield role, unit type, etc.)
3. **Saved Filters** - Allow users to save and name common keyword combinations
4. **Datasheet Filtering** - Apply same system when browsing unit datasheets
5. **Smart Suggestions** - Suggest related keywords based on selections

### Possible Improvements:
- Keyword frequency counts (show how many datasheets have each keyword)
- Highlight keywords in ability descriptions
- Export filtered results
- Share filter configurations with others
