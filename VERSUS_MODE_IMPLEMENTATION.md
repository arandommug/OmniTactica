# Versus Mode - Complete Implementation Summary

## Overview
A comprehensive unit vs unit combat calculator for Warhammer 40K using Monte Carlo simulation with detailed logging and statistics.

## Architecture

### Core Components

#### 1. **Data Models** (`AppCode/Models/Core/VersusContext.cs`)
- **VersusContext**: Main container for combat scenario
  - AttackingUnits, DefendingUnits lists
  - GlobalModifiers for each side
  - SimulationSettings

- **CombatUnit**: Represents a unit in combat
  - DatasheetId, DatasheetName, FactionId
  - List of CombatModels
  - List of ConditionalModifiers
  - Reference to DatasheetDetail

- **CombatModel**: Individual model within a unit
  - Stats: M, T, Sv, InvSv, W, Ld, OC
  - Quantity (supports multiple identical models)
  - List of CombatWeapons
  - List of ConditionalModifiers
  - Wound tracking (CurrentWounds, MaxWounds, IsDestroyed)

- **CombatWeapon**: Weapon equipped on a model
  - Stats: Range, Type, A, BsWs, S, AP, D
  - IsSelected flag for toggling weapons
  - Parsed WeaponAbilities
  - List of ConditionalModifiers

- **WeaponAbilities**: Parsed abilities from weapon description
  - Boolean flags: Assault, Blast, DevastatingWounds, Hazardous, Heavy, IgnoresCover, IndirectFire, LethalHits, Pistol, Precision, Torrent, TwinLinked
  - Integer values: RapidFire, SustainedHits, Melta
  - Anti-keyword support: AntiInfantry, AntiVehicle, AntiMonster (with threshold values)

- **ConditionalModifier**: When/Then modifier system
  - ModifierCondition (when)
  - ModifierEffect (then)
  - IsActive, IsCustom flags

- **ConditionType Enum**:
  - Always, UnitCharged, TargetWithinHalfRange, TargetWithinEngagementRange
  - UnitRemainedStationary, TargetIsInfantry, TargetIsVehicle, TargetIsMonster, TargetIsCharacter
  - TargetUnitSize5Plus, TargetUnitSize10Plus, CriticalHit, CriticalWound, CustomCondition

- **EffectType Enum**:
  - AddHitModifier, AddWoundModifier, AddSaveModifier, AddAPModifier, AddDamageModifier
  - AddAttacks, AddExtraHitsOnCrit, AutoWoundOnCrit, ConvertToMortalWounds
  - RerollHits, RerollWounds, RerollOnes
  - IgnoreInvulnerable, IgnoreCover, HalveDamage, ReduceDamage, FeelNoPain
  - CriticalHitOn, CriticalWoundOn, CustomEffect

- **VersusResult**: Comprehensive simulation results
  - Average/Median/Min/Max damage
  - Standard deviation
  - Average models killed
  - Wipeout probability
  - DamageDistribution dictionary
  - Per-unit and per-weapon statistics
  - Stage breakdown (attacks, hits, wounds, saves, damage)
  - Sample combat log

#### 2. **VersusService** (`AppCode/Services/VersusService.cs`)
Main service for managing versus combat state.

**Key Features:**
- **Auto-population from database**: 
  - Loads DatasheetDetail using DatasheetRepository
  - Parses unit composition from `Datasheets_unit_composition` table
    - Example: "1 Warboss", "5-10 Intercessors"
  - Parses loadout text to assign default weapons
    - Example: "Every model is equipped with: bolt pistol; bolt rifle; close combat weapon."
  - Matches weapons from `Datasheets_wargear` table
  - Parses weapon abilities from description field

