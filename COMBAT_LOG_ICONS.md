# Combat Log Icon Reference

This document lists all Font Awesome icons used in the Combat Log Viewer component.

## Icons Used

| Purpose | Icon Class | Visual | Color |
|---------|-----------|--------|-------|
| **Attack** | `fa fa-fist-raised` | ✊ | Default |
| **Hit** | `fa fa-crosshairs` | ⊕ | Info (blue) |
| **Wound** | `fa fa-tint` | 💧 | Danger (red) |
| **Save** | `fa fa-shield` | 🛡 | Success (green) |
| **Damage** | `fa fa-bolt` | ⚡ | Danger (red) |
| **Success/Check** | `fa fa-check` | ✓ | Success (green) |
| **Failure/Miss** | `fa fa-times` | ✗ | Danger (red) |
| **Critical** | `fa fa-bolt` | ⚡ | Warning (yellow) |
| **Mortal Wound/Death** | `fa fa-skull` | ☠ | Danger (red) |
| **Dice** | `fa fa-dice` | 🎲 | Default |
| **Result** | `fa fa-flag-checkered` | 🏁 | Default |
| **Expand/Collapse** | `fa fa-chevron-down` | ▼ | Default |
| **Scroll/Log** | `fa fa-scroll` | 📜 | Default |
| **Info** | `fa fa-info-circle` | ℹ | Default |
| **Fight Separator** | `fa fa-crosshairs` | ⊕ | Default |

## Color Classes

Bootstrap color classes used throughout:

- `.text-success` - Green (#198754)
- `.text-danger` - Red (#dc3545)
- `.text-warning` - Yellow (#ffc107)
- `.text-info` - Blue (#0dcaf0)
- `.text-muted` - Gray (#6c757d)

## Badge Colors

Custom badge classes for results:

- `.result-badge-no-wounds` - Gray background (#6c757d)
- `.result-badge-saved` - Green background (#198754)
- `.result-badge-damage` - Red background (#dc3545)
- `.result-badge-destroyed` - Black background (#212529) with white text

## Phase Border Colors

Cards are color-coded by their primary phase:

- **Hit Phase**: Blue (#0dcaf0)
- **Wound Phase**: Red (#dc3545)
- **Save Phase**: Green (#198754)
- **Damage Phase**: Purple (#6f42c1)

## Usage Examples

### Header Summary
```razor
<i class="fa fa-fist-raised"></i> 4
→
<i class="fa fa-crosshairs"></i> 3
→
<i class="fa fa-tint"></i> 2
```
Displays: ✊ 4 → ⊕ 3 → 💧 2

### Hit Phase
```razor
<i class="fa fa-check text-success"></i> Hits: 3
<i class="fa fa-bolt text-warning"></i> Critical Hits: 1
<i class="fa fa-times text-danger"></i> Misses: 0
```

### Save Result
```razor
<i class="fa fa-shield"></i> Save Passed (Invuln)
<i class="fa fa-times"></i> Save Failed (Armor)
```

### Final Result
```razor
<i class="fa fa-bolt"></i> 5 damage inflicted
<i class="fa fa-skull"></i> Target destroyed
```

## Verbose Mode

When verbose mode is enabled, dice rolls are shown:

```razor
<i class="fa fa-dice"></i> Hit Dice: [6,5,4,3]
<i class="fa fa-dice"></i> Wound Dice: [5,4,2]
<i class="fa fa-dice"></i> Save Dice: [6]
```

## Notes

- All icons use the `fa` prefix (Font Awesome 5 compatible)
- Icons are combined with Bootstrap utility classes for colors
- Responsive design ensures icons display well on mobile devices
- Icons are semantic and match Warhammer 40K terminology
