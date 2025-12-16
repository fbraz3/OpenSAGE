# Gap Analysis Visual Reference

## Current State vs. Playable Game

```
TODAY (Beautiful Demo)               GOAL (Playable Skirmish)
┌─────────────────────────────┐     ┌─────────────────────────────┐
│ Game Rendering              │     │ Complete Game Systems       │
│  ✅ Maps & Terrain          │     │  ✅ Rendering (DONE)        │
│  ✅ Particles               │     │  ✅ Input → Orders          │
│  ✅ GUI/WND                 │     │  ✅ Game Loop Active        │
│  ✅ Audio (playback)        │     │  ✅ Unit Spawning           │
│  ✅ Models/Animation        │     │  ✅ Movement+Pathfinding    │
│  ✅ Performance Optimized   │     │  ✅ Combat System           │
│                             │     │  ✅ Building Construction   │
│ Game Logic (MISSING)        │     │  ✅ Economy/Resources       │
│  ❌ Input Handling          │     │                             │
│  ❌ Unit Management         │     │ Result:                     │
│  ❌ Movement System         │     │  → Full skirmish games      │
│  ❌ Combat                  │     │  → Unit tactics matter      │
│  ❌ Building                │     │  → Resource management      │
│  ❌ Economy                 │     │  → 15-20 min play sessions  │
│  ❌ Scripting               │     │                             │
│  ❌ AI Opponents            │     │ (4-5 weeks more work)       │
└─────────────────────────────┘     └─────────────────────────────┘

       Static Display                    Interactive Game
        Beautiful                         Playable
```

---

## Critical Path to Playability

```
PHASE 1A: Input & Game Loop
┌─────────────────────────────────────────────────────┐
│ Goal: Make game interactive                         │
│ Effort: 8-11 days                                   │
├─────────────────────────────────────────────────────┤
│ 1. Connect SelectionSystem → Orders                 │
│ 2. Implement GameLogic.Update() loop                │
│ 3. Add Order processing                             │
└─────────────────────────────────────────────────────┘
           ↓
PHASE 1B: Movement & Pathfinding
┌─────────────────────────────────────────────────────┐
│ Goal: Units move to clicked locations               │
│ Effort: 11-14 days                                  │
├─────────────────────────────────────────────────────┤
│ 1. Build terrain walkability map                    │
│ 2. Implement A* pathfinding                         │
│ 3. Add locomotion module                            │
│ 4. Connect orders → movement                        │
└─────────────────────────────────────────────────────┘
           ↓
PHASE 1C: Combat System
┌─────────────────────────────────────────────────────┐
│ Goal: Units can fight each other                    │
│ Effort: 9-11 days                                   │
├─────────────────────────────────────────────────────┤
│ 1. Implement weapon firing                          │
│ 2. Add target acquisition                           │
│ 3. Add projectiles                                  │
│ 4. Implement damage & death                         │
└─────────────────────────────────────────────────────┘
           ↓
PHASE 1D: Building System
┌─────────────────────────────────────────────────────┐
│ Goal: Player can build structures                   │
│ Effort: 8-11 days                                   │
├─────────────────────────────────────────────────────┤
│ 1. Implement construction orders                    │
│ 2. Add building time tracking                       │
│ 3. Add placement validation                         │
│ 4. Handle construction completion                   │
└─────────────────────────────────────────────────────┘
           ↓
PHASE 1E: Economy (Basic)
┌─────────────────────────────────────────────────────┐
│ Goal: Resources drive gameplay                      │
│ Effort: 6-8 days                                    │
├─────────────────────────────────────────────────────┤
│ 1. Implement supply harvesting                      │
│ 2. Add production cost deduction                    │
│ 3. Add refunds on cancellation                      │
└─────────────────────────────────────────────────────┘
           ↓
✅ MINIMUM PLAYABLE GAME (4-5 weeks total)
   Players can play skirmish matches!
```

---

## Gap Priority Map