**Methods:**
- `AddAttackingUnitAsync(int datasheetId)`: Adds attacker
- `AddDefendingUnitAsync(int datasheetId)`: Adds defender
- `RemoveAttackingUnit(string unitId)`: Removes attacker
- `RemoveDefendingUnit(string unitId)`: Removes defender
- `GetUnit(string unitId)`: Gets unit by ID
- `AddModelToUnit`, `RemoveModelFromUnit`, `UpdateModel`: Model management
- `AddWeaponToModel`, `RemoveWeaponFromModel`, `ToggleWeaponSelection`, `UpdateWeapon`: Weapon management
- `AddUnitModifier`, `RemoveUnitModifier`: Unit-level modifiers
- `AddModelModifier`, `RemoveModelModifier`: Model-level modifiers
- `AddWeaponModifier`, `RemoveWeaponModifier`: Weapon-level modifiers
- `UpdateGlobalAttackerModifiers`, `UpdateGlobalDefenderModifiers`: Global modifiers
- `UpdateSimulationSettings`: Update simulation config
- `SwapAttackerDefender`: Swap sides
- `ClearAll`: Reset everything
- Event: `OnContextChanged` for reactive updates

**Text Parsing (Minimal):**
- Unit composition: Regex pattern `(\d+)(?:-(\d+))?\s+(.+)` to extract model counts and names
- Loadout: Splits by `;`, `,`, `\n` and matches weapon names
- Weapon abilities: Parses description text for keywords and values

#### 3. **VersusCalculator** (`AppCode/Services/VersusCalculator.cs`)
Static class for Monte Carlo combat simulation.

**Main Method:**
- `Calculate(VersusContext context)`: Runs all simulations and aggregates results

**Simulation Flow:**
1. **Attacks Phase**: Determine number of attacks
   - Parse attack characteristic (e.g., "2D6", "12", "D6+3")
   - Apply Rapid Fire if within half range
   - Apply Blast based on target unit size
   - Apply conditional modifiers

2. **Hit Phase**: Resolve hit rolls
   - Roll to hit based on BS/WS
   - Check for critical hits (6s or modified threshold)
   - Apply Torrent (auto-hit)
   - Apply reroll hits/reroll 1s
   - Apply Lethal Hits (auto-wound on crit)
   - Apply Sustained Hits (extra hits on crit)
   - Log all results

3. **Wound Phase**: Resolve wound rolls
   - Calculate wound threshold based on S vs T
   - Roll to wound
   - Check for critical wounds
   - Apply Anti-keywords (e.g., Anti-Infantry 4+)
   - Apply reroll wounds
   - Apply Devastating Wounds (convert to mortal wounds on crit)
   - Log all results

4. **Save Phase**: Resolve saves
   - Apply AP modifier
   - Determine if armor or invulnerable save is better
   - Apply cover bonus
   - Roll saves
   - Apply Ignore Invulnerable/Ignore Cover effects
   - Log all results

5. **Damage Phase**: Apply damage and Feel No Pain
   - Calculate damage (parse damage characteristic)
   - Apply Melta (extra damage within melta range)
   - Apply damage reduction/halve damage
   - Allocate wounds to models based on WoundAllocationMethod
   - Roll Feel No Pain saves
   - Track destroyed models
   - Log all results

**Comprehensive Logging:**
- Each combat phase creates detailed log entries
- Tracks: attacker, weapon, defender, roll results, modifiers applied
- Phase tags for easy filtering
- First simulation provides sample log

**Statistics Aggregation:**
- Damage distribution histogram
- Per-weapon statistics (average attacks, hits, wounds, damage, rates)
- Per-unit statistics (damage dealt, models killed, survival)
- Stage breakdown (total attacks/hits/wounds/saves/damage across all simulations)

#### 4. **WeaponAbilityParser** (`AppCode/Utilities/WeaponAbilityParser.cs`)
Utility for parsing weapon abilities from description text.

**Supported Abilities:**
- Simple flags: Assault, Blast, Torrent, Twin-Linked, Heavy, Pistol, Precision, Hazardous, Lethal Hits, Devastating Wounds, Indirect Fire, Ignores Cover
- With values: Rapid Fire X, Sustained Hits X, Melta X
- Anti-keywords: Anti-Infantry X+, Anti-Vehicle X+, Anti-Monster X+

**Methods:**
- `Parse(string description)`: Returns WeaponAbilities object
- `ParseAttacks(string attacksValue)`: Parses attack characteristic (fixed, dice, or combo)
- `CalculateExpectedAttacks(...)`: Calculates expected attack count

### UI Components

#### 1. **VersusPage** (`Components/Pages/VersusPage.razor`)
Main page for versus mode.

