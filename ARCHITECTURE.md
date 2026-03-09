# Architecture Refactoring Summary

## Overview
Successfully refactored the codebase to follow a clean, layered architecture that separates:
- Database schema (Tables)
- Domain/game models (Models)
- Data access logic (Repositories)
- Business logic (Services)
- Utilities (Mapping/helpers)

## New Directory Structure

```
AppCode/
│
├── Data/                           # Database layer
│   ├── Tables/                     # Pure DB schema (flat, no navigation)
│   │   ├── TableBase.cs           # Base query helper class
│   │   ├── FactionTable.cs
│   │   ├── AbilityTable.cs
│   │   └── DetachmentTable.cs
│   │
│   ├── Database/                   # Database management
│   │   └── WahaSQLiteService.cs   # Connection factory
│   │
│   └── Import/                     # Data import/patching
│       ├── WahaDataImporter.cs
│       └── WahaDataPatcher.cs
│
├── Models/                         # Domain models (game concepts)
│   ├── Core/                       # Core game entities
│   │   ├── Faction.cs             # With Detachments, Abilities lists
│   │   └── Detachment.cs          # With Abilities list
│   │
│   ├── Rules/                      # Game rules
│   │   └── Ability.cs
│   │
│   └── Datasheets/                 # Unit data (future)
│
├── Repositories/                   # Query layer (Tables → Models)
│   ├── FactionRepository.cs       # Queries Factions + Abilities
│   └── DetachmentRepository.cs    # Queries Detachments + Abilities
│
├── Services/                       # Business logic layer
│   └── WahaDataService.cs         # High-level API for UI
│
└── Utilities/                      # Helpers
    └── SqlMapper.cs
```

## Layer Responsibilities

### 1. Data/Tables (Database Schema)
**Purpose**: Match SQLite schema exactly

**Characteristics**:
- Simple record types
- No navigation properties
- No lists
- Just columns

**Example**:
```csharp
public record FactionTable(
    string Id,
    string Name
);
```

### 2. Models (Domain/Game Objects)
**Purpose**: Represent Warhammer 40K game concepts

**Characteristics**:
- Rich objects with relationships
- Navigation properties (Lists)
- Matches how the game works, not how DB stores it

**Example**:
```csharp
public class Faction
{
    public string Id { get; set; }
    public string Name { get; set; }
    public List<Detachment> Detachments { get; set; } = new();
    public List<Ability> Abilities { get; set; } = new();
}
```

### 3. Repositories (Query Layer)
**Purpose**: Translate database tables into domain models

**Characteristics**:
- Inherit from TableBase for query helpers
- Load data from one or more tables
- Map table records → domain models
- Handles N+1 query optimization

**Example**:
```csharp
public class FactionRepository : TableBase
{
    public async Task<Faction?> GetFactionWithAbilitiesAsync(string factionId)
    {
        // Query FactionTable
        // Query AbilityTable
        // Map to Faction domain model
        // Return complete object
    }
}
```

### 4. Services (Business Logic)
**Purpose**: Provide high-level API for UI components

**Characteristics**:
- Coordinates multiple repositories
- Returns view-specific DTOs
- Implements caching (future)
- This is what Blazor pages inject and use

**Example**:
```csharp
public class WahaDataService
{
    public Task<List<Faction>> GetAllFactionsAsync();
    public Task<FactionOverview?> GetFactionOverviewAsync(string factionId);
    public Task<Detachment?> GetDetachmentDetailsAsync(int detachmentId);
}
```

### 5. WahaSQLiteService (Database Engine)
**Purpose**: Manage SQLite connections only

**Does NOT contain**:
- Game-specific queries
- Navigation properties
- Table-specific methods

**Only provides**:
- CreateConnection() - Read-only connection
- CreateWriteConnection() - Write connection
- DatabaseExists property

## Dependency Flow

