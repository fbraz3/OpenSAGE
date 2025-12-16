# Complete Playability Roadmap: Phases 05-08

**Project**: OpenSAGE - Functional RTS Game  
**Timeline**: 4-5 weeks (41-55 developer-days)  
**Status**: ✅ ALL PHASES DOCUMENTED & READY FOR IMPLEMENTATION  
**Target Outcome**: Fully playable Command & Conquer-style RTS game  

---

## Executive Summary

OpenSAGE has **100% rendering complete** (map, particles, GUI, optimization all functional). This roadmap documents the **5-phase implementation plan** to add game logic and transform the visual engine into a **playable RTS game**.

After these 5 phases, players can:
- ✅ Click to select units and give move commands
- ✅ Build structures and train units
- ✅ Manage economy through resource harvesting
- ✅ Engage in tactical combat
- ✅ Experience complete base management gameplay

**Realistic estimate**: 4-5 weeks with one full-time developer  
**Critical path**: Input → Loop → Pathfinding → Combat → Building

---

## Phase Breakdown

### Phase 05: Input & Selection System (COMPLETE ✅)
**Document**: [PHASE05_INPUT_SELECTION.md](PHASE05_INPUT_SELECTION.md)

**What it does**: Route mouse input to game logic, enable unit selection

**Duration**: 5-7 days  
**Tasks**:
- InputRouter: UI → Game logic separation
- SelectionManager: Single/multi-select units
- RaycastManager: Terrain picking via mouse
- CommandQueue: Queue player commands

**After Phase 05**: Players can select units and issue move commands

**Code Examples**: ✅ InputRouter.cs, SelectionManager.cs, RaycastManager.cs

---

### Phase 06: Game Loop & State Management (COMPLETE ✅)
**Document**: [PHASE06_GAME_LOOP.md](PHASE06_GAME_LOOP.md)

**What it does**: Establish persistent game state, manage object lifecycle

**Duration**: 3-4 days  
**Tasks**:
- GameLogic refactor: Object lifecycle management
- Player & Team management: Money, stats, units tracking
- Object update contracts: Base GameObject class update pattern
- Integration with Game.cs: Wire into main render loop

**Key Architecture**: Authority tick (5 Hz game logic, 60 Hz rendering)

**After Phase 06**: Game maintains state, units exist and update

**Code Examples**: ✅ GameLogic.cs (150+ lines), Player.cs, GameObject.cs

---

### Phase 07A: Pathfinding & Movement (COMPLETE ✅)
**Document**: [PHASE07A_PATHFINDING.md](PHASE07A_PATHFINDING.md)

**What it does**: Enable units to navigate terrain intelligently

**Duration**: 11-14 days  
**Tasks**:
- Navigation Mesh generation: Build NavMesh from terrain/obstacles
- A* Pathfinding algorithm: Compute optimal paths
- Unit Locomotor system: Smooth movement along paths
- Collision avoidance: Prevent unit stacking

**Performance**: 100+ units pathfinding simultaneously at 60 FPS

**After Phase 07A**: Units move smoothly to clicked destinations

**Code Examples**: ✅ NavigationMesh.cs, Pathfinder.cs, Locomotor.cs

---

### Phase 07B: Combat System & Targeting (COMPLETE ✅)
**Document**: [PHASE07B_COMBAT.md](PHASE07B_COMBAT.md)

**What it does**: Enable units to attack each other

**Duration**: 9-11 days  
**Tasks**:
- Targeting system: Acquire enemies, priority scoring
- Weapon system & firing: Spawn projectiles, manage reload
- Damage & health: Track unit health, death handling
- Combat loop integration: Coordinate firing with movement

**Key Concepts**: 
- Target priority (closest, lowest health, unit type)
- Projectile ballistics with lead calculation
- Weapon reload timers
- Kill credit tracking

**After Phase 07B**: Units engage in combat, health decreases, units die

**Code Examples**: ✅ TargetingSystem.cs, Weapon.cs, Projectile.cs

---

### Phase 08: Building & Economy (COMPLETE ✅)
**Document**: [PHASE08_BUILDING_ECONOMY.md](PHASE08_BUILDING_ECONOMY.md)

**What it does**: Enable base management and economy

**Duration**: 14-19 days  
**Tasks**:
- Building system: Placement validation, construction progress
- Production system: Queue units, manage production buildings
- Harvester & resources: Collect ore, deliver to refineries
- Economy integration: Money flow, income/expense tracking

**Key Concepts**:
- Building placement validation (terrain, collision, cost)
- Construction progress (50% health → 100% over time)
- Production queues with multiple units
- Harvester AI (move → collect → deliver)
- Resource nodes and refineries