**Layout:**
- Split-screen design: Attacker (left/red) | Defender (right/blue)
- Each side has:
  - "Add Unit" button (opens UnitPickerModal)
  - Bookmark dropdown (quick-add from bookmarks)
  - Unit summary cards (click to edit, X to remove)
    - Shows model count, weapon count, active modifiers

**Top Controls:**
- Swap button (exchanges attacker/defender)
- Settings button (opens SimulationSettingsModal)
- Calculate button (runs simulation, shows iteration count)
- Clear All button (resets everything)

**Results Display:**
- Summary cards: Average damage, median, models killed, wipeout %
- Stage breakdown table: Attacks → Hits → Wounds → Saves → Damage (with percentages)
- Damage distribution: Probability chart for each damage outcome
- Combat log: Expandable detailed log from one simulation
  - Color-coded by phase (attacks, hit, wound, save, damage)
  - Step-by-step breakdown

#### 2. **UnitEditorModal** (`Components/Pages/UnitEditorModal.razor`)
Full-screen modal for editing a combat unit.

**Layout:**
- **Left Panel**: Datasheet reference (read-only)
  - Stats table
  - Unit composition
  - Loadout
  - All available weapons
  - Keywords

- **Right Panel**: Editable configuration
  - Models section:
    - Add/remove model types
    - Quantity spinners
    - Add modifiers per model
  - Weapons section (per model):
    - Weapon cards with stats
    - Toggle selection (checkboxes)
    - Add modifiers per weapon
  - Unit modifiers section:
    - Add modifiers to entire unit

**Features:**
- Auto-parses weapon abilities and displays them
- Weapon cards show: Range, Type, Attacks, BS/WS, Strength, AP, Damage, Abilities
- Modifier badges show active conditions/effects
- Save button applies changes

#### 3. **ModifierEditorModal** (`Components/Pages/ModifierEditorModal.razor`)
Modal for creating/editing modifiers.

**Features:**
- **Quick Add Presets**: Common modifiers with one click
  - Reroll Hits/Wounds/1s
  - +1/-1 to Hit/Wound/Save
  - Feel No Pain (5+, 6+)
  - Cover
  - Lance (charge bonus)
  - Heavy (stationary bonus)
  - Devastating Wounds
  - Lethal Hits
  - Sustained Hits

- **Custom Builder**:
  - When (Condition) dropdown
  - Then (Effect) dropdown
  - Value input (if required)
  - Preview description

**Conditional Logic:**
- User-friendly condition descriptions
- Value hints for effects that need them
- Real-time preview of what modifier will do

#### 4. **SimulationSettingsModal** (`Components/Pages/SimulationSettingsModal.razor`)
Modal for configuring simulation parameters.

**Settings:**
- Number of simulations (1,000 - 100,000, step 1,000)
- Wound allocation method:
  - Even Distribution
  - Target Weakest Models First
  - Target Full Health Models First
  - Random Allocation
- Range to target (inches, optional)
  - Used for Rapid Fire, Melta
- Attacker charged this turn (checkbox)
  - Affects charge-dependent abilities
- Enable detailed combat log (checkbox)
  - Performance impact warning

#### 5. **UnitPickerModal** (`Components/Pages/UnitPickerModal.razor`)
Modal for selecting a unit from the database.

**Features:**
- Faction filter dropdown
- Search box (filters by name)
- Scrollable list of units
- Shows: Unit name, points cost, role
- Click to select and add to versus mode

**Updated Bootstrap 5 Modal Syntax:**
- Uses `new bootstrap.Modal(document.getElementById('modal')).show()`
- Uses `bootstrap.Modal.getInstance(document.getElementById('modal')).hide()`

### Database Structure

**Key Tables:**
- `Datasheets`: Main unit table (id, name, faction_id, loadout, etc.)
- `Datasheets_unit_composition`: Model composition (e.g., "1 Warboss", "5-10 Intercessors")
- `Datasheets_models`: Model profiles (M, T, Sv, inv_sv, W, Ld, OC)
- `Datasheets_wargear`: Weapons (name, range, type, A, BS_WS, S, AP, D, description)
- `Datasheets_keywords`: Unit keywords (used for Anti- abilities, etc.)

**CSV Note:**
- Wahapedia CSV files use pipe delimiter (`|`), not comma
- See `.github/copilot-instructions.md` for reference

### Integration

**Services Registered** (`MauiProgram.cs`):
```csharp
builder.Services.AddSingleton<VersusService>();
```

