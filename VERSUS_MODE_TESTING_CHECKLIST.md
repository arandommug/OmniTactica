# Versus Mode - Testing Checklist

Use this checklist to verify all functionality is working correctly.

## ✅ Phase 1: Basic Navigation & Setup

### Navigation
- [ ] App launches successfully
- [ ] Navigate to `/versus` from Browse page
- [ ] Navigate to `/versus` from mobile drawer menu
- [ ] Navigate to `/versus` from desktop sidebar
- [ ] Versus page loads without errors
- [ ] Page displays split-screen layout (Attacker | Defender)
- [ ] All buttons are visible and styled correctly

### Database Connection
- [ ] Database file exists at correct path
- [ ] Can query database using sqlite3 from terminal
- [ ] Verify sample data exists:
  ```
  sqlite3 "C:\Users\nerda\AppData\Local\User Name\com.companyname.OmniTactica\Data\wahapedia.db" "SELECT COUNT(*) FROM Datasheets;"
  ```
  Expected: Should return a number > 0

## ✅ Phase 2: Adding Units

### Add Attacker from Search
- [ ] Click "Add Unit" under Attacker section
- [ ] UnitPickerModal opens
- [ ] Faction dropdown populates
- [ ] Select a faction (e.g., "Adeptus Astartes")
- [ ] Unit list loads for that faction
- [ ] Search box filters units by name
- [ ] Click on "Intercessor Squad"
- [ ] Modal closes
- [ ] Unit appears in Attacker section
- [ ] Unit card shows:
  - [ ] Unit name
  - [ ] Model count
  - [ ] Weapon count
  - [ ] X button to remove

### Add Defender from Search
- [ ] Click "Add Unit" under Defender section
- [ ] Select faction "Orks"
- [ ] Click on "Boyz" unit
- [ ] Unit appears in Defender section

### Verify Auto-Population
#### Attacker (Intercessors):
- [ ] Click on Intercessor card to open editor
- [ ] Left panel shows datasheet reference
- [ ] Unit composition shows "1 Intercessor Sergeant" and "4-9 Intercessors"
- [ ] Right panel shows editable models:
  - [ ] Intercessor Sergeant (quantity 1)
  - [ ] Intercessors (quantity 4 or more)
- [ ] Each model has weapons:
  - [ ] Bolt pistol
  - [ ] Bolt rifle
  - [ ] Close combat weapon
- [ ] Weapon cards show correct stats:
  - [ ] Bolt rifle: Range 24", Type Ranged, A 2, BS 3, S 4, AP -1, D 1
  - [ ] Abilities: "assault, heavy"
- [ ] Close modal

#### Defender (Boyz):
- [ ] Click on Boyz card to open editor
- [ ] Verify unit composition parsed correctly
- [ ] Verify weapons assigned correctly
- [ ] Close modal

### Add from Bookmark
- [ ] Navigate to Browse page
- [ ] Bookmark a unit (e.g., "Warboss")
- [ ] Navigate back to Versus page
- [ ] Click bookmark dropdown on Attacker side
- [ ] Dropdown shows bookmarked units
- [ ] Click "Warboss"
- [ ] Unit is added to Attacker section

### Multiple Units
- [ ] Add a second attacker unit
- [ ] Both units show in list
- [ ] Add a second defender unit
- [ ] Both units show in list

## ✅ Phase 3: Unit Editor

### Opening Editor
- [ ] Click on any unit card
- [ ] Modal opens in full-screen
- [ ] Left panel shows datasheet reference:
  - [ ] Stats table (M, T, Sv, InvSv, W, Ld, OC)
  - [ ] Unit composition text
  - [ ] Loadout text
  - [ ] All weapons list with stats
  - [ ] Keywords list

### Models Section
- [ ] Right panel shows "Models" section
- [ ] Each model type has a card
- [ ] Quantity controls (+/- buttons) work
- [ ] Increment quantity works
- [ ] Decrement quantity works (minimum 1)
- [ ] "Add Modifier" button exists on each model