```
Blazor Pages
    ↓ (inject)
WahaDataService
    ↓ (uses)
Repositories (FactionRepository, DetachmentRepository, etc.)
    ↓ (uses)
TableBase (query helpers)
    ↓ (uses)
WahaSQLiteService (connection factory)
    ↓ (creates)
SqliteConnection
```

## Key Benefits

### 1. Separation of Concerns
- Database logic ≠ Domain logic ≠ UI logic
- Each class has one clear responsibility
- Easy to test each layer independently

### 2. Scalability
- Adding new tables: Create Table record + Repository method
- Adding new queries: Add method to Repository
- Adding new UI views: Add method to Service
- No massive god classes

### 3. Performance
- Repositories can optimize queries (batch loading)
- Avoid N+1 query problems
- Easy to add caching at Service layer

### 4. Maintainability
- Clear naming conventions
- Predictable file locations
- Self-documenting structure

## Migration Guide

### Old Pattern (Before)
```csharp
@inject WahaSQLiteService DB

// Browse.razor
factions = await DB.Factions.GetAllAsync();

// FactionViewer.razor
abilities = await DB.Abilities.GetFactionAbilitiesAsync(FactionId);
faction = await DB.Factions.GetByIdAsync(FactionId);
```

### New Pattern (After)
```csharp
@inject WahaDataService Data

// Browse.razor
factions = await Data.GetAllFactionsAsync();

// FactionViewer.razor
var overview = await Data.GetFactionOverviewAsync(FactionId);
faction = overview.Faction;
detachments = overview.Detachments;
```

## Dependency Injection Setup

In `MauiProgram.cs`:

```csharp
// Database (singleton - one connection factory)
builder.Services.AddSingleton<WahaSQLiteService>();

// Repositories (singleton - stateless query objects)
builder.Services.AddSingleton<FactionRepository>();
builder.Services.AddSingleton<DetachmentRepository>();

// Services (singleton - high-level API)
builder.Services.AddSingleton<WahaDataService>();
```

## Next Steps

### Immediate
1. Add more table models as you expand (Datasheet, Stratagem, Enhancement, etc.)
2. Add corresponding repositories
3. Expand WahaDataService with new methods

### Future Enhancements
1. **Caching**: Add memory cache to WahaDataService for frequently accessed data
2. **DTOs**: Create view-specific DTOs (e.g., `FactionOverview`, `DatasheetDetails`)
3. **Mapping Layer**: Extract mapping logic to dedicated mappers if it gets complex
4. **Query Optimization**: Implement batch loading in repositories to eliminate N+1 queries

## Files Changed

### Created (New Architecture)
- `AppCode/Data/Tables/FactionTable.cs`
- `AppCode/Data/Tables/AbilityTable.cs`
- `AppCode/Data/Tables/DetachmentTable.cs`
- `AppCode/Models/Core/Faction.cs`
- `AppCode/Models/Core/Detachment.cs`
- `AppCode/Models/Rules/Ability.cs`
- `AppCode/Repositories/FactionRepository.cs`
- `AppCode/Repositories/DetachmentRepository.cs`
- `AppCode/Services/WahaDataService.cs`

### Moved & Updated
- `WahaSQLiteService.cs` → `AppCode/Data/Database/`
- `WahaDataImporter.cs` → `AppCode/Data/Import/`
- `WahaDataPatcher.cs` → `AppCode/Data/Import/`
- `TableBase.cs` → `AppCode/Data/Tables/`

### Updated
- `MauiProgram.cs` - DI registration
- `Browse.razor` - Use WahaDataService
- `FactionViewer.razor` - Use WahaDataService, improved UI
- `Settings.razor` - Updated namespaces

### Deleted
- `AppCode/WahaData/` (entire old structure)

## Build Status
✅ Build successful
✅ All namespaces updated
✅ Dependency injection configured
✅ Blazor pages refactored
✅ Clean separation of concerns achieved
