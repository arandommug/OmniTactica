# Enhanced Versus Mode - Feature Documentation

## Overview
The Enhanced Versus Mode (Versus V2) provides a comprehensive combat simulation system that supports:
- Multiple attacking and defending units
- Individual model and weapon selection
- Granular modifier system at multiple levels (global, unit, model, weapon)
- Automatic ability parsing and application
- Configurable firing order and wound allocation
- Detailed per-weapon and per-unit statistics

## Key Features

### 1. Multiple Units Support
- Add multiple attacking units and multiple defending units
- Each unit maintains its own roster of models and weapons
- Units can be expanded/collapsed for easier management

### 2. Model Management
- Add individual models from a unit's datasheet
- Each model tracks its wounds (current/max)
- Models can have unique modifiers applied
- Support for mixed-model units (e.g., squad leaders with different profiles)

### 3. Weapon Selection
- Assign weapons to individual models
- Toggle weapons on/off to simulate different loadouts
- Each weapon can have its own modifiers
- Support for multiple weapons per model

### 4. Four-Tier Modifier System

#### Global Modifiers (Apply to all units on one side)
- Hit/Wound/Save modifiers
- Cover status
- Ignore cover capability

#### Unit Modifiers (Apply to all models in a unit)
- Hit/Wound modifiers
- Re-roll capabilities (all hits, 1s, wounds)
- Save modifiers (armor and invulnerable)
- Toughness modifier
- Feel No Pain
- Damage reduction/halving
- Cover status

#### Model Modifiers (Apply to individual models)
- Hit/Wound modifiers (for attacking)
- Save modifiers
- Toughness modifier
- Feel No Pain
- Damage reduction/halving

#### Weapon Modifiers (Apply to individual weapons)
- Hit/Wound modifiers
- Re-roll capabilities
- Critical hit/wound thresholds
- Sustained Hits
- Lethal Hits
- Devastating Wounds
- AP and Damage modifiers
- Extra attacks
- Ignore invulnerable saves
- Ignore cover
- Twin-Linked
- Torrent (auto-hit)

### 5. Automatic Ability Application
The system automatically applies modifiers for common weapon abilities:
- **Twin-Linked**: Automatically enables wound re-rolls
- **Torrent**: Automatically enables auto-hit
- **Ignores Cover**: Automatically set on weapons with this ability
- More abilities can be easily added via the WeaponAbilityParser

### 6. Firing Order Configuration

#### By Unit
Fire all weapons from one unit before moving to the next
- Useful for sequential unit activations
- Respects unit firing priority

#### By Priority
Fire weapons in order of their assigned priority
- Allows fine-grained control over activation order
- Can simulate "best weapon first" strategies

#### Simultaneous
All weapons fire at once (default Monte Carlo simulation)
- Most statistically accurate
- Reflects the abstract nature of the turn sequence

### 7. Wound Allocation Methods

#### Even Distribution
Wounds spread evenly across all models
- Most balanced approach
- Reflects careful wound allocation

#### Target Weakest
Wounds allocated to already-damaged models first
- Simulates finishing off wounded models
- Can affect probabilities if models have different wounds remaining

#### Target Strongest
Wounds allocated to full-health models first
- Simulates spreading damage
- Useful for units where killing specific models is harder

### 8. Comprehensive Results

The enhanced mode provides:
- **Average Total Damage**: Mean damage dealt across all simulations
- **Average Models Killed**: Expected number of models destroyed
- **Median Damage**: Middle value of damage distribution (50th percentile)
- **Wipeout Probability**: Chance of destroying all defending units
- **Per-Weapon Statistics**: Breakdown of hits, wounds, and damage per weapon
- **Per-Unit Statistics**: Damage dealt by each attacking unit
- **Damage Distribution**: Full probability distribution of outcomes

## Usage Guide

### Basic Workflow

1. **Add Units**
   - Click "Add Unit" under Attackers or Defenders
   - Select the datasheet from the unit selector
   - Click "Load Weapons" or "Load Models" to fetch datasheet details

2. **Add Models**
   - Expand a unit
   - Click on a model profile to add one model of that type
   - Repeat to add multiple models
   - Each model starts at full wounds

3. **Add Weapons** (for attackers)
   - Expand a model
   - Click on weapon profiles to equip that model
   - Green checkmark indicates selected weapons that will fire
   - Click checkmark button to toggle weapon selection

4. **Apply Modifiers**
   - **Global**: Use "Global Attacker/Defender Modifiers" buttons
   - **Unit**: Click "Unit Modifiers" button on expanded unit
   - **Model**: Click magic wand icon next to a model (defenders)
   - **Weapon**: Click edit icon next to a weapon (attackers)