### Weapons Section (Per Model)
- [ ] Each model shows its weapons
- [ ] Weapon cards display:
  - [ ] Weapon name
  - [ ] Stats (Range, Type, A, BS/WS, S, AP, D)
  - [ ] Abilities badges
  - [ ] Checkbox for selection
- [ ] Weapon checkboxes can be toggled
- [ ] Unchecking weapon updates immediately
- [ ] "Add Modifier" button exists on each weapon
- [ ] "Add Weapon" button allows adding more weapons

### Unit Modifiers Section
- [ ] "Unit Modifiers" section exists
- [ ] "Add Modifier" button works
- [ ] Opens ModifierEditorModal

### Saving Changes
- [ ] Click "Save Changes" button
- [ ] Modal closes
- [ ] Unit card updates with new info
- [ ] Model count reflects changes
- [ ] Weapon count reflects changes

### Canceling Changes
- [ ] Open unit editor
- [ ] Make a change (e.g., change quantity)
- [ ] Click outside modal or close button
- [ ] Change is not saved
- [ ] Unit card shows original info

## ✅ Phase 4: Modifiers

### Opening Modifier Editor
- [ ] In unit editor, click "Add Modifier" (any level)
- [ ] ModifierEditorModal opens
- [ ] Shows preset modifiers section
- [ ] Shows custom builder section

### Preset Modifiers
Test at least 3 presets:
- [ ] "Reroll Hits 1s"
  - [ ] Click button
  - [ ] Modifier appears as badge
  - [ ] Badge shows "Reroll 1s to Hit"
- [ ] "Feel No Pain 5+"
  - [ ] Click button
  - [ ] Modifier appears
  - [ ] Shows "FNP 5+"
- [ ] "+1 to Hit"
  - [ ] Click button
  - [ ] Modifier appears
  - [ ] Shows "+1 to Hit"

### Custom Modifiers
Create a "Lance" modifier:
- [ ] Modifier name: "Lance"
- [ ] When: "Unit Charged This Turn"
- [ ] Then: "Add Damage Modifier"
- [ ] Value: 1
- [ ] Preview shows description
- [ ] Click "Save"
- [ ] Modifier appears with correct name
- [ ] Conditional badge shows "When Charged → +1 Damage"

Create an "Always Active" modifier:
- [ ] When: "Always"
- [ ] Then: "Add Hit Modifier"
- [ ] Value: -1
- [ ] Save
- [ ] Badge shows "-1 to Hit"

### Removing Modifiers
- [ ] Modifier has X button
- [ ] Click X
- [ ] Modifier is removed immediately

### Modifier Visibility
- [ ] Unit-level modifiers show on unit
- [ ] Model-level modifiers show on that model only
- [ ] Weapon-level modifiers show on that weapon only

## ✅ Phase 5: Simulation Settings

### Opening Settings
- [ ] Click "Settings" button at top
- [ ] SimulationSettingsModal opens

### Iterations Setting
- [ ] Number input shows default (10000)
- [ ] Can change to 1000
- [ ] Can change to 100000
- [ ] Cannot go below 100
- [ ] Cannot go above 100000

### Wound Allocation Setting
- [ ] Dropdown shows all options:
  - [ ] Even Distribution
  - [ ] Target Weakest Models First
  - [ ] Target Full Health Models First
  - [ ] Random Allocation
- [ ] Can select each option

### Range Setting
- [ ] Number input for range
- [ ] Can enter value (e.g., 12)
- [ ] Can leave empty
- [ ] Help text explains purpose

### Attacker Charged Checkbox
- [ ] Checkbox can be checked
- [ ] Checkbox can be unchecked
- [ ] Help text explains purpose

### Detailed Logging Checkbox
- [ ] Checkbox can be checked
- [ ] Checkbox can be unchecked
- [ ] Help text shows performance warning