```
                    IMPACT ON PLAYABILITY
                    ↑ HIGH
                    │
      Combat ●      │  Building ●       Economy ●
                    │
      Movement ●    │  Input ●
                    │
      GameLoop ●    │
                    │
      Pathfinding ● │
                    │ LOW
                    └────────────────────────→ IMPLEMENTATION DIFFICULTY
                           LOW      MEDIUM      HIGH

Legend:
● CRITICAL (must have for playable game)
● HIGH (core gameplay)
● MEDIUM (nice to have soon)
● LOW (can wait)

CRITICAL PATH (dependencies):
Input → GameLoop → [Movement+Pathfinding] → [Combat]
                 ↓
              Building
                 ↓
              Economy

```

---

## System Dependency Graph

```
┌─────────────────────────────────────────────────────────────┐
│                    Input System                             │
│ (Player clicks, selects units, gives orders)               │
│                                                             │
│ Dependencies: Selection System ✅                          │
│ Status: 🟡 40% (needs connection to orders)                │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ↓
        ┌────────────────────────┐
        │  Order System (NEW)    │
        │ Move, Attack, Build    │
        └────────┬───────────────┘
                 │
    ┌────────────┼────────────┐
    ↓            ↓            ↓
┌─────────┐  ┌──────────┐  ┌────────────┐
│Movement │  │ Combat   │  │ Building   │
│ System  │  │ System   │  │ System     │
│ 🔴0%    │  │ 🟡25%    │  │ 🟡20%      │
└─────────┘  └──────────┘  └────────────┘
    │            │              │
    ├─→ Pathfinding ◄─┴──────────┘
    │     System
    │     🔴 0%
    │
    ├─→ GameLoop (active tick)
    │     🔴 5%
    │
    ├─→ Economy System
    │     🟡 15%
    │
    └─→ Object Manager
          🟡 40%

All systems feed back to:
└─→ GameLogic (central coordinator)
    └─→ Renders via existing Graphics Pipeline ✅
```

---

## Implementation Checklist (Priority Order)

### PHASE 1A: Input & Game Loop (8-11 days)

```
☐ Week 1 Day 1-2: Input System Connection
  ☐ Create OrderGeneratorSystem class
  ☐ Connect SelectionSystem → OrderGeneratorSystem
  ☐ Implement Order data structure
  ☐ Add order queue to GameObject
  ☐ Test: Click → Select feedback

☐ Week 1 Day 3-4: Game Loop Implementation
  ☐ Extend GameLogic.Update()
  ☐ Create ObjectManager
  ☐ Implement module update loop
  ☐ Add object sleepy scheduler
  ☐ Test: Objects receive updates

☐ Week 1 Day 5: Right-Click Move Orders
  ☐ Connect SelectionSystem → Move Order generation
  ☐ Test: Right-click generates order
  ☐ Test: Selected units highlighted

☐ Week 2 Day 1: Death/Removal System
  ☐ Implement death callbacks
  ☐ Add object removal from lists
  ☐ Test: Health persists frame-to-frame
```

### PHASE 1B: Movement & Pathfinding (11-14 days)

```
☐ Week 2 Day 2-5: Pathfinding System
  ☐ Build terrain walkability map from HeightMap
  ☐ Implement A* pathfinding algorithm
  ☐ Add path caching for performance
  ☐ Handle dynamic obstacles
  ☐ Test: Path calculation < 5ms

☐ Week 3 Day 1-3: Locomotion Module
  ☐ Create LocomotionModule class
  ☐ Implement path-following
  ☐ Add destination detection
  ☐ Add basic collision avoidance
  ☐ Test: Units move along paths
  ☐ Test: Arrive at destination correctly

☐ Week 3 Day 4: Integration & Polish
  ☐ Connect Move Orders → LocomotionModule
  ☐ Test: Right-click → unit movement
  ☐ Visual debugging (draw paths)
```

### PHASE 1C: Combat System (9-11 days)

```
☐ Week 3 Day 5 - Week 4 Day 2: Weapon System
  ☐ Create WeaponModule class
  ☐ Implement weapon firing cycle
  ☐ Add target acquisition
  ☐ Implement line-of-sight check
  ☐ Test: Units can acquire targets

☐ Week 4 Day 3: Projectiles & Damage
  ☐ Create ProjectileManager
  ☐ Implement projectile physics
  ☐ Add projectile-to-target collision
  ☐ Connect projectile → damage application
  ☐ Test: Projectiles hit and deal damage

☐ Week 4 Day 4: Death & Destruction
  ☐ Implement body.AttemptDamage() properly
  ☐ Add death callbacks
  ☐ Show corpse/destruction visuals
  ☐ Remove dead units
  ☐ Test: Units die and disappear
```

