# EA Generals Tooltip System - Documentation Index

## Overview

Complete analysis of the EA Generals tooltip system extracted from the CnC_Generals_Zero_Hour reference code. Four comprehensive documents provide different levels of detail for understanding tooltip implementation.

---

## Documentation Files

### 1. **TOOLTIP_SEARCH_RESULTS_SUMMARY.md**
**Best for:** Getting a complete overview

Complete summary of all search results with code snippets organized by functionality:
- All tooltip patterns found (rendering, delay, callbacks, positioning, events)
- File examination list with specific lines
- Configuration reference
- Complete lifecycle explanation
- OpenSAGE implementation notes

**Sections:**
- Search Results Overview
- Files Examined (with line numbers)
- Configuration Reference
- Complete Lifecycle Diagram
- Key Implementation Patterns
- OpenSAGE Notes

---

### 2. **EA_TOOLTIP_SYSTEM_ANALYSIS.md**
**Best for:** Deep technical understanding

In-depth technical analysis with complete code implementations:
- Full tooltip rendering logic with animation
- Two-tier delay mechanism with timing calculations
- Callback architecture with integration points
- Positioning with edge detection
- Event system for show/hide
- Complete configuration system
- Data structures
- Integration points

**Sections:**
- Rendering & Display Logic (350+ lines code)
- Delay Mechanism (250+ lines code)
- Callback/Handler System (detailed flow)
- Positioning Logic (edge cases included)
- Event System (state machine)
- Configuration & Properties (INI + WND)
- Complete Lifecycle (8 phases)
- Data Structures (all members)
- Integration Points (3 systems)
- File Reference Map

**Best For:** Implementation in OpenSAGE

---

### 3. **TOOLTIP_QUICK_REFERENCE.md**
**Best for:** Quick lookup and examples

Practical reference with diagrams and code snippets:
- Architecture diagram
- State transitions
- Animation timing
- Window-level configuration examples
- Color system with defaults
- INI configuration keys
- Method reference
- Copy-paste snippets
- Testing checklist

**Sections:**
- System Architecture Diagram
- Delay System Flow Chart
- Window Configuration (3 methods)
- Rendering Coordinates
- Color System
- INI Keys Reference
- State Transitions
- Animation Timing
- Key Methods Reference
- Complete Example (Control Bar)
- Testing Checklist
- Copy-Paste Snippets

**Best For:** Practical implementation reference

---

### 4. **TOOLTIP_CODE_REFERENCES.md**
**Best for:** Finding specific code locations

Organized reference to exact line numbers in source:
- Mouse class tooltip members (table)
- Key functions with line numbers
- GameWindow callback integration
- GameWindowManager integration points
- Control Bar example
- WND file parsing
- Mouse input tracking
- Window instance data
- Animation frame calculation
- Edge wrapping logic
- Color house support
- Default colors
- INI configuration keys (table)
- Script fade suppression
- Clipping region rendering
- Cross-reference table

**Sections:**
- Mouse Class Members (with lines)
- Key Functions (with file:line)
- GameWindow Integration
- GameWindowManager Integration
- Control Bar Example
- WND Parsing
- Mouse Input
- Instance Data
- Calculations
- Color Support
- INI Keys (table)
- Cross-Reference Table

**Best For:** Navigating the source code

---

## File Locations in Reference Code

### Primary Source (Generals Version)

```
references/generals_code/Generals/Code/
├── GameEngine/
│   ├── Include/GameClient/
│   │   ├── Mouse.h                  ← Tooltip member declarations
│   │   ├── GameWindow.h             ← Callback type + methods
│   │   └── WinInstanceData.h        ← Per-window tooltip data
│   └── Source/GameClient/
│       ├── Input/Mouse.cpp          ← Core tooltip logic
│       ├── GUI/GameWindowManager.cpp ← Integration
│       ├── GUI/ControlBar/ControlBar.cpp ← Example
│       └── GUI/GameWindowManagerScript.cpp ← WND parsing
└── GameEngineDevice/
    └── Source/W3DDevice/GameClient/W3DMouse.cpp ← Rendering
```

### GeneralsMD Version

Similar structure under `references/generals_code/GeneralsMD/`

---

## Quick Navigation Guide

### I need to understand...

**How tooltips are rendered?**
→ See **EA_TOOLTIP_SYSTEM_ANALYSIS.md** Section 1 (drawTooltip)
→ Or **TOOLTIP_CODE_REFERENCES.md** "Draw Tooltip" section

**How delay works?**
→ See **TOOLTIP_QUICK_REFERENCE.md** "Delay System Flow"
→ Or **EA_TOOLTIP_SYSTEM_ANALYSIS.md** Section 2

**How to implement callbacks?**
→ See **TOOLTIP_QUICK_REFERENCE.md** "Window-Level Configuration"
→ Or **TOOLTIP_CODE_REFERENCES.md** "Control Bar Tooltip Example"

**Where to find specific code?**
→ See **TOOLTIP_CODE_REFERENCES.md** entire document

**How to configure in INI?**
→ See **TOOLTIP_QUICK_REFERENCE.md** "INI Configuration Keys"
→ Or **EA_TOOLTIP_SYSTEM_ANALYSIS.md** Section 6

**The complete system architecture?**
→ See **TOOLTIP_QUICK_REFERENCE.md** "System Architecture Diagram"
→ Or **TOOLTIP_SEARCH_RESULTS_SUMMARY.md** "Complete Lifecycle"