### Saving Settings
- [ ] Click "Save"
- [ ] Modal closes
- [ ] Calculate button shows new iteration count
- [ ] Settings persist

## ✅ Phase 6: Running Calculations

### Prerequisites Check
- [ ] "Calculate" button is disabled when:
  - [ ] No attacker
  - [ ] No defender
  - [ ] Attacker has no selected weapons
- [ ] "Calculate" button is enabled when:
  - [ ] At least one attacker with selected weapons
  - [ ] At least one defender

### Running Simulation
#### Setup:
- Attacker: 5 Intercessors with bolt rifles
- Defender: 10 Ork Boyz
- Settings: 10,000 iterations, range 12" (for rapid fire)

#### Execution:
- [ ] Click "Calculate"
- [ ] Spinner appears with progress message
- [ ] Message shows iteration count (e.g., "Calculating 10,000 simulations...")
- [ ] Calculation completes (should take 2-10 seconds)
- [ ] Results appear

### Viewing Results

#### Summary Cards
- [ ] "Average Damage" card shows:
  - [ ] Numeric value (e.g., 4.2)
  - [ ] Icon and styling
- [ ] "Median Damage" card shows:
  - [ ] Numeric value (e.g., 4)
- [ ] "Models Killed" card shows:
  - [ ] Numeric value (e.g., 4.2)
- [ ] "Wipeout Probability" card shows:
  - [ ] Percentage (e.g., 0.3%)

#### Stage Breakdown Table
- [ ] Table has columns: Stage, Total, Success Rate
- [ ] Rows show:
  - [ ] Attacks (e.g., 20.0)
  - [ ] Hits (e.g., 13.3, 66.7%)
  - [ ] Critical Hits (e.g., 2.2)
  - [ ] Wounds (e.g., 6.7, 50.0%)
  - [ ] Critical Wounds (e.g., 1.1)
  - [ ] Failed Saves (e.g., 5.6, 83.3%)
  - [ ] Damage Dealt (e.g., 4.2)
- [ ] Percentages make sense

#### Damage Distribution Chart
- [ ] Chart exists
- [ ] X-axis shows damage values (0, 1, 2, 3, ...)
- [ ] Y-axis shows probability (%)
- [ ] Bars show distribution
- [ ] Highest bar is at expected damage value
- [ ] Chart is readable

#### Combat Log
- [ ] If detailed logging was enabled:
  - [ ] "Combat Log" section exists
  - [ ] Expandable section (click to expand/collapse)
  - [ ] Log entries are visible
  - [ ] Entries are color-coded by phase:
    - [ ] Yellow: Attacks
    - [ ] Cyan: Hit
    - [ ] Red: Wound
    - [ ] Green: Save
    - [ ] Purple: Damage
  - [ ] Each entry shows:
    - [ ] Step number
    - [ ] Phase badge
    - [ ] Message describing what happened
  - [ ] Log is scrollable if long
  - [ ] Font is monospace (readable)

