# Stratagems & Enhancements Implementation

## Overview
Added complete support for Stratagems and Enhancements, including table models, domain models, repository queries, and an enhanced FactionViewer page with interactive detachment selection.

## New Files Created

### Data/Tables (Database Schema)

#### 1. StratagemTable.cs
```csharp
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
```

#### 2. EnhancementTable.cs
```csharp
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
```

### Models/Rules (Domain Models)

#### 3. Stratagem.cs
```csharp
public class Stratagem
{
    public int Id { get; set; }
    public string FactionId { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public string CpCost { get; set; }
    public string Legend { get; set; }
    public string Turn { get; set; }
    public string Phase { get; set; }
    public string Detachment { get; set; }
    public int DetachmentId { get; set; }
    public string Description { get; set; }
}
```

#### 4. Enhancement.cs
```csharp
public class Enhancement
{
    public int Id { get; set; }
    public string FactionId { get; set; }
    public string Name { get; set; }
    public int Cost { get; set; }
    public string Detachment { get; set; }
    public int DetachmentId { get; set; }
    public string Legend { get; set; }
    public string Description { get; set; }
}
```

## Updated Files

### Detachment Model
Added the stratagem and enhancement lists:
```csharp
public class Detachment
{
    public int Id { get; set; }
    public string FactionId { get; set; }
    public string Name { get; set; }
    public string Legend { get; set; }
    public string Type { get; set; }

    public List<DetachmentAbility> Abilities { get; set; } = new();
    public List<Stratagem> Stratagems { get; set; } = new();      // ✅ Now populated
    public List<Enhancement> Enhancements { get; set; } = new();  // ✅ Now populated
}
```

### DetachmentRepository
Renamed method and added queries for stratagems and enhancements:

**Old**: `GetDetachmentWithAbilitiesAsync()`  
**New**: `GetDetachmentWithDetailsAsync()` - loads abilities, stratagems, AND enhancements

Added three new queries:
1. **Stratagems query** - Loads all stratagems for the detachment
2. **Enhancements query** - Loads all enhancements for the detachment
3. **Mapper functions** - `MapStratagemFromTable()` and `MapEnhancementFromTable()`

### WahaDataService
Updated method call:
```csharp
public Task<Detachment?> GetDetachmentDetailsAsync(int detachmentId)
    => _detachments.GetDetachmentWithDetailsAsync(detachmentId);
```

## Enhanced FactionViewer Page

### New Features

#### 1. Dropdown Detachment Selector
Replaced accordion with a Bootstrap dropdown:
```html
<select id="detachmentSelect" class="form-select" @onchange="OnDetachmentSelected">
    <option value="">-- Choose Detachment --</option>
    @foreach (var d in detachments)
    {
        <option value="@d.Id">@d.Name</option>
    }
</select>
```

#### 2. Detachment Details Card
Shows detachment name, description, legend, and type in a card layout.

#### 3. Detachment Abilities Section
Accordion display of all detachment-specific abilities with legends.

#### 4. Stratagems Section
Accordion with:
- Stratagem name
- CP cost badge (Info badge)
- Type, Phase, Turn badges
- Legend (if available)
- Description (HTML cleaned)

#### 5. Enhancements Section
Accordion with:
- Enhancement name
- Points cost badge (Warning badge)
- Legend (if available)
- Description (HTML cleaned)

### UI/UX Improvements

**Loading States**:
- Initial faction load spinner
- Separate detachment details load spinner
- Proper state management with `loadingDetachment` flag

**Visual Hierarchy**:
- Army abilities at top
- Detachment selector below
- Selected detachment details in expandable sections
- Color-coded badges (Info for CP, Warning for points)

**Responsive Design**:
- Bootstrap form controls
- Accordion components for content organization
- Badge system for quick reference

### Code Structure

```csharp
@code {
    [Parameter]
    public string FactionId { get; set; } = string.Empty;

    private Faction? faction;
    private List<Detachment> detachments = new();
    private Detachment? selectedDetachment;      // ✅ New
    private bool loading = true;
    private bool loadingDetachment = false;      // ✅ New

    private async Task OnDetachmentSelected(ChangeEventArgs e)
    {
        // Parse selected ID
        // Load full detachment details
        // Update UI
    }
}
```

## User Flow

1. **Page Load**
   - Display faction name and army abilities
   - Show detachment dropdown (basic info only)

2. **User Selects Detachment**
   - Trigger `OnDetachmentSelected`
   - Show loading spinner
   - Load full detachment details (abilities, stratagems, enhancements)
   - Display all content in organized sections

3. **User Explores Content**
   - Click accordion items to expand/collapse
   - View stratagem CP costs at a glance
   - See enhancement point costs
   - Read full descriptions with HTML formatting

## Performance Benefits

**Lazy Loading**: Detachment details (abilities, stratagems, enhancements) only loaded when user selects a detachment, not all at once on page load.

**Single Query**: `GetDetachmentWithDetailsAsync()` loads all related data in one repository method, minimizing round trips.

## Visual Examples

### Stratagem Display
```
┌─────────────────────────────────────────┐
│ ▶ Fire Overwatch               [2 CP]   │
├─────────────────────────────────────────┤
│ [Battle Tactic] [Any Phase] [Yours]    │
│ When: An enemy unit declares a charge   │
│ Effect: This unit can shoot...          │
└─────────────────────────────────────────┘
```

### Enhancement Display
```
┌─────────────────────────────────────────┐
│ ▶ Warlord Trait: Inspire       [15 pts] │
├─────────────────────────────────────────┤
│ The bearer gains an aura ability...     │
└─────────────────────────────────────────┘
```

## Build Status
✅ Build successful  
✅ All models created  
✅ Repository queries implemented  
✅ FactionViewer enhanced with interactive UI  
✅ Lazy loading of detachment details  
✅ Clean HTML rendering with WahaDataImporter.CleanHtml()
