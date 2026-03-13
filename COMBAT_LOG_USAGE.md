# Combat Log Viewer - Usage Guide

## Overview

The Combat Log Viewer is a Blazor component that displays detailed combat simulation results in an expandable, mobile-friendly card format.

## How to Use

### 1. Basic Usage

The component is automatically used in the Versus Calculator page (`VersusPage.razor`):

1. Navigate to the **Versus Calculator** page
2. Add attacking and defending units
3. Configure simulation settings
4. Click **Calculate**
5. Scroll down to the **Combat Log** section

### 2. Expanding/Collapsing Cards

Each attack sequence is shown as a collapsed card by default:

- **Click anywhere on a card header** to expand and see detailed results
- **Click again** to collapse the card
- The card shows a summary in the header even when collapsed

### 3. Verbose Mode

Toggle the **Verbose** switch in the Combat Log header to show:
- Individual dice rolls for hits
- Individual dice rolls for wounds
- Individual dice rolls for saves

This is useful for:
- Debugging simulations
- Understanding probability outcomes
- Verifying calculations

### 4. Expand All

Click the **expand/compress** button in the Combat Log header to:
- **Expand**: Remove scroll limit and show all content
- **Compress**: Return to scrollable view with height limit

## Reading the Combat Log

### Header Summary (Always Visible)

```
Knight Master — Power Fist
✊ 4 → ⊕ 3 → 💧 2
Result: 2 Damage
```

- **First line**: Attacker model and weapon name
- **Second line**: Attack flow (Attacks → Hits → Wounds)
- **Third line**: Final result badge

### Result Badges

- **Gray (No Wounds)**: Attack failed to wound
- **Green (Saved)**: Wounds were saved by the defender
- **Red (X Damage)**: Amount of damage inflicted
- **Black (Destroyed)**: Target was destroyed

### Detailed Phases

When expanded, each card shows:

#### 1. Attacks Phase
- Total number of attacks
- Modifiers applied (e.g., Blast, Rapid Fire)

#### 2. Hit Roll Phase
- ✓ Successful hits
- ⚡ Critical hits
- ✗ Misses
- ☠ Auto-wounds from special abilities
- Effects (e.g., "Sustained Hits generated +2 additional hits")

#### 3. Wound Roll Phase
- ✓ Successful wounds
- ⚡ Critical wounds
- ✗ Failed to wound
- ☠ Mortal wounds
- Effects (e.g., "Devastating Wounds: Critical wound converted to mortal wound")

#### 4. Save Phase
Grouped by defender:
- Roll results (e.g., "Roll: 6 vs 4+")
- 🛡 Save Passed or ✗ Save Failed
- Save type (Armor or Invuln)
- ☠ Mortal wounds bypass saves

#### 5. Damage Phase
- Individual damage events
- Remaining wounds on targets
- Model destruction notifications

#### 6. Result Summary
- Final outcome
- Total damage inflicted
- ☠ Target destroyed notification

## Example Reading

```
═══════════════════════════════════════
Deathwing Knight #1 — Storm Bolter
═══════════════════════════════════════

✊ 4 → ⊕ 3 → 💧 2
Result: Saved
[Click to expand]
```

Expanded view:

```
Attacks
  Total: 4
  Rapid Fire: Added 2 attacks (within half range)

Hit Roll
  ✓ Hits: 3
  ⚡ Critical Hits: 0
  ✗ Misses: 1

Wound Roll
  ✓ Wounds: 2
  ⚡ Critical Wounds: 0
  ✗ Failed: 1

Save — Land Raider
  Roll: 6 vs 4+
  🛡 Save Passed (Armor)
  Roll: 5 vs 4+
  🛡 Save Passed (Armor)

Result
  All wounds saved
```

## Tips

### For Quick Analysis
- Keep cards collapsed
- Scan result badges for outcomes
- Use color coding to identify phases

### For Detailed Review
- Expand specific attacks of interest
- Enable verbose mode for dice transparency
- Look for effect messages to understand special rules

### For Mobile Use
- Cards are optimized for small screens
- Tap cards to expand/collapse
- Summary line uses compact arrow notation
- No horizontal scrolling required

## Performance Notes

- Only the first simulation's log is displayed (out of 10,000 runs)
- This is a representative sample showing how combat resolves
- Each simulation may vary due to random dice rolls
- The aggregate statistics show averages across all simulations

## Fight Phase Separators

If the simulation involves multiple combat phases, you'll see separators:

```
═══════════════════════════════════════
⊕ Fight Phase ⊕
Deathwing Knights vs Land Raider
═══════════════════════════════════════
```

This helps organize the log when multiple units attack in sequence.

## Troubleshooting

### No Combat Log Shown
- Ensure "Detailed Logging" is enabled in simulation settings
- Click the settings (⚙) button to verify
- Re-run the calculation

### Cards Not Expanding
- Ensure JavaScript is enabled
- Check that Bootstrap is loaded correctly
- Try refreshing the page

### Icons Not Showing
- Verify Font Awesome is loaded (check console for errors)
- Ensure internet connection is active (if using CDN)
- Check that `lib/fontawesome` exists in wwwroot

## Integration with Your Code

To use the component in other pages:

```razor
@using OmniTactica.Components.Shared

<CombatLogViewer 
    Entries="@yourCombatLogEntries" 
    VerboseMode="@showDiceRolls" />
```

Parameters:
- `Entries` (required): List of `CombatLogEntry` objects
- `VerboseMode` (optional): Boolean to show dice rolls (default: false)

## Related Documentation

- [COMBAT_LOG_IMPLEMENTATION.md](./COMBAT_LOG_IMPLEMENTATION.md) - Technical implementation details
- [COMBAT_LOG_ICONS.md](./COMBAT_LOG_ICONS.md) - Icon reference guide

## Support

For issues or questions:
1. Check the implementation documentation
2. Review the icon reference
3. Verify Font Awesome and Bootstrap are loaded
4. Check browser console for errors
