# Combat Log - What's New

## 🎉 New Features

### 1. Verbose Mode Toggle (FIXED)
The verbose mode toggle now works correctly! 

**How to use**:
- Click the **Verbose** toggle switch in the Combat Log header
- Expands cards to see dice roll values
- Useful for debugging and understanding probability

**Shows**:
- 🎲 Hit Dice: [6,5,4,3]
- 🎲 Wound Dice: [5,4,2]
- 🎲 Save Dice: [6]

---

### 2. Weapon Abilities & Modifiers Section (NEW)
Every attack now shows a prominent section listing all active weapon abilities and modifiers!

**What you'll see**:

```
═══════════════════════════════════════════
Deathwing Knight — Power Fist
═══════════════════════════════════════════

🌟 Active Weapon Abilities & Modifiers
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
[Assault]  [Anti-Infantry 3+]  [Devastating Wounds]
⚙ Unit Charged: +1 to Wound
⚙ Target is Infantry: Critical Wound on 3+
```

**Weapon Abilities** (shown as blue badges):
- Assault
- Blast
- Devastating Wounds
- Hazardous
- Heavy
- Ignores Cover
- Indirect Fire
- Lethal Hits
- Melta X
- Pistol
- Precision
- Rapid Fire X
- Sustained Hits X
- Torrent
- Twin-Linked
- Anti-[Keyword] X+

**Active Modifiers** (shown with gear icon):
- +X to Hit/Wound/Save
- Reroll abilities
- Damage modifiers
- Critical thresholds
- Special effects

---

### 3. Better Anti-X Visibility (IMPROVED)
Anti-Infantry, Anti-Vehicle, and other Anti-X abilities are now clearly visible!

**Before**: Users had to expand wound phase and look for effects
**Now**: Shows prominently in abilities section at the top

**Example**:
```
🌟 Active Weapon Abilities & Modifiers
[Anti-Infantry 3+]  [Anti-Vehicle 4+]
⚙ Target is Infantry: Critical Wound on 3+
```

When the weapon attacks an Infantry unit:
- Ability badge shows: **[Anti-Infantry 3+]**
- Modifier shows: **⚙ Target is Infantry: Critical Wound on 3+**
- Wound phase shows: **Effect: Anti-Infantry: Critical wounds on 3+**
- Critical wounds count separately in the result

---

## 📊 How It Looks

### Collapsed View (Summary)
```
┌────────────────────────────────────────────┐
│ Knight Master — Power Fist                 │
│ ✊ 4 → ⊕ 3 → 💧 2                          │
│ Result: 2 Damage                      ▼    │
└────────────────────────────────────────────┘
```

### Expanded View (Full Details)
```
┌────────────────────────────────────────────┐
│ Knight Master — Power Fist                 │
│ ✊ 4 → ⊕ 3 → 💧 2                          │
│ Result: 2 Damage                      ▲    │
├────────────────────────────────────────────┤
│                                            │
│ 🌟 Active Weapon Abilities & Modifiers    │
│ ┌──────────────────────────────────────┐  │
│ │ [Anti-Infantry 3+] [Devastating Wounds]│
│ │ ⚙ Unit Charged: +1 to Wound           │
│ │ ⚙ Target is Infantry: Critical Wound  │
│ │    on 3+                               │
│ └──────────────────────────────────────┘  │
│                                            │
│ ✊ Attacks                                 │
│   Total: 4                                 │
│                                            │
│ ⊕ Hit Roll                                │
│   ✓ Hits: 3                               │
│   ⚡ Critical Hits: 0                      │
│   ✗ Misses: 1                             │
│   [Verbose] 🎲 Hit Dice: [6,5,4,3]       │
│                                            │
│ 💧 Wound Roll                             │
│   ✓ Wounds: 2                             │
│   ⚡ Critical Wounds: 1                    │
│   ✗ Failed: 0                             │
│   Effect: Critical wounds on 3+           │
│   [Verbose] 🎲 Wound Dice: [6,4,3]       │
│                                            │
│ 🛡 Save — Land Raider                     │
│   Roll: 5 vs 4+                           │
│   ✗ Save Failed (Armor)                   │
│   [Verbose] 🎲 Save Dice: [5]            │
│                                            │
│ ⚡ Damage                                  │
│   2 damage to Land Raider - 20/22 remain │
│                                            │
│ 🏁 Result                                 │
│   ⚡ 2 damage inflicted                   │
│                                            │
└────────────────────────────────────────────┘
```