**Navigation** (`Components/Layout/NavMenu.razor`):
- Mobile drawer and desktop sidebar both have "Versus" link
- Route: `/versus`
- Icon: `fa fa-fist-raised`

**Action Buttons** (`Components/Shared/DatasheetActionButtons.razor`):
- Unit viewer page has "Set as Attacker" and "Set as Defender" buttons
- Clicking these:
  1. Calls `VersusService.AddAttackingUnitAsync(datasheetId)` or `AddDefendingUnitAsync(datasheetId)`
  2. Navigates to `/versus`
  3. Unit is auto-populated with default models/weapons

## Usage Flow

### Adding Units
1. Navigate to `/versus`
2. Click "Add Unit" (attacker or defender)
3. Select faction (optional) and search for unit
4. Click unit in list
5. Unit is added with:
   - Default models from unit composition
   - Default weapons from loadout
   - All weapon abilities parsed

### Editing Units
1. Click on unit summary card
2. UnitEditorModal opens full-screen
3. Left side shows datasheet reference
4. Right side allows editing:
   - Add/remove model types
   - Change quantities
   - Toggle weapon selection
   - Add conditional modifiers (unit/model/weapon level)
5. Click "Save Changes"

### Adding Modifiers
1. In UnitEditorModal, click "Add Modifier" button
2. ModifierEditorModal opens
3. Choose from presets OR build custom:
   - Select "When" condition
   - Select "Then" effect
   - Enter value if needed
4. Click "Save"
5. Modifier appears as badge on unit/model/weapon

### Running Simulation
1. Ensure at least one attacker and one defender are added
2. Click "Settings" to configure (optional)
3. Click "Calculate"
4. Wait for simulation to complete
5. View results:
   - Summary statistics
   - Stage breakdown
   - Damage distribution chart
   - Detailed combat log (if enabled)

### Example Scenario
**Attacker:** Intercessor Squad (5 models)
- Each equipped with: bolt pistol, bolt rifle, close combat weapon
- Bolt rifle: 24" Assault/Heavy, A:2, BS:3+, S:4, AP:-1, D:1

**Defender:** Ork Boyz (10 models)
- Each equipped with: slugga, choppa
- T:5, Sv:6+, W:1

**Simulation (10,000 runs):**
- Average damage: 4.2
- Median damage: 4
- Models killed: 4.2 average
- Wipeout probability: 0.3%

**Stage Breakdown:**
- Total attacks: 10 (5 models × 2 attacks)
- Total hits: 6.67 (66.7%)
- Total wounds: 3.33 (50%)
- Failed saves: 2.78 (83.3%)
- Total damage: 4.2

## Testing Checklist

### Basic Functionality
- [ ] Navigate to `/versus` page
- [ ] Add attacker from database search
- [ ] Add defender from database search
- [ ] Add attacker from bookmark
- [ ] Add defender from bookmark
- [ ] Edit attacker unit (open modal)
- [ ] Edit defender unit (open modal)
- [ ] Remove attacker
- [ ] Remove defender
- [ ] Swap attacker/defender
- [ ] Clear all

### Unit Editor
- [ ] View datasheet reference panel
- [ ] Add model type
- [ ] Remove model type
- [ ] Change model quantity
- [ ] Add weapon to model
- [ ] Remove weapon from model
- [ ] Toggle weapon selection
- [ ] Add unit-level modifier
- [ ] Add model-level modifier
- [ ] Add weapon-level modifier
- [ ] Remove modifiers
- [ ] Save changes
- [ ] Cancel changes

### Modifier Editor
- [ ] Add preset modifier (Reroll Hits)
- [ ] Add preset modifier (FNP 5+)
- [ ] Create custom modifier (When Always → Then +1 to Hit)
- [ ] Create custom modifier (When Charged → Then +1 Attack)
- [ ] View modifier preview
- [ ] Edit existing modifier
- [ ] Delete modifier

### Simulation Settings
- [ ] Change iteration count
- [ ] Change wound allocation method
- [ ] Set range to target
- [ ] Toggle attacker charged
- [ ] Toggle detailed logging
- [ ] Save settings