### PHASE 1D: Building System (8-11 days)

```
☐ Week 4 Day 5 - Week 5 Day 2: Construction System
  ☐ Extend ConstructBuildingOrderGenerator
  ☐ Create ConstructionSystem
  ☐ Add construction time tracking
  ☐ Implement builder assignment
  ☐ Add construction completion
  ☐ Test: Buildings construct over time

☐ Week 5 Day 3: Placement & Validation
  ☐ Implement BuildingPlacementValidator
  ☐ Validate terrain slope/flatness
  ☐ Check for obstacles
  ☐ Show ghost building
  ☐ Show placement feedback (valid/invalid)
  ☐ Test: Reject invalid placements

☐ Week 5 Day 4: Integration
  ☐ Connect UI placement → construction
  ☐ Test: Full building placement workflow
```

### PHASE 1E: Economy (6-8 days)

```
☐ Week 5 Day 5 - Week 6 Day 2: Supply Collection
  ☐ Create HarvesterModule
  ☐ Implement supply gathering behavior
  ☐ Add ore patch tracking
  ☐ Add return-to-base logic
  ☐ Test: Harvesters collect ore

☐ Week 6 Day 3: Production Costs
  ☐ Connect unit creation → money deduction
  ☐ Connect building placement → money deduction
  ☐ Add insufficient funds check
  ☐ Add cancellation refunds
  ☐ Test: Can't build if no money

☐ Week 6 Day 4-5: Polish & Debugging
  ☐ Show cost feedback in UI
  ☐ Test full economy loop
  ☐ Performance optimization
  ☐ Bug fixes
```

---

## Risk Matrix

```
Risk                    Probability  Impact  Mitigation
─────────────────────────────────────────────────────────────
Pathfinding too slow    MEDIUM       HIGH    Profile early,
                                             implement
                                             incremental
                                             search
─────────────────────────────────────────────────────────────
Combat balance broken   MEDIUM       MEDIUM  Extensive
                                             playtesting,
                                             balance
                                             iteration
─────────────────────────────────────────────────────────────
Network/Multiplayer     MEDIUM       HIGH    Defer to
  support needed early                       Phase 2
─────────────────────────────────────────────────────────────
Audio system             LOW          LOW     Already
  integration issues                         working,
                                             defer
─────────────────────────────────────────────────────────────
Memory/Performance      MEDIUM       MEDIUM  Performance
  with 100+ units                           profiler
                                             ready
─────────────────────────────────────────────────────────────
Scripting/Campaign      LOW           HIGH   Not required
  pressure (can defer)                      for MVP
─────────────────────────────────────────────────────────────
```

---

## Success Metrics

### Minimum Playable Game (4-5 weeks)

- ✅ **Functional**: Can complete 15-20 minute skirmish match
- ✅ **Interactive**: All major player actions work
- ✅ **Balanced**: Game feels fair and fun to both players
- ✅ **Stable**: No crashes or major bugs
- ✅ **Performant**: 60 FPS on target hardware

### Performance Targets

- **Pathfinding**: < 5ms for typical map
- **GameLogic update**: < 16.67ms (60 FPS)
- **Unit count**: Support 100+ units smoothly
- **Building count**: Support 50+ buildings
- **Memory**: < 500 MB for typical game

### Coverage Targets

- **Unit tests**: > 80% of logic systems
- **Gameplay tests**: All major systems functional
- **Manual QA**: 2-3 hours testing per build

---

## Next Steps

1. **This week**: Create OrderGeneratorSystem and connect input
2. **Next week**: Implement GameLoop and Pathfinding skeleton
3. **Week 3**: Complete pathfinding + start combat
4. **Week 4**: Building system + economy basics
5. **Week 5**: Polish, testing, balance
6. **Week 6**: Deployment-ready MVP

**Target**: Playable skirmish game by end of Week 5
