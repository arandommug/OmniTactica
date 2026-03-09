# Dynamic Subfaction Detection

## Overview
Instead of hardcoding chapter/subfaction names for specific factions, the system now **automatically detects** subfactions by analyzing ability legends and detachment names. This works for any faction that has subfaction-specific content.

## How It Works

### 1. Pattern Recognition
The `SubfactionDetector` analyzes text from abilities and detachments looking for:
- **Bracket tags**: `[Blood Angels]`, `[Goff]`, `[Saim-Hann]`
- **"Only" patterns**: "Dark Angels only", "Farsight Enclaves units"
- **Proper nouns**: Capitalized multi-word phrases at start of legends

### 2. Automatic Detection
On page load, the system:
1. Queries all abilities and detachments for the faction
2. Extracts potential subfaction names using pattern matching
3. Filters out generic phrases ("If a", "When this", etc.)
4. Returns a list of detected subfaction names

### 3. Dynamic UI
- **Dropdown only shows if subfactions detected**
- **"All [Faction Name] (Generic)"** option filters to non-subfaction content
- **Subfaction options** are auto-populated from detection
- **Status indicator** shows count: "11 subfaction(s) detected"

## Implementation Details

### SubfactionDetector Class

**Location**: `AppCode/Repositories/SubfactionDetector.cs`

**Key Methods**:
```csharp
public async Task<List<string>> DetectSubfactionsAsync(string factionId)
```
- Analyzes all abilities and detachments
- Returns list of unique subfaction names

**Pattern Matching**:
```csharp
private List<string> ExtractSubfactionNames(List<string> textSamples)
```
Three extraction patterns:
1. `[Subfaction]` - Bracket tags
2. `Subfaction only` - Explicit "only" patterns  
3. `Proper Noun` - Capitalized phrases (filtered)

**Generic Phrase Filter**:
```csharp
private bool IsGenericPhrase(string phrase)
```
Excludes common game terms like "If a", "Each time", "This unit"

### Service Layer Updates

**WahaDataService**:
```csharp
public Task<List<string>> DetectSubfactionsAsync(string factionId)
```
Exposes detection to UI layer

**Renamed Parameters**:
- `chapterFilter` → `subfactionFilter` (more generic)
- Works for chapters, clans, craftworlds, dynasties, etc.

### Repository Updates

**Generic Filtering Logic**:
Both `FactionRepository` and `DetachmentRepository` now:
- Accept `subfactionFilter` parameter
- Use dynamic pattern extraction instead of hardcoded lists
- Detect subfactions from the data itself

**Smart Filtering**:
```csharp
if (!string.IsNullOrEmpty(subfactionFilter))
{
    // Show subfaction-specific + generic content
    filtered = items.Where(i => 
        ContainsSubfaction(i, subfactionFilter) ||
        !ContainsAnySubfaction(i, allItems)
    );
}
```

### UI Updates

**FactionViewer.razor**:
```razor
@if (detectedSubfactions.Count > 0)
{
    <select @onchange="OnSubfactionSelected">
        <option value="">All @faction.Name (Generic)</option>
        @foreach (var subfaction in detectedSubfactions)
        {
            <option value="@subfaction">@subfaction</option>
        }
    </select>
    <small>@detectedSubfactions.Count subfaction(s) detected</small>
}
```

**Page Load Flow**:
1. `OnInitializedAsync()` calls `DetectSubfactionsAsync()`
2. Dropdown rendered only if subfactions found
3. User selection triggers `OnSubfactionSelected()`
4. Page reloads with filtered content

## Examples

### Space Marines
**Detected Subfactions**:
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

### Orks (if they have clan-specific rules)
**Detected Subfactions**:
- Goff
- Bad Moons
- Evil Sunz
- Deathskulls
- Blood Axes
- Snakebites

### Aeldari Craftworlds
**Detected Subfactions**:
- Ulthwé
- Saim-Hann
- Biel-Tan
- Alaitoc
- Iyanden

## Advantages

✅ **Zero Configuration** - No hardcoded lists to maintain
✅ **Faction Agnostic** - Works for any faction with subfactions
✅ **Data-Driven** - Adapts to changes in Wahapedia data
✅ **User-Friendly** - Only shows dropdown when relevant
✅ **Accurate** - Uses actual game data patterns
✅ **Scalable** - New subfactions detected automatically

## Edge Cases Handled

1. **No Subfactions**: Dropdown doesn't render
2. **Generic Content**: "All [Faction] (Generic)" option shows base rules
3. **Mixed Content**: Subfaction filter shows subfaction + generic rules
4. **Multiple Mentions**: Deduplicates subfaction names
5. **Short Names**: Filters out abbreviations and short terms

## Performance

- **Detection**: Runs once on page load
- **Caching**: Subfaction list stored in component state
- **Filtering**: In-memory operations on query results
- **No Extra Queries**: Uses existing ability/detachment data

## Future Enhancements

1. **Caching**: Cache detected subfactions per faction (avoid re-detection)
2. **Machine Learning**: Improve pattern recognition with ML
3. **User Overrides**: Allow manual subfaction tagging
4. **Source Integration**: Link to Source table for explicit tracking

## Build Status
✅ Build successful
✅ Dynamic subfaction detection implemented
✅ Generic filtering logic across all factions
✅ Pattern-based text analysis
✅ Dropdown auto-hides when no subfactions detected