5. **Configure Combat**
   - Set Firing Order type
   - Set Wound Allocation method
   - Adjust simulation count if needed (default: 10,000)

6. **Calculate**
   - Click the "Calculate" button
   - Wait for simulation to complete
   - Review detailed results

### Advanced Techniques

#### Simulating Buffed Units
1. Add a unit
2. Load its datasheet and add models/weapons
3. Click "Unit Modifiers"
4. Apply buffs (e.g., +1 to hit, re-roll 1s)
5. Individual weapons can receive additional modifiers

#### Comparing Weapon Effectiveness
1. Add a single unit with one model
2. Add multiple weapon options to that model
3. Select which weapons to test
4. Run calculation
5. Review per-weapon statistics to compare

#### Testing Against Mixed Units
1. Add a defending unit
2. Add multiple different model types (e.g., 9 basic troops + 1 leader)
3. Apply different modifiers to the leader model
4. Run simulation to see how wounds distribute

#### Simulating Multi-Phase Combat
1. Set up initial combat
2. Calculate results
3. Note average models killed
4. Remove that many models from defenders
5. Set up second phase with reduced defenders
6. Calculate again

## Technical Details

### Monte Carlo Simulation
- Uses 10,000 simulations by default (configurable)
- Each simulation:
  1. Clones defending units
  2. Resolves attacks in configured order
  3. Applies wounds based on allocation method
  4. Tracks damage and models killed
- Results aggregated to produce statistics

### Modifier Stacking
Modifiers stack across all levels:
- Global + Unit + Model + Weapon modifiers all apply
- Example: +1 global hit + +1 weapon hit = +2 to hit total
- Re-rolls: Most specific takes precedence (weapon > unit > global)

### Critical Hits/Wounds
- Default threshold: Unmodified 6
- Can be customized per weapon (e.g., Anti-X 4+ = critical on 4+)
- Must be unmodified roll (re-rolls don't count as unmodified)

### Damage Application
Order of operations:
1. Calculate base damage from weapon
2. Apply weapon damage modifiers
3. Apply defender damage reduction
4. Apply halve damage (if applicable)
5. Apply Feel No Pain

## Comparison to Original Versus Mode

| Feature | Original | Enhanced (V2) |
|---------|----------|---------------|
| Multiple Attackers | ✗ | ✓ |
| Multiple Defenders | ✗ | ✓ |
| Multiple Weapons | ✗ | ✓ |
| Model-Level Control | ✗ | ✓ |
| Weapon-Level Modifiers | ✗ | ✓ |
| Unit-Level Modifiers | ✓ (limited) | ✓ (comprehensive) |
| Automatic Abilities | Partial | Full |
| Firing Order Control | ✗ | ✓ |
| Wound Allocation | ✗ | ✓ |
| Per-Weapon Stats | ✗ | ✓ |
| Per-Unit Stats | ✗ | ✓ |

## Future Enhancements

Potential additions:
- Save/Load battle scenarios
- Export results to CSV
- Graphical damage distribution chart
- More weapon abilities (Rapid Fire distance, Blast scaling, etc.)
- Aura effects between units
- Sequential round simulation
- Import from saved army lists
- Preset modifier templates (stratagems, doctrines, etc.)

## API Usage

For developers wanting to use the calculator programmatically:

```csharp
// Create context
var context = new VersusContextV2();

// Add attacking unit
var attackerUnit = new CombatUnit
{
    DatasheetId = 123,
    DatasheetName = "Space Marine Intercessors"
};

// Add model with weapon
var model = new CombatModel
{
    ModelProfile = modelProfile,
    CurrentWounds = 2,
    MaxWounds = 2
};

var weapon = new CombatWeapon
{
    WeaponProfile = weaponProfile,
    IsSelected = true,
    ParsedAbilities = WeaponAbilityParser.Parse(weaponProfile.Description)
};

model.Weapons.Add(weapon);
attackerUnit.Models.Add(model);
context.AttackingUnits.Add(attackerUnit);

// Add defender...
// (similar process)

// Calculate
var result = VersusCalculatorV2.Calculate(context, 10000);

// Access results
Console.WriteLine($"Average Damage: {result.AverageTotalDamage:F2}");
Console.WriteLine($"Models Killed: {result.AverageModelsKilled:F2}");
```

## Notes

- The original Versus mode (/versus) remains available and unchanged
- Both modes use the same core datasheet data
- V2 is more complex but offers significantly more control
- For quick calculations, the original mode may be simpler
- For detailed battle planning, V2 is recommended

## Support

For issues, feature requests, or questions about the Enhanced Versus Mode, please open an issue on the GitHub repository.