### Running Multiple Scenarios
- [ ] Run calculation twice with same setup
- [ ] Results are similar but not identical (variance)
- [ ] Change a setting (e.g., range to 24")
- [ ] Run again
- [ ] Results change accordingly

## ✅ Phase 7: Edge Cases & Validation

### Weapon Ability Parsing
Test these specific units/weapons:
- [ ] **Rapid Fire**: Intercessor bolt rifle
  - [ ] At 12" range: ~20 attacks (doubled)
  - [ ] At 24" range: ~10 attacks (normal)
- [ ] **Melta**: Meltagun (if available)
  - [ ] Within melta range: damage increased
  - [ ] Outside melta range: normal damage
- [ ] **Torrent**: Hand flamer (if available)
  - [ ] Auto-hits (no hit rolls needed)
- [ ] **Lethal Hits**: Warboss kombi-weapon (if available)
  - [ ] Critical hits auto-wound
- [ ] **Devastating Wounds**: Thunder hammer (if available)
  - [ ] Critical wounds become mortal wounds
- [ ] **Twin-Linked**: Twin slugga (if available)
  - [ ] Reroll wounds
- [ ] **Blast**: Frag grenades (if available)
  - [ ] More attacks vs larger units

### Stats Parsing
- [ ] Invulnerable saves parse correctly (e.g., "5+", "4+")
- [ ] Save values parse correctly (e.g., "3+", "6+")
- [ ] Movement parses correctly (e.g., "6\"", "12\"")
- [ ] Attacks parse correctly:
  - [ ] Fixed: "2", "10"
  - [ ] Dice: "D6", "2D6"
  - [ ] Combo: "6+D6", "D6+3"
- [ ] Damage parses correctly:
  - [ ] Fixed: "1", "3"
  - [ ] Dice: "D3", "D6"
  - [ ] Modified: "D6+1", "D6+2"

### Modifier Conditions
- [ ] "Always" modifier always applies
- [ ] "Unit Charged" modifier only applies when "Attacker Charged" is checked
- [ ] "Within Half Range" modifier only applies when range is set and <= half weapon range
- [ ] Multiple modifiers can be active simultaneously
- [ ] Modifiers stack correctly (+1 hit, +1 hit = +2 hit)

### Unit Composition Parsing
Test these patterns:
- [ ] "1 Warboss" → 1 model
- [ ] "5-10 Intercessors" → minimum 5 models
- [ ] "1 Sergeant" + "4-9 Marines" → two model types

### Weapon Loadout Parsing
- [ ] "Every model is equipped with: weapon1; weapon2" → all models get both weapons
- [ ] "This model is equipped with: weapon1" → only specific model gets weapon
- [ ] HTML tags are stripped from loadout text

## ✅ Phase 8: User Experience

### Responsiveness
- [ ] Mobile (narrow screen):
  - [ ] Split-container stacks vertically
  - [ ] Attacker section on top
  - [ ] Defender section below
  - [ ] All buttons accessible
- [ ] Desktop (wide screen):
  - [ ] Split-container shows side-by-side
  - [ ] Equal width columns
  - [ ] All content fits

### Performance
- [ ] 1,000 iterations: < 1 second
- [ ] 10,000 iterations: 1-5 seconds
- [ ] 100,000 iterations: 10-30 seconds
- [ ] UI doesn't freeze during calculation
- [ ] Progress indicator shows during calculation

### Usability
- [ ] Button labels are clear
- [ ] Icons match their function
- [ ] Color coding is consistent:
  - [ ] Red = Attacker
  - [ ] Blue = Defender
  - [ ] Green = Success/Calculate
  - [ ] Yellow = Warning/Swap
- [ ] Hover effects work on buttons
- [ ] Form controls are easy to use
- [ ] Modals are easy to close

## ✅ Phase 9: Swap & Clear

### Swap Sides
- [ ] Add attacker and defender
- [ ] Click "Swap" button
- [ ] Attacker becomes defender
- [ ] Defender becomes attacker
- [ ] All units and modifiers preserved
- [ ] Settings preserved

### Clear All
- [ ] Add multiple units
- [ ] Click "Clear All" button
- [ ] All attackers removed
- [ ] All defenders removed
- [ ] Results cleared
- [ ] Page returns to empty state

## ✅ Phase 10: Integration

### From Browse Page
- [ ] Navigate to Browse page
- [ ] Find a unit (e.g., "Space Marine Captain")
- [ ] Click "Set as Attacker" button (gun icon)
- [ ] Redirects to /versus
- [ ] Unit is added as attacker
- [ ] Auto-populated with models and weapons

### From Unit Viewer
- [ ] Navigate to a unit's detail page
- [ ] Click "Set as Defender" button (shield icon)
- [ ] Redirects to /versus
- [ ] Unit is added as defender
- [ ] Auto-populated with models and weapons

### Bookmark Integration
- [ ] Bookmark a unit
- [ ] Versus page bookmark dropdown shows it
- [ ] Can quick-add from bookmark
- [ ] Works for both attacker and defender

## 📊 Sample Test Scenarios

### Scenario 1: Basic Infantry vs Infantry
- **Attacker**: 5 Intercessors @ 12" range
- **Defender**: 10 Ork Boyz
- **Expected**: ~4-5 kills, ~40-50% casualty rate
- **Validate**: Results are in expected range

### Scenario 2: Heavy Weapon vs Vehicle
- **Attacker**: 1 model with Lascannon
- **Defender**: Tank (T12, Sv2+, W12)
- **Expected**: ~2-4 damage per hit
- **Validate**: Damage variance due to D6 rolls

### Scenario 3: Melta Weapon
- **Setup**: 1 model with Meltagun vs Tank
- **Test A**: Range 6" (within melta)
  - **Expected**: Higher damage
- **Test B**: Range 12" (outside melta)
  - **Expected**: Lower damage
- **Validate**: Test A damage > Test B damage

### Scenario 4: Conditional Modifier
- **Setup**: Add Lance modifier to weapon (+1 damage when charged)
- **Test A**: Attacker charged = false
  - **Expected**: Normal damage
- **Test B**: Attacker charged = true
  - **Expected**: +1 damage
- **Validate**: Test B damage > Test A damage

### Scenario 5: Rapid Fire
- **Setup**: 5 Intercessors with Rapid Fire 1 weapons
- **Test A**: Range 24" (outside half range)
  - **Expected**: 10 attacks (2 per model)
- **Test B**: Range 12" (within half range)
  - **Expected**: 20 attacks (4 per model with Rapid Fire)
- **Validate**: Test B attacks = 2 × Test A attacks

## 🐛 Known Issues to Check

### Issues to Verify Are Fixed:
- [ ] Bootstrap modal syntax uses modern API (not jQuery)
- [ ] All using statements present (Regex in UnitEditorModal)
- [ ] WeaponAbilityParser uses correct property types (bool vs int?)
- [ ] TwinLinked doesn't incorrectly double attacks
- [ ] Melta property is int? (not bool + separate MeltaRange)
- [ ] No references to old V2/V3 files
- [ ] No references to UnitModifiers/ModelModifiers/WeaponModifiers classes
- [ ] VersusService has all required methods
- [ ] VersusCalculator.Calculate is static and accessible

### Potential Issues to Watch For:
- [ ] Large simulations (100,000+) don't crash
- [ ] Multiple units don't cause performance issues
- [ ] Complex modifiers don't cause infinite loops
- [ ] Database queries don't timeout
- [ ] Memory doesn't leak on repeated calculations

## ✅ Final Checks

### Code Quality
- [ ] No build errors
- [ ] No build warnings (check Output window)
- [ ] No runtime errors in browser console
- [ ] No TODO comments left in code

### Documentation
- [ ] VERSUS_MODE_IMPLEMENTATION.md exists and is accurate
- [ ] VERSUS_MODE_QUICK_START.md exists and is helpful
- [ ] This testing checklist covers all features

### Deployment Readiness
- [ ] App runs in Debug mode
- [ ] App runs in Release mode
- [ ] Database path is correct for target environment
- [ ] All assets (icons, etc.) are present

## 📝 Test Results

Date: _______________
Tester: _______________

Total Tests: _______
Passed: _______
Failed: _______
Skipped: _______

### Failed Tests (if any):
1. _______________________________________________
2. _______________________________________________
3. _______________________________________________

### Notes:
_______________________________________________
_______________________________________________
_______________________________________________

### Overall Status:
- [ ] ✅ All tests passed - Ready for use
- [ ] ⚠️ Minor issues - Usable with caveats
- [ ] ❌ Major issues - Needs fixes

## 🎉 Completion

Once all tests pass, the Versus Mode is complete and ready for use!

**Congratulations!** 🎊
