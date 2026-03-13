# Versus Mode - Quick Start Guide

## 🚀 Quick Start (5 Minutes)

### Step 1: Navigate to Versus Mode
1. Open the app
2. Click **"Versus"** in the navigation menu
   - Mobile: Swipe up from bottom drawer
   - Desktop: Click "Versus" in sidebar

### Step 2: Add an Attacker
1. Click **"Add Unit"** button under "Attacker" (red side)
2. Select a faction (e.g., "Adeptus Astartes")
3. Search for a unit (e.g., "Intercessor")
4. Click on the unit card

✅ The unit is now added with:
- Default models from unit composition
- Default weapons from loadout
- All abilities automatically parsed

### Step 3: Add a Defender
1. Click **"Add Unit"** button under "Defender" (blue side)
2. Select a faction (e.g., "Orks")
3. Search for a unit (e.g., "Boyz")
4. Click on the unit card

### Step 4: Run the Simulation
1. Click **"Calculate"** button at the top
2. Wait a few seconds
3. View the results!

## 📊 Understanding Results

### Summary Cards
- **Average Damage**: Mean damage dealt across all simulations
- **Median Damage**: Middle value (50th percentile)
- **Models Killed**: Average number of models destroyed
- **Wipeout Probability**: Chance of completely destroying the defender

### Stage Breakdown
Shows what happens at each phase:
1. **Attacks**: How many attacks were made
2. **Hits**: How many hit (with hit rate %)
3. **Wounds**: How many wounded (with wound rate %)
4. **Saves**: How many failed saves
5. **Damage**: Final damage dealt

### Damage Distribution
Graph showing probability of each damage outcome:
- X-axis: Damage dealt (0, 1, 2, 3, etc.)
- Y-axis: Probability (%)
- Helps understand variability and consistency

### Combat Log
Step-by-step breakdown of what happened in ONE simulation:
- Color-coded by phase
- Shows rolls, modifiers, and results
- Useful for understanding WHY certain results occurred

## 🛠️ Customizing Units

### Editing a Unit
1. Click on the unit summary card
2. Full-screen editor opens

**Left Panel** (Reference):
- Original datasheet stats
- Available weapons and options

**Right Panel** (Editable):
- Add/remove model types
- Change quantities (spinners)
- Toggle weapon selection (checkboxes)
- Add modifiers

### Adding Models
1. In unit editor, click **"Add Model"** button
2. Select model type from dropdown
3. Set quantity
4. Weapons are added automatically from loadout

### Toggling Weapons
1. In unit editor, find the model
2. Check/uncheck weapon boxes
3. Only selected weapons participate in combat

### Changing Quantities
1. Use **+/-** buttons next to each model type
2. Minimum: 1
3. Maximum: from unit composition (e.g., 10 for "5-10 Intercessors")

## ✨ Adding Modifiers

### Quick Presets
1. In unit editor, click **"Add Modifier"**
2. Click a preset button (e.g., "Reroll Hits 1s", "Feel No Pain 5+")
3. Done!

### Custom Modifiers
1. Click **"Add Modifier"**
2. Select **"When"** condition:
   - Example: "Unit Charged This Turn"
3. Select **"Then"** effect:
   - Example: "Add Attacks"
4. Enter value if needed:
   - Example: +1
5. Click **"Save"**

### Modifier Examples

**Lance** (classic charge bonus):
- When: "Unit Charged This Turn"
- Then: "Add Damage Modifier"
- Value: +1

**Heavy Weapon** (bonus when stationary):
- When: "Unit Remained Stationary"
- Then: "Hit Modifier"
- Value: +1

**Cover**:
- When: "Always"
- Then: "Save Modifier"
- Value: +1

**Reanimation Protocols** (Necron ability):
- When: "Always"
- Then: "Feel No Pain"
- Value: 5

**Oath of Moment** (Space Marine ability):
- When: "Always"
- Then: "Reroll Hits"

## ⚙️ Simulation Settings

Click **"Settings"** button at the top to adjust:

### Number of Simulations
- **1,000**: Fast, less accurate
- **10,000**: Good balance (default)
- **100,000**: Slow, most accurate

### Wound Allocation
- **Even Distribution**: Spreads damage evenly (fastest)
- **Target Weakest**: Focuses on wounded models first
- **Target Full Health**: Maximizes overkill (realistic for some armies)
- **Random**: Realistic random targeting

### Range to Target
Enter distance in inches:
- **12"**: Affects Rapid Fire weapons (doubles shots if within half range)
- **6"**: Affects Melta weapons (increases damage)

### Attacker Charged
Check this if the attacker charged:
- Affects charge-dependent abilities (Lance, etc.)

### Detailed Logging
Check to see step-by-step combat log:
- ⚠️ Performance impact on large simulations
- Only logs the FIRST simulation

## 🎯 Common Scenarios

### Space Marines vs Orks
**Attacker**: 5 Intercessors
- Bolt rifles: 24" range, 2 attacks each, Assault/Heavy
- Total: 10 attacks at 3+ BS, S4, AP-1, D1

**Defender**: 10 Ork Boyz
- T5, Sv6+, W1 each

**Expected Result**: ~4 kills (40% of the unit)

### Heavy Weapon vs Tank
**Attacker**: 1 model with Lascannon
- 48" range, 1 attack, BS3+, S12, AP-3, D D6+1

**Defender**: Tank (T12, Sv2+, W12)

