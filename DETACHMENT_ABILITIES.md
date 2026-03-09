# Detachment Abilities Implementation

## Overview
Corrected the implementation to properly handle `Detachment_abilities` as its own separate table, not as a junction table to `Abilities`.

## Key Distinction

### Faction Abilities (Abilities table)
- **Table**: `Abilities`
- **Columns**: id, name, legend, faction_id, description
- **Purpose**: General faction-wide abilities
- **Model**: `Ability` in `Models/Rules/`

### Detachment Abilities (Detachment_abilities table)
- **Table**: `Detachment_abilities`
- **Columns**: id, faction_id, name, legend, description, detachment, detachment_id
- **Purpose**: Detachment-specific abilities (tied to specific detachments)
- **Model**: `DetachmentAbility` in `Models/Rules/`

## Files Created

### 1. AppCode/Data/Tables/DetachmentAbilityTable.cs
```csharp
public record DetachmentAbilityTable(
    int Id,
    string FactionId,
    string Name,
    string Legend,
    string Description,
    string Detachment,
    int DetachmentId
);
```

Flat record matching the `Detachment_abilities` table schema.

### 2. AppCode/Models/Rules/DetachmentAbility.cs
```csharp
public class DetachmentAbility
{
    public int Id { get; set; }
    public string FactionId { get; set; }
    public string Name { get; set; }
    public string Legend { get; set; }
    public string Description { get; set; }
    public string Detachment { get; set; }
    public int DetachmentId { get; set; }
}
```

Domain model representing a detachment-specific ability.

## Updated Files

### AppCode/Models/Core/Detachment.cs
Changed from:
```csharp
public List<Ability> Abilities { get; set; } = new();
```

To:
```csharp
public List<DetachmentAbility> Abilities { get; set; } = new();
```

Now correctly uses `DetachmentAbility` instead of generic `Ability`.

### AppCode/Repositories/DetachmentRepository.cs

#### Old Query (Incorrect - used join)
```csharp
SELECT a.id, a.name, a.legend, a.faction_id, a.description 
FROM Detachment_abilities da
JOIN Abilities a ON da.id = a.id AND da.faction_id = a.faction_id
WHERE da.detachment_id = @detachmentId
```

#### New Query (Correct - direct query)
```csharp
SELECT id, faction_id, name, legend, description, detachment, detachment_id
FROM Detachment_abilities
WHERE detachment_id = @detachmentId
ORDER BY name
```

The `Detachment_abilities` table contains all the data we need directly - no join required!

#### Updated Mapper
```csharp
private static DetachmentAbility MapDetachmentAbilityFromTable(DetachmentAbilityTable table) => new()
{
    Id = table.Id,
    FactionId = table.FactionId,
    Name = table.Name,
    Legend = table.Legend,
    Description = table.Description,
    Detachment = table.Detachment,
    DetachmentId = table.DetachmentId
};
```

## Architecture Pattern

This follows the established pattern:

```
Detachment_abilities (SQLite Table)
        ↓
DetachmentAbilityTable (Data/Tables - flat record)
        ↓
DetachmentAbility (Models/Rules - domain model)
        ↓
DetachmentRepository (Repositories - query & map)
        ↓
WahaDataService (Services - high-level API)
        ↓
Blazor Pages (UI)
```

## Usage

```csharp
// Get detachment with abilities
var detachment = await detachmentRepository.GetDetachmentWithAbilitiesAsync(detachmentId);

// Access detachment-specific abilities
foreach (var ability in detachment.Abilities)
{
    Console.WriteLine($"{ability.Name}: {ability.Description}");
    Console.WriteLine($"Detachment: {ability.Detachment}");
}
```

## Build Status
✅ Build successful
✅ Proper separation between faction abilities and detachment abilities
✅ Correct query without unnecessary join
✅ Clean domain model