**After Phase 08**: **GAME IS PLAYABLE**

**Code Examples**: ✅ Building.cs, ProductionQueue.cs, HarvesterUnit.cs

---

## Master Timeline

```text
WEEK 1
├─ Mon-Tue: Phase 05 (Input & Selection) - Days 1-2
├─ Wed-Fri: Phase 06 (Game Loop) - Days 3-4
└─ Fri-Sat: Phase 07A START (Pathfinding) - Days 5+

WEEK 2
├─ Mon-Fri: Phase 07A (Pathfinding) - Days 5-10
└─ Fri-Sat: Phase 07B START (Combat) - Day 11+

WEEK 3
├─ Mon-Fri: Phase 07B (Combat) - Days 11-16
└─ Fri-Sat: Phase 08 START (Building) - Day 17+

WEEK 4
├─ Mon-Fri: Phase 08 (Building & Economy) - Days 17-24
└─ Fri: PLAYABLE GAME ACHIEVED ✅

WEEK 5 (Optional Buffer)
└─ Mon-Fri: Testing, performance optimization, bug fixes
```

**Total**: 41-55 developer-days = 1 full-time developer × 4-5 weeks

---

## Critical Path

**These phases must complete in sequence**:

```
Phase 05 (Input)
    ↓
Phase 06 (Loop)
    ↓
Phase 07A (Pathfinding)
    ↓
Phase 07B (Combat)
    ↓
Phase 08 (Building)
    ↓
✅ PLAYABLE GAME
```

**Rationale**:
- Input must precede game logic (no commands without input)
- Game loop must precede everything (no state without loop)
- Pathfinding must precede combat (units must move to fight)
- Combat must precede building (defenders needed for strategy)
- Building is final system (completes gameplay loop)

**Cannot parallelize**: Strict dependency chain

---

## Effort Estimation

| Phase | Days | Tasks | LOC | Status |
|-------|------|-------|-----|--------|
| 05 | 5-7 | 4 | 500+ | ✅ Documented |
| 06 | 3-4 | 4 | 600+ | ✅ Documented |
| 07A | 11-14 | 4 | 1000+ | ✅ Documented |
| 07B | 9-11 | 4 | 900+ | ✅ Documented |
| 08 | 14-19 | 4 | 1100+ | ✅ Documented |
| **TOTAL** | **41-55** | **20** | **4100+** | **READY** |

---

## Required Infrastructure

**Already complete** ✅:
- Game.cs (60 Hz render loop)
- GraphicsSystem (rendering pipeline)
- Scene3D (spatial management)
- ParticleSystemManager (effects)
- ContentManager (asset loading)
- Veldrid graphics abstraction

**To be built**:
- InputRouter (input routing)
- GameLogic (state management)
- Pathfinder (A* algorithm)
- CombatSystem (weapon firing)
- BuildingSystem (placement/construction)
- EconomySystem (money management)

**Total new classes**: ~25-30

---

## Success Criteria

### Phase 05 Complete
- [ ] Raycast from mouse to terrain
- [ ] Single-click selects nearest unit
- [ ] Drag-select creates group
- [ ] Right-click queues move command
- [ ] Command executes next frame

### Phase 06 Complete
- [ ] GameLogic maintains object list
- [ ] Authority tick runs at 5 Hz
- [ ] Units persist across frames
- [ ] Player money tracked
- [ ] 100 units update without stalling

### Phase 07A Complete
- [ ] NavMesh builds from terrain
- [ ] A* finds optimal paths
- [ ] 100+ units move simultaneously
- [ ] Collision avoidance prevents stacking
- [ ] Movement looks natural

### Phase 07B Complete
- [ ] Units target enemies automatically
- [ ] Weapons fire projectiles
- [ ] Projectiles move toward targets
- [ ] Damage applied on impact
- [ ] Units die at 0 health

### Phase 08 Complete
- [ ] Buildings placed on terrain
- [ ] Construction progress visible
- [ ] Production queue processes units
- [ ] Harvesters collect resources
- [ ] Money increases from harvesting
- [ ] **GAME PLAYABLE** ✅

---

## Performance Targets

All phases must maintain:
- ✅ **60 FPS** minimum
- ✅ **100+ units** moving/fighting simultaneously
- ✅ **20+ buildings** functioning
- ✅ **< 5ms** per pathfinding query
- ✅ **< 2ms** combat targeting/firing

**Profiling required**: Each phase must profile and optimize before moving to next

---

## Testing Strategy