**Expected Result**: ~3-4 damage per hit

### Melta Weapon at Different Ranges
**Attacker**: 1 model with Meltagun (Melta 2)
- 12" range, 1 attack, BS3+, S9, AP-4, D D6

**Test at 6" (within Melta range)**:
- Damage increases to D6+2

**Test at 12" (outside Melta range)**:
- Damage stays at D6

## 💡 Pro Tips

### Tip 1: Use Bookmarks
- Bookmark frequently used units
- Click the bookmark icon in unit picker
- Quick-add from bookmark dropdown

### Tip 2: Swap Sides
- Testing how a unit performs as attacker vs defender?
- Click **"Swap"** button instead of re-adding units

### Tip 3: Save Before Major Changes
- No undo button (yet)!
- If making major changes, note down what you had
- Or take a screenshot

### Tip 4: Start with Defaults
- Don't over-customize on first test
- Run with default loadout first
- Then adjust one thing at a time to see impact

### Tip 5: Check the Combat Log
- If results seem weird, enable detailed logging
- Run a smaller simulation (1,000 iterations)
- Check the combat log to see what's happening

### Tip 6: Use Range Settings
- Always set range for Rapid Fire weapons
- Set to half of weapon range for maximum effect
- Example: Bolt Rifle (24" range) → Set range to 12"

### Tip 7: Model Charge Bonuses
- Enable "Attacker Charged" in settings
- Add custom modifiers for charge bonuses:
  - +1 Attack, +1 Strength, etc.

### Tip 8: Realistic Scenarios
- Use "Even Distribution" for spread fire
- Use "Target Weakest" for focused fire
- Use "Random" for most realistic simulation

## 🐛 Troubleshooting

### "Calculate" Button is Disabled
**Cause**: No attacker or no defender or no selected weapons
**Fix**: 
1. Make sure both attacker and defender are added
2. Make sure at least one weapon is selected (checkbox)

### No Weapons Showing Up
**Cause**: Loadout parsing failed or datasheet has no weapons
**Fix**: 
1. Open unit editor
2. Manually add weapons using "Add Weapon" button
3. Select from dropdown

### Results Seem Too High/Low
**Possible Causes**:
1. Weapon abilities not parsed correctly → Check unit editor
2. Modifiers stacking incorrectly → Remove and re-add
3. Range settings incorrect → Check simulation settings
4. Stats parsed incorrectly → Check unit editor

**Fix**: 
1. Enable detailed logging
2. Run 1,000 simulations
3. Check combat log
4. Verify stats match datasheet

### App Crashes During Calculation
**Cause**: Too many iterations or infinite loop
**Fix**: 
1. Reduce iterations to 1,000
2. Check for conflicting modifiers
3. Restart app

### Modifiers Not Working
**Cause**: Condition not met or effect not supported
**Fix**: 
1. Check condition settings (e.g., range, charged status)
2. Try "Always" condition first to verify effect works
3. Check modifier preview text

## 📚 Advanced Features

### Multi-Model Units
Units with multiple model types (e.g., Sergeant + Marines):
1. Each model type has its own weapons
2. Edit each separately
3. Set quantities for each

### Weapon Profiles with Modes
Weapons with multiple firing modes (e.g., Plasma - Standard/Supercharge):
- Both modes appear as separate weapons
- Toggle which one to use

### Anti-Keywords
Weapons with Anti-Infantry/Vehicle/Monster:
- Automatically parsed from description
- Provides bonus to wound certain targets
- Example: Anti-Infantry 4+ → Wound Infantry on 4+ regardless of S vs T

### Conditional Damage
Complex abilities:
1. Add multiple modifiers to same weapon
2. Use different conditions for each
3. Example: 
   - Modifier 1: When "Always" → Then "Add Attacks"
   - Modifier 2: When "Charged" → Then "Add Damage Modifier"

## 🎓 Understanding the Math

### Hit Rate Formula
```
Base: (7 - BS) / 6
Example: BS 3+ = (7-3)/6 = 4/6 = 66.67%
```

### Wound Rate Formula (simplified)
```
S >= 2×T: Wound on 2+  (83.33%)
S > T:    Wound on 3+  (66.67%)
S = T:    Wound on 4+  (50%)
S < T:    Wound on 5+  (33.33%)
S <= T/2: Wound on 6+  (16.67%)
```

### Save Rate
```
Modified Save = Armor Save - AP
If Modified Save > Invulnerable Save:
    Use Invulnerable Save

Fail Rate = (7 - Save) / 6
Example: 4+ save = (7-4)/6 = 3/6 = 50% fail rate
```

### Expected Damage
```
Expected = Attacks × Hit Rate × Wound Rate × (1 - Save Rate) × Average Damage
```

### Why Use Monte Carlo?
- Handles variance (dice, D6 damage, etc.)
- Simulates edge cases
- Shows probability distribution
- More realistic than formulas alone

## 📞 Need Help?

Check these resources:
1. **Combat Log**: Most issues are visible here
2. **Documentation**: VERSUS_MODE_IMPLEMENTATION.md (technical details)
3. **Database**: Use sqlite3 to inspect wahapedia.db if needed

## 🎉 Have Fun!

Remember: This is a tool for learning and planning, not a substitute for playing the actual game!

**Key Insight**: Sometimes the "best" unit on paper loses due to dice variance. This tool helps you understand that variance and plan accordingly.

**Good luck on the battlefield!** ⚔️