---

## 🎮 User Guide

### Viewing Weapon Abilities
1. Run a versus calculation
2. Expand any combat log card
3. Look at the top section with 🌟 icon
4. Blue badges = weapon abilities
5. Gear icons = active modifiers

### Understanding Anti-X
When you see **[Anti-Infantry 3+]**:
- Weapon has special bonus against Infantry
- Wound rolls of 3+ are critical wounds
- Critical wounds trigger Devastating Wounds if equipped
- Look in Wound Roll section for "Critical Wounds" count

### Toggling Verbose Mode
1. Find the Combat Log section
2. Look for the **Verbose** switch (🎲 icon)
3. Click to toggle on/off
4. Dice rolls appear/disappear in expanded cards
5. Useful for understanding randomness

---

## 🔍 What to Look For

### Weapon Is Working Correctly If:
- ✅ Abilities appear in the abilities section
- ✅ Active modifiers show with descriptions
- ✅ Effects appear in appropriate phases
- ✅ Critical wounds counted separately
- ✅ Modifiers apply based on conditions

### Something Might Be Wrong If:
- ❌ Expected ability doesn't appear
- ❌ Modifier shows but doesn't affect results
- ❌ Anti-X shows but no critical wounds
- ❌ Condition met but modifier not active

---

## 💡 Tips

1. **Check abilities first**: Before investigating results, check what abilities are active
2. **Compare modifiers**: Active modifiers show what bonuses are currently applying
3. **Use verbose for dice**: Enable verbose to see if you're just unlucky with rolls
4. **Watch for conditions**: Modifiers only show if their conditions are met
5. **Critical vs Normal**: Critical wounds are tracked separately - look for ⚡ icon

---

## 📱 Mobile Friendly

All new features work great on mobile:
- Abilities shown as compact badges
- Modifiers in readable text format
- Touch to expand/collapse cards
- Scrollable sections for long lists
- Icons remain clear on small screens

---

## 🐛 Known Issues Fixed

- ✅ Verbose toggle not updating UI
- ✅ Anti-X abilities hidden in effects
- ✅ No way to see active modifiers
- ✅ Critical wound threshold unclear
- ✅ Weapon abilities not visible

---

## 🚀 Performance

All features are optimized:
- Only first simulation logged (out of 10,000)
- Abilities collected once per weapon
- Modifiers evaluated efficiently
- UI renders smoothly
- No noticeable performance impact

---

## ❓ FAQ

**Q: Why don't I see any abilities?**  
A: Weapon might not have any abilities, or logging might be disabled. Check simulation settings.

**Q: Why don't my modifiers show?**  
A: Modifiers only appear if their conditions are met. Check if "Unit Charged" is enabled, target has correct keywords, etc.

**Q: What does "Critical Wound on 3+" mean?**  
A: Wound rolls of 3 or higher are treated as critical wounds and may trigger special effects.

**Q: Can I see dice rolls?**  
A: Yes! Enable Verbose mode with the toggle switch.

**Q: Why does it say "No wounds inflicted" but I see wounds in the phase?**  
A: All wounds were saved by the defender. Check the Save phase section.

---

## 🎯 Summary

Three major improvements:
1. **Verbose toggle works** - See your dice rolls
2. **Abilities visible** - Know what your weapon can do
3. **Modifiers shown** - Understand why results vary

All these features help you:
- ✅ Understand combat results
- ✅ Debug weapon configurations
- ✅ Learn Warhammer rules
- ✅ Optimize army composition
- ✅ Validate calculations

Enjoy the improved combat log! 🎉