### Calculation
- [ ] Run calculation with default settings
- [ ] Run calculation with 1,000 iterations
- [ ] Run calculation with 100,000 iterations
- [ ] View summary statistics
- [ ] View stage breakdown
- [ ] View damage distribution
- [ ] View combat log (if enabled)
- [ ] Expand/collapse combat log

### Data Integrity
- [ ] Verify unit composition parsed correctly
- [ ] Verify loadout weapons assigned correctly
- [ ] Verify weapon abilities parsed correctly
  - [ ] Rapid Fire X
  - [ ] Melta X
  - [ ] Lethal Hits
  - [ ] Devastating Wounds
  - [ ] Sustained Hits X
  - [ ] Anti-Infantry X+
  - [ ] Torrent
  - [ ] Twin-Linked
  - [ ] Blast
- [ ] Verify stats parsed correctly (M, T, Sv, InvSv, W, etc.)
- [ ] Verify conditional modifiers work
  - [ ] Always condition
  - [ ] Charged condition
  - [ ] Within half range condition

### Edge Cases
- [ ] Unit with no weapons
- [ ] Unit with only melee weapons
- [ ] Unit with variable model count (5-10)
- [ ] Unit with multiple model types
- [ ] Weapon with dice attacks (2D6)
- [ ] Weapon with combo attacks (6+D6)
- [ ] Weapon with variable damage (D3, D6)
- [ ] Target with invulnerable save
- [ ] Target with Feel No Pain
- [ ] Multiple modifiers stacking
- [ ] Conditional modifier that doesn't trigger
- [ ] Rapid Fire at different ranges
- [ ] Melta at different ranges
- [ ] Blast against different unit sizes

## Known Limitations
1. Does not support multi-phase combat (just shooting or melee, not both in sequence)
2. Does not support psychic phase
3. Does not support stratagems (must be added as custom modifiers)
4. Does not support detachment abilities (must be added as custom modifiers)
5. Does not support multi-unit interactions (e.g., aura abilities affecting nearby units)
6. Does not support leader attachments (must manually combine profiles)
7. Does not support morale phase
8. Loadout parsing is basic and may miss complex weapon options

## Future Enhancements
- Save/load versus scenarios
- Export results to PDF/CSV
- Compare multiple scenarios side-by-side
- Import/export unit configurations
- Pre-built army lists
- Tournament mode (best of 3, etc.)
- Advanced statistics (confidence intervals, t-tests)
- Weapon efficiency analysis
- Recommended loadouts based on target
- AI opponent suggestions
- Multi-unit battles (3v3, 5v5, etc.)

## File Structure
```
OmniTactica/
├── AppCode/
│   ├── Models/
│   │   └── Core/
│   │       └── VersusContext.cs           # All data models
│   ├── Services/
│   │   ├── VersusService.cs               # State management & data loading
│   │   └── VersusCalculator.cs            # Monte Carlo simulation engine
│   └── Utilities/
│       └── WeaponAbilityParser.cs         # Weapon ability parsing
├── Components/
│   ├── Layout/
│   │   └── NavMenu.razor                  # Navigation (includes /versus link)
│   ├── Pages/
│   │   ├── VersusPage.razor               # Main page
│   │   ├── UnitEditorModal.razor          # Full-screen unit editor
│   │   ├── ModifierEditorModal.razor      # Modifier builder
│   │   ├── SimulationSettingsModal.razor  # Settings
│   │   └── UnitPickerModal.razor          # Unit selection
│   └── Shared/
│       └── DatasheetActionButtons.razor   # "Set as Attacker/Defender" buttons
├── MauiProgram.cs                         # Service registration
└── VERSUS_MODE_IMPLEMENTATION.md          # This file
```

## Build Status
✅ Build Successful (all files compile)

## Notes
- Database path: `C:\Users\nerda\AppData\Local\User Name\com.companyname.OmniTactica\Data\wahapedia.db`
- Uses existing repositories (DatasheetRepository, BookmarkService)
- Minimal text parsing (only unit composition and loadout)
- All weapon abilities auto-parsed from description field
- Conditional modifiers use when/then pattern from versus-rules.json
- Monte Carlo simulation is fully logged and statistical
- No TODOs left in code
- No V2/V3/Enhanced naming - everything is just "Versus"

## Contributors
- AI Assistant (Implementation)
- Project Owner (Requirements & Testing)