**How to test implementation?**
→ See **TOOLTIP_QUICK_REFERENCE.md** "Testing Checklist"

**Integration points in OpenSAGE?**
→ See **TOOLTIP_SEARCH_RESULTS_SUMMARY.md** "OpenSAGE Notes"
→ Or **EA_TOOLTIP_SYSTEM_ANALYSIS.md** Section 9

---

## Key Concepts Summary

### Core Components

1. **Mouse System** - Central tooltip management
   - Tracks cursor position and movement
   - Manages delay timer
   - Renders tooltips

2. **GameWindowManager** - Tooltip detection & callback invocation
   - Finds window under cursor
   - Detects tooltip content
   - Calls callbacks or sets static text

3. **GameWindow** - Per-window tooltip configuration
   - Callback function pointer
   - Static tooltip text
   - Delay override

4. **WinInstanceData** - Window instance tooltip data
   - Delay milliseconds
   - Tooltip text
   - Display properties

### Key Mechanisms

1. **Two-Tier Delay**
   - Global default: 50ms
   - Per-window override: WinInstanceData::m_tooltipDelay
   - Resets on ANY mouse movement

2. **Callback Chain**
   - Window has callback? → Call it
   - Otherwise → Use static text

3. **Animation**
   - Highlight sweep effect
   - Duration: m_tooltipFillTime (50ms default)
   - Position: (width * elapsed) / fillTime

4. **Edge Wrapping**
   - Right edge: Flips 20px + width left
   - Bottom edge: Flips height up
   - 4px safety margin

5. **Show/Hide Events**
   - Show: Delay met + valid content
   - Hide: Mouse moves, window changes, manual reset, fade

---

## Implementation Checklist for OpenSAGE

- [ ] Implement Mouse tooltip members (colors, fonts, delays, animation)
- [ ] Create delay tracking system (m_stillTime comparison)
- [ ] Implement drawTooltip() with positioning and animation
- [ ] Create tooltip callback system (GameWindow integration)
- [ ] Add GameWindowManager detection logic
- [ ] Parse WND file TOOLTIPDELAY entries
- [ ] Load INI configuration keys
- [ ] Support house colors (optional)
- [ ] Implement edge wrapping with boundary detection
- [ ] Create clipping regions for animation
- [ ] Add reset functionality (resetTooltipDelay)
- [ ] Integrate with rendering pipeline
- [ ] Test all state transitions
- [ ] Verify animation timing

---

## Code Statistics

| Metric | Value |
|--------|-------|
| Total tooltip-related lines in Mouse.cpp | ~600 |
| INI configuration keys | 15 |
| Main rendering function | drawTooltip (69 lines) |
| Delay checking logic | ~30 lines |
| Color configuration options | 5 |
| Window layers supported | 3 (ABOVE, NORMAL, BELOW) |
| Animation highlight width | 15px |
| Default delay | 50ms |
| Default animation duration | 50ms |
| Positioning offset | 20px |
| Edge safety margin | 4px |

---

## Document Features

### Comprehensive Coverage
- ✓ All 5 requested topics (rendering, delay, callbacks, positioning, events)
- ✓ Complete code snippets
- ✓ Line-by-line references
- ✓ Default values and configurations
- ✓ State machine diagrams
- ✓ Integration points

### Multiple Formats
- ✓ Technical deep-dive for developers
- ✓ Quick reference for lookup
- ✓ Code reference for navigation
- ✓ Summary with examples

### Ready for Implementation
- ✓ Copy-paste code snippets
- ✓ Exact file and line locations
- ✓ Architecture diagrams
- ✓ Testing checklist
- ✓ Implementation notes

---

## Related Documentation

See also:
- [DEV_BLOG/2024-12-DIARY.md](../DEV_BLOG/2024-12-DIARY.md) - Development notes
- [developer-guide.md](../developer-guide.md) - General development guide
- [coding-style.md](../coding-style.md) - Code style guidelines

---

## Usage Recommendations

### For Initial Learning
1. Start with **TOOLTIP_QUICK_REFERENCE.md** (architecture, state flow)
2. Read **TOOLTIP_SEARCH_RESULTS_SUMMARY.md** (overview, lifecycle)
3. Reference **TOOLTIP_CODE_REFERENCES.md** (find specific code)

### For Implementation
1. Review **EA_TOOLTIP_SYSTEM_ANALYSIS.md** (technical details)
2. Use **TOOLTIP_CODE_REFERENCES.md** (locate exact code)
3. Check **TOOLTIP_QUICK_REFERENCE.md** for examples

### For Debugging
1. Use **TOOLTIP_CODE_REFERENCES.md** (find function locations)
2. Check **EA_TOOLTIP_SYSTEM_ANALYSIS.md** Section 7 (complete lifecycle)
3. Reference **TOOLTIP_QUICK_REFERENCE.md** (state machine)

### For Testing
1. Use **TOOLTIP_QUICK_REFERENCE.md** (testing checklist)
2. Verify against **TOOLTIP_SEARCH_RESULTS_SUMMARY.md** (complete lifecycle)

---

**Last Updated:** December 15, 2024
**Source:** EA Generals Zero Hour reference code
**Documentation Version:** 1.0
**Status:** Complete - All 5 tooltip system topics covered