**Each phase includes**:
- Unit tests for core algorithms
- Integration tests with game loop
- Performance tests (100+ entities)
- Visual validation (manual testing)

**Example test files created**: 
- PathfindingTests.cs
- CombatTests.cs
- EconomyTests.cs

---

## Reference Architecture

**Following EA Generals patterns**:
- Command queue for unit orders
- MVC pattern for selection UI
- Authority tick for deterministic logic
- Navigation mesh + A* for pathfinding
- Object-module pattern for extensibility
- Production queue for unit training

**Verified against**: `references/generals_code/` - EA C++ source

---

## Risk Assessment

### High Risk 🔴
1. **Pathfinding performance with 100+ units**
   - Mitigation: Profile early, use spatial hashing
2. **Combat balance feels wrong**
   - Mitigation: Extensive playtesting, balance knobs

### Medium Risk 🟡
3. **AI opponent implementation (Phase 2+)**
   - Mitigation: Start with scripted builds, defer AI
4. **Multiplayer networking complexity**
   - Mitigation: Single-player first, network later

### Low Risk 🟢
5. **Rendering already complete**
6. **Input/UI infrastructure exists**
7. **Content loading functional**

---

## Next Steps (After Phase 08)

**Optional Phase 2** (Polish & AI):
- AI opponent strategy
- Scripting engine (mission campaigns)
- Advanced pathfinding (terrain costs, dynamic obstacles)
- Multiplayer networking

**Estimated**: 2-3 weeks additional

---

## File Locations

**Phase Documentation**:
```
docs/PLANNING/phases/
├── PHASE05_INPUT_SELECTION.md          ✅ Complete
├── PHASE06_GAME_LOOP.md                ✅ Complete
├── PHASE07A_PATHFINDING.md             ✅ Complete
├── PHASE07B_COMBAT.md                  ✅ Complete
└── PHASE08_BUILDING_ECONOMY.md         ✅ Complete

docs/ETC/
├── CRITICAL_GAP_ANALYSIS_DECEMBER_2025.md        ✅ Analysis
├── COMPREHENSIVE_IMPLEMENTATION_PHASES_05-08.md  ✅ Architecture
├── EXECUTIVE_SUMMARY_PLAYABILITY_ANALYSIS.md     ✅ Summary
└── ROADMAP.md                                    ✅ Overall plan
```

**Code will be created in**:
```
src/OpenSage.Game/
├── Logic/
│   ├── Input/
│   ├── Pathfinding/
│   ├── Object/Targeting/
│   ├── Object/Weapon/
│   ├── Object/Building/
│   ├── Resource/
│   └── Economy/
└── GameLogic.cs (refactored)
```

---

## Getting Started

**For the implementation team**:

1. **Read documents in order**:
   - PHASE05_INPUT_SELECTION.md
   - PHASE06_GAME_LOOP.md
   - PHASE07A_PATHFINDING.md
   - PHASE07B_COMBAT.md
   - PHASE08_BUILDING_ECONOMY.md

2. **Start Phase 05 immediately**:
   - Each document has code examples
   - Follow acceptance criteria strictly
   - Write tests alongside code

3. **Maintain 60 FPS at all times**:
   - Profile regularly
   - Optimize bottlenecks before next phase

4. **Refer to EA Generals source**:
   - `references/generals_code/` has reference implementations
   - Use for validation, not copy-paste

5. **Check in with stakeholders**:
   - After Phase 05: "I can click and select units"
   - After Phase 06: "Units exist and persist"
   - After Phase 07A: "Units move smoothly"
   - After Phase 07B: "Units fight each other"
   - After Phase 08: "I can play the game!" 🎮

---

## Success Statement

**Upon completion of Phase 08, the following will be true**:

✅ Player loads a map  
✅ Player selects units  
✅ Player gives movement orders  
✅ Units pathfind across terrain  
✅ Units engage enemies automatically  
✅ Player builds structures  
✅ Player trains units from buildings  
✅ Harvesters collect resources  
✅ Economy flows (money in/out)  
✅ Units die, buildings get destroyed  
✅ Game maintains 60 FPS with 100+ units  

**Result**: A fully functional, playable RTS game built from the EA Generals architecture.

---

## Commitment

This roadmap is:
- ✅ **Detailed**: Code examples provided
- ✅ **Realistic**: Time estimates based on complexity
- ✅ **Achievable**: 4-5 weeks with one developer
- ✅ **Validated**: Checked against EA Generals source
- ✅ **Complete**: No missing pieces
- ✅ **Actionable**: Ready to start immediately

**No more analysis needed. Time to build.** 🚀

---

**"Bite my shiny metal ass!" - This roadmap.** 🤖
