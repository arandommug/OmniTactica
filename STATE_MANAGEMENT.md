# State Management Implementation

## Overview
Implemented comprehensive state management for faction-related pages to preserve user selections and UI state during navigation.

## Components

### 1. FactionViewerState Service (`AppCode/Services/FactionViewerState.cs`)
Scoped service that maintains:
- **Current Faction Context**: Tracks which faction the user is viewing
- **Keyword Filters**: Selected keywords and AND/OR logic (shared across all pages)
- **Detachment Selection**: Currently selected detachment (shared across all pages)
- **UI State**: Per-page state like scroll positions, open accordions, etc.

### 2. Registration
Registered as a **Scoped service** in `MauiProgram.cs` so it persists during the user session but is separate per browser tab.

## Features

### ✅ Keyword Filter Persistence
- Keywords selected in **FactionViewer** automatically appear in **UnitsViewer**
- Keywords selected in **UnitsViewer** are preserved when navigating back to **FactionViewer**
- AND/OR logic setting is shared across pages
- Filters are maintained when drilling into individual units

### ✅ Detachment Selection Sharing
- Detachment selected in **FactionViewer** automatically filters content in **UnitViewer**
- Detachment selected in **UnitViewer** is reflected when navigating back
- Selection persists across unit navigation

### ✅ Page State Preservation
- **FactionViewer**: 
  - Remembers open detachment selection
  - Preserves keyword filters
- **UnitsViewer**: 
  - Remembers keyword filters
  - Maintains list state
- **UnitViewer**: 
  - Preserves detachment selection
  - Uses shared or query parameter detachment

### ✅ Automatic State Reset
- When switching to a different faction, all state automatically resets
- Prevents incorrect filter application across factions

## Implementation Pattern

### Page Initialization
```csharp
protected override async Task OnInitializedAsync()
{
    // Ensure state is for correct faction
    ViewerState.ResetForFaction(FactionId);
    
    // Load state from service
    selectedKeywords = new List<string>(ViewerState.SelectedKeywords);
    useAndLogic = ViewerState.UseAndLogic;
    
    // ... load data with filters
}
```

### Saving State
```csharp
private async Task ApplyKeywordFilter()
{
    // Update local state
    selectedKeywords = new List<string>(tempSelectedKeywords);
    useAndLogic = tempUseAndLogic;
    
    // Save to shared state
    ViewerState.SetKeywordFilter(selectedKeywords, useAndLogic);
    
    // Reload data
    await LoadDataAsync();
}
```

## User Experience Benefits

1. **Seamless Navigation**: Users can navigate between pages without losing their filter selections
2. **Consistent Filtering**: Keyword and detachment filters apply consistently across all views
3. **No Re-work**: Users don't need to reapply filters or reopen accordions when navigating back
4. **Intuitive Flow**: Selections made in one context (e.g., faction viewer) automatically apply in related contexts (e.g., unit viewer)

## Technical Benefits

1. **Scoped Lifetime**: State persists for the session but doesn't leak between tabs/users
2. **Clean Architecture**: State logic separated from UI logic
3. **Type-Safe**: Strongly-typed state properties
4. **Automatic Cleanup**: State automatically resets when switching factions
5. **Performance**: No unnecessary data reloading; filters are applied efficiently

## Future Enhancements

Possible additions:
- Scroll position preservation
- Accordion open/close state per page
- Recently viewed units/factions
- Filter history/favorites
- Persistent state across app restarts (using local storage)
