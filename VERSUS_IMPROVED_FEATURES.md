# Versus Calculator - Complete Feature Review

## ✅ Core Requirements - ALL IMPLEMENTED

### 1. ✅ One-Screen Layout (No Tabs)
- **Attacker on Left**, **Defender on Right** - visible simultaneously
- Split-screen grid layout using CSS Grid
- Responsive design (stacks on mobile)
- **NO TABS** - both sides always visible

### 2. ✅ Swap Functionality
- **"Swap" button** in header
- Instantly swaps:
  - All attacking units ↔ defending units
  - Attacker global modifiers ↔ defender global modifiers
- One-click operation

### 3. ✅ Summary View by Default
**Each unit card shows compact summary:**
- **Models**: Grouped by type (e.g., "1× Knight Master", "4× Deathwing Knights")
- **Weapons**: Grouped with stats (e.g., "2× Mace of absolution (A4 S6 AP-2 D2)")
- **Active Modifiers**: Badges showing hit/wound/save bonuses, rerolls, FNP, cover, etc.
- **Defender Stats**: T, Sv, W when applicable

**NOT shown by default:**
- Individual model/weapon controls
- Detailed modifier inputs
- Weapon selection checkboxes

### 4. ✅ Edit Opens Modal
- **"Edit" button** on each unit card
- Opens `UnitEditorModal` with:
  - **Models & Weapons Tab**: Add/remove models, assign/toggle weapons
  - **Unit Modifiers Tab**: Full modifier editing interface
- Changes saved when modal closes

### 5. ✅ Auto-Population from Composition Table

**Smart Parsing:**
- Reads `Datasheets_unit_composition.csv` data
- Parses patterns like:
  - "1 Knight Master"
  - "4 Deathwing Knights"  
  - "1-2 Spanners" (uses minimum count)
- Handles "OR" lines (skips, uses first option)
- Removes HTML tags (`<span class="kwb">EPIC</span>` etc.)

**Smart Model Matching:**
- Tries exact name match first
- Falls back to partial matching (contains)
- Handles singular/plural variations
- Always provides fallback

**Result:** When you add a unit, it automatically creates the correct models.

### 6. ✅ Auto-Population from Loadout

**Intelligent Weapon Assignment:**
- Parses loadout text from `Datasheets` table
- Recognizes patterns like:
  - "**The Knight Master is equipped with:** great weapon of the Unforgiven"
  - "**Every Deathwing Knight is equipped with:** mace of absolution"
  - "**Each model is equipped with:** power weapon"
- Matches weapons to specific model types
- Multiple fallback strategies

**Result:** Models are automatically equipped with correct weapons based on loadout.

### 7. ✅ Auto-Population from Wargear Options

**Available Weapons:**
- All weapons from `Datasheets_wargear` table are available
- Can be added via dropdown in Unit Editor modal
- Weapon stats (Range, A, BS/WS, S, AP, D) displayed

### 8. ✅ Global Modifiers
- **"Global Mods" button** in header
- Opens inline modal with:
  - **Attacker Modifiers**: Hit, Wound, Save, Ignore Cover
  - **Defender Modifiers**: Hit, Wound, Save, In Cover
- Applied to ALL units on that side

### 9. ✅ Calculate & Results
- **"Calculate" button** (disabled until valid setup)
- Runs 10,000 Monte Carlo simulations
- Shows results:
  - **Average Damage**
  - **Average Models Killed**
  - **Median Damage**
  - **Wipeout Probability**

### 10. ✅ Unit Management
- **Add Unit**: Opens unit picker modal with faction filter & search
- **Remove Unit**: X button on each unit card
- **Edit Unit**: Opens full editor modal
- **Clear All**: Resets entire versus calculator

## 📋 Complete Feature Checklist

### Layout & Navigation
- [x] Single /versus route
- [x] Old pages backed up (.backup extension)
- [x] Navigation menu updated (only one Versus link)
- [x] Split-screen layout (attacker | defender)
- [x] Responsive design
- [x] No tabs

### Unit Selection & Auto-Population
- [x] Unit picker modal with faction filter
- [x] Search functionality
- [x] Auto-populate models from composition table
- [x] Smart model name matching
- [x] Auto-equip weapons from loadout
- [x] Intelligent loadout parsing
- [x] Handle "OR" cases in composition
- [x] Handle singular/plural variations
- [x] Clean HTML from composition/loadout

### Summary Display
- [x] Compact unit cards
- [x] Model count by type
- [x] Weapon list with stats
- [x] Active modifiers badges
- [x] Defender stats (T/Sv/W)
- [x] Visual distinction (red for attacker, blue for defender)

### Unit Editing
- [x] Edit button opens modal
- [x] Models & Weapons tab
- [x] Add models from datasheet
- [x] Remove models
- [x] Add weapons from dropdown
- [x] Toggle weapon selection
- [x] Remove weapons
- [x] Unit Modifiers tab with full controls
- [x] Save/Cancel buttons

### Modifiers System
- [x] Global modifiers (attacker & defender)
- [x] Unit-level modifiers
- [x] Model-level modifiers
- [x] Weapon-level modifiers
- [x] Hit/Wound/Save modifiers
- [x] Reroll options (hits, 1s, wounds)
- [x] Feel No Pain
- [x] Cover
- [x] Damage reduction
- [x] Halve damage
- [x] All weapon abilities (sustained hits, lethal hits, etc.)

### Combat Calculation
- [x] Monte Carlo simulation (10k runs)
- [x] Multi-unit support
- [x] Multiple weapons per model
- [x] Weapon selection (on/off)
- [x] Damage distribution
- [x] Model destruction tracking
- [x] Results display

### User Experience
- [x] Swap button
- [x] Clear all button
- [x] Calculate button (with disabled state)
- [x] Inline modals
- [x] Summary-first approach
- [x] Edit-on-demand
- [x] Visual feedback
- [x] Tooltips on weapons

## 🎯 All Your Requirements = ✅ COMPLETE

1. ✅ Attacker and defender on ONE screen - **DONE**
2. ✅ Ability to swap the two - **DONE**  
3. ✅ Default view is summary (models, weapons, modifiers) - **DONE**
4. ✅ Edit opens modal - **DONE**
5. ✅ Auto-populate from composition table - **DONE**
6. ✅ Auto-populate weapons from loadout - **DONE**
7. ✅ Use wargear options for available weapons - **DONE**
8. ✅ One versus page (not multiple) - **DONE**

## 🚀 Ready to Use

Navigate to `/versus` and the new improved calculator is ready!

All old pages are backed up with `.backup` extension and won't interfere.
