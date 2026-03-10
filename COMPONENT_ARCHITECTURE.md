# Keyword Filter Modal - Component Architecture

## Overview
The `KeywordFilterModal` component is a fully self-contained, reusable component for keyword filtering across the OmniTactica app.

## Design Principles

### 1. **Self-Contained State Management**
The modal manages ALL its own internal state:
- `tempSelectedKeywords` - Working copy of selections
- `tempUseAndLogic` - Working copy of AND/OR logic
- `searchText` - Search filter text
- `commonKeywords` / `uniqueKeywords` - Loaded keyword lists
- `showUniqueKeywords` - Collapse state
- `loading` - Loading indicator state

### 2. **One-Way Data Flow**
- **Input**: Receives initial values when opened (`InitialSelectedKeywords`, `InitialUseAndLogic`)
- **Output**: Only communicates back when "Apply" is clicked via `OnApplyFilter` callback
- **No Two-Way Binding**: Parent values are not updated during user interaction in the modal

### 3. **Minimal Parent Contract**
Parent pages only need:
```razor
<KeywordFilterModal 
    FactionId="@FactionId"
    ShowModal="@showKeywordModal"
    ShowModalChanged="@OnModalClosed"
    InitialSelectedKeywords="@selectedKeywords"
    InitialUseAndLogic="@useAndLogic"
    OnApplyFilter="@HandleKeywordFilterApplied" />
```

## Component API

### Parameters

| Parameter | Type | Purpose |
|-----------|------|---------|
| `FactionId` | `string` | Faction to load keywords for |
| `ShowModal` | `bool` | Controls modal visibility |
| `ShowModalChanged` | `EventCallback<bool>` | Notifies when modal is closed |
| `InitialSelectedKeywords` | `List<string>` | Keywords to pre-select when opened |
| `InitialUseAndLogic` | `bool` | Initial AND/OR logic state |
| `OnApplyFilter` | `EventCallback<(List<string>, bool)>` | Called when Apply is clicked with final values |

### Callback Signature
```csharp
private async Task HandleKeywordFilterApplied((List<string> Keywords, bool UseAndLogic) filterValues)
{
    selectedKeywords = filterValues.Keywords;
    useAndLogic = filterValues.UseAndLogic;
    // ... apply the filter
}
```

## Parent Page Requirements

### Minimal Code Per Page

**Fields (3 lines):**
```csharp
private List<string> selectedKeywords = new();
private bool useAndLogic = false;
private bool showKeywordModal = false;
```

**Methods (2 simple methods):**
```csharp
private void OpenKeywordModal()
{
    showKeywordModal = true;
}

private Task OnModalClosed(bool value)
{
    showKeywordModal = value;
    return Task.CompletedTask;
}

private async Task HandleKeywordFilterApplied((List<string> Keywords, bool UseAndLogic) filterValues)
{
    selectedKeywords = filterValues.Keywords;
    useAndLogic = filterValues.UseAndLogic;
    // Save to state and reload data
}
```

## Shared Helper: KeywordFilterHelper

For keyword matching logic used across pages:

```csharp
// Check if text matches filter
KeywordFilterHelper.MatchesKeywordFilter(text, selectedKeywords, useAndLogic)

// Get which keywords matched
KeywordFilterHelper.GetMatchedKeywords(text, selectedKeywords)
```

### Benefits
- ✅ **No code duplication** between pages
- ✅ **Single source of truth** for filtering logic
- ✅ **Testable** in isolation
- ✅ **Consistent behavior** across the app

## Files Structure

```
Components/
  Shared/
    KeywordFilterModal.razor        # Self-contained modal component
  Pages/
    FactionViewer.razor             # Uses modal + helper
    UnitsViewer.razor               # Uses modal + helper
    UnitViewer.razor                # Uses helper only
AppCode/
  Helpers/
    KeywordFilterHelper.cs          # Shared keyword matching logic
```

## Code Reduction Summary

### Before Refactoring
- **FactionViewer**: ~600 lines (including 200+ lines of modal markup + duplicate methods)
- **UnitsViewer**: ~400 lines (including 200+ lines of modal markup + duplicate methods)
- **Total**: ~1000 lines with significant duplication

### After Refactoring
- **KeywordFilterModal**: ~230 lines (single source)
- **KeywordFilterHelper**: ~30 lines (shared logic)
- **FactionViewer**: ~400 lines (modal removed, uses helper)
- **UnitsViewer**: ~200 lines (modal removed, uses helper)
- **Total**: ~860 lines, **zero duplication**

**Net Result**: ~140 lines removed + eliminated all duplicate code

## Key Improvements

1. **DRY Principle**: Modal code exists once, used everywhere
2. **Maintainability**: Changes to modal UI/behavior in one place
3. **Testability**: Component can be unit tested independently
4. **Simplicity**: Parent pages have minimal boilerplate
5. **Reusability**: Easy to add to new pages (just 3 fields + 3 methods)
6. **Clarity**: Clear separation between modal state and parent state
