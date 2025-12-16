# DEEP ANALYSIS: Critical Gaps Preventing OpenSAGE Playability

**Analysis Date**: December 16, 2025  
**Status**: Complete Gap Analysis & Phased Implementation Plan  
**Target**: Identify all critical blockers to getting from "beautiful static demo" → "actually playable game"

---

## Executive Summary

OpenSAGE has completed **100% of rendering systems** (4 phases, 100 commits):
- ✅ Maps, terrain, water, roads - COMPLETE
- ✅ Particle systems - COMPLETE  
- ✅ GUI/WND rendering - COMPLETE
- ✅ Performance optimization - COMPLETE

**BUT** game logic systems haven't started. The game is a **beautiful but completely non-interactive demo**. You can view maps and UI but cannot:
- Click and select units
- Give orders (move, attack)
- Build structures
- Manage resources/money
- Fight combat
- Play skirmish/multiplayer
- Save/load games

**Verdict**: 4-6 weeks of focused effort needed to reach "minimum playable skirmish game" status.

---

## Current Architecture State

### What EXISTS (Rendering Complete ✅)
```
Game.cs → 60 Hz render loop ✅
  ├─ Terrain rendering ✅
  ├─ Particle systems ✅
  ├─ GUI/WND ✅
  ├─ Animation & models ✅
  └─ Performance profiling ✅
```

### What's MISSING (Logic Not Started ❌)
```
GameLogic → 5 Hz tick (STUB ONLY)
  ├─ Input handling ❌ (MINIMAL)
  ├─ Unit/building selection ❌ (PARTIAL - code exists but not connected)
  ├─ Orders/movement ❌ (NO IMPLEMENTATION)
  ├─ Combat system ❌ (DAMAGE TYPES ONLY)
  ├─ Resource/economy ❌ (BANK ACCOUNT EXISTS, NOT INTEGRATED)
  ├─ Scripting engine ❌ (DOCS ONLY, NOT IMPLEMENTED)
  ├─ AI system ❌ (SKELETON ONLY)
  └─ Pathfinding ❌ (NO IMPLEMENTATION)
```

---

## Part 1: CRITICAL GAPS BY CATEGORY

### 1️⃣ INPUT SYSTEM - ⚠️ PARTIALLY BROKEN

**Current State**: Input handlers exist but don't connect to game logic  
**Status**: 🟡 40% done

#### What Works ✅
- Mouse/keyboard input capture in `InputSystem`
- Camera panning with right-mouse (partially)
- WND GUI input handling
- Selection box detection (code exists in `SelectionSystem`)

#### What Doesn't Work ❌
- **Unit selection on map**: Selection system exists but doesn't persist selection to orders
- **Right-click move orders**: No order generation
- **Hotkeys**: Not connected to order system  
- **Context-sensitive cursors**: Hardcoded, not dynamic based on selection

#### Implementation Gaps
```csharp
// EXISTS: SelectionSystem tracks _selectedUnits
// EXISTS: SelectionMessageHandler in Logic/
// MISSING: OrderGeneratorSystem that converts selections → orders
// MISSING: Input command translation to game logic
```

**Impact**: Player cannot interact with game at all.

**Fix Effort**: 3-4 days
- Connect SelectionSystem → OrderGeneratorSystem
- Implement right-click move order
- Implement control-group selection
- Add order preview (ghost building placement, move path)

---

### 2️⃣ GAME STATE MANAGEMENT - ❌ NOT STARTED

**Current State**: Object creation works, but no persistent state tracking  
**Status**: 🔴 5% done (only basic structures)

#### What Works ✅
- `GameObject` base class with modules
- `Player` class with name, color, allies/enemies
- `BankAccount` for money tracking
- Object ID allocation

#### What Doesn't Work ❌
- **No game simulation loop**: GameLogic.Update() is mostly empty
- **No object lifecycle**: Units/buildings don't age, heal, repair
- **No persistence**: No save/load (StatePersister exists but unused for live game)
- **No player income**: No resource gathering or selling
- **No building queue**: Construction pipeline doesn't exist
- **No diplomacy**: Ally/enemy relations are hardcoded

#### Implementation Gaps
```csharp
// Current GameLogic structure
public sealed class GameLogic : DisposableBase
{
    public List<GameObject> Objects { get; }  // ✅ EXISTS
    public void Update(in TimeInterval gameTime) { } // ❌ EMPTY!
    
    // MISSING:
    // - ObjectUpdateSystem (tick all objects)
    // - ProductionSystem (queued building/units)
    // - IncomeSystem (collect resources)
    // - DiplomacySystem (track alliances)
    // - DeathSystem (handle unit destruction)
}
```

**Impact**: Game has no state changes. Nothing happens.

**Fix Effort**: 5-7 days
- Implement object update tick with sleepy scheduler
- Implement production system (build queue, time-based completion)
- Implement income/resource flow
- Implement death/destruction callbacks

---

### 3️⃣ UNIT & BUILDING MANAGEMENT - 🟡 PARTIAL

**Current State**: Object templates exist, but instantiation/management incomplete  
**Status**: 🟡 30% done

#### What Works ✅
- `ObjectTemplate` loading from INI files
- `Building`, `Unit` hierarchy
- Module-based architecture (Body, Draw, Behavior modules)
- Health/damage system types defined

#### What Doesn't Work ❌
- **No unit spawning**: Can't create units at game start
- **No building placement**: Construction order doesn't create building
- **No factory production**: Unit production queues don't work
- **No unit movement**: No locomotion orders
- **No unit selection persistence**: Selection lost on input

#### Implementation Gaps
```csharp
// Order types exist but not executed:
public enum OrderType
{
    Move,           // ❌ No locomotion module
    Attack,         // ❌ Weapon firing not integrated
    BuildBuilding,  // ❌ No construction behavior
    Repair,         // ❌ No repair mechanics
    Garrison,       // ❌ Not implemented
    Produce,        // ❌ No production queue
}

// Module system exists but not all connected:
// ✅ Draw module (visual)
// ✅ Body module (health/damage)
// 🟡 Behavior modules (some incomplete)
// ❌ Locomotion (no movement)
// ❌ Weapon (no firing)
// ❌ Production (no build queues)
```

**Impact**: Units exist as visual props but cannot act.

**Fix Effort**: 10-12 days
- Implement unit spawning at map start
- Implement factory/production queue system
- Implement basic locomotion (pathfinding → movement)
- Implement building placement order processing
- Implement unit group behavior

---

### 4️⃣ COMBAT SYSTEM - 🟡 MOSTLY DEFINED

**Current State**: Damage types/death types exist but no active combat  
**Status**: 🟡 25% done

#### What Works ✅
- Damage type enums (FLAME, PIERCING, EXPLOSIVES, etc.)
- Death type enums (NORMAL, BURNED, EXPLODED, etc.)
- Health/armor system defined (`ActiveBody`, `HighlanderBody`, `UndeadBody`)
- Body damage states (PRISTINE, DAMAGED, REALLYDAMAGED, RUBBLE)
- Weapon template system (mostly)

#### What Doesn't Work ❌
- **No weapon firing**: Weapons don't shoot
- **No target acquisition**: Units don't acquire targets
- **No damage application**: No combat damage resolution
- **No area effects**: No splash damage from explosions
- **No unit deaths**: Units don't die from combat
- **No kill scoring**: No bounty/experience system

#### Implementation Gaps
```csharp
// Weapon system exists but is inert:
public sealed class Weapon : GameObject
{
    public void Fire(GameObject target) { } // ❌ NOT IMPLEMENTED
    
    // MISSING:
    // - Projectile creation/movement
    // - Target validation (range, line-of-sight, friendliness)
    // - Damage calculation (armor, damage types, resistance)
    // - Area effect radius calculation
    // - Terrain obstruction checking
}

// Combat loop doesn't exist:
// ❌ No firing cycle for units with weapons
// ❌ No target priority system
// ❌ No line-of-sight checking
// ❌ No collision detection for projectiles
```

**Impact**: Combat doesn't exist - units just stand around.

**Fix Effort**: 8-10 days
- Implement weapon firing cycle
- Implement target acquisition system
- Implement line-of-sight checking
- Implement damage resolution
- Implement unit death/corpse mechanics

---

### 5️⃣ RESOURCE/ECONOMY SYSTEM - 🟡 SKELETON

**Current State**: Money tracking exists, no flow mechanics  
**Status**: 🟡 15% done

#### What Works ✅
- `BankAccount` class (deposit, withdraw, money count)
- `Player.BankAccount` property
- `BuildListInfo` structure for base buildings
- Resource types defined in INI

#### What Doesn't Work ❌
- **No supply collection**: Harvesters don't gather resources
- **No supply delivery**: Collected resources don't go to player
- **No income rate**: No passive income from buildings
- **No unit cost deduction**: Spawning units doesn't cost money
- **No building refund**: Selling buildings doesn't refund cost
- **No tech tree integration**: Can't purchase upgrades

#### Implementation Gaps
```csharp
// Economy system doesn't exist:
public sealed class EconomySystem : GameSystem
{
    // MISSING:
    // - Supply deposited/withdrawn tracking
    // - Harvester assignment logic
    // - Ore/crystal collection mechanics
    // - Delivery to supply center
    // - Unit production cost deduction
    // - Building construction cost deduction
    // - Upgrade purchase logic
}

// Supply gathering pipeline missing:
// Harvester → [Travel to ore] → [Collect] → [Return to depot] → [Deposit]
// ❌ NO IMPLEMENTATION
```

**Impact**: Players have infinite money, can't play economy game.

**Fix Effort**: 5-7 days
- Implement supply collection (harvester fill logic)
- Implement supply delivery (return-to-base)
- Implement production cost deduction
- Implement building cost integration
- Implement tech/upgrade purchasing

---

### 6️⃣ BUILDING SYSTEM - 🟡 PARTIAL

**Current State**: Building templates loaded, but construction incomplete  
**Status**: 🟡 20% done

#### What Works ✅
- `BuildListInfo` for map buildings
- Building template loading
- Building placement validation (terrain, terrain slope)
- Base definition via BuildListInfo

#### What Doesn't Work ❌
- **No dynamic building placement**: Player can't place new buildings
- **No construction progress**: Buildings don't build over time
- **No construction cancellation**: Can't cancel builds
- **No building repair**: Damaged buildings don't repair
- **No building selling**: Can't demolish for refund
- **No power system**: Buildings don't draw/provide power

#### Implementation Gaps
```csharp
// Construction system doesn't exist:
public sealed class ConstructionSystem : GameSystem
{
    // MISSING:
    // - Active construction list
    // - Builder assignment
    // - Construction progress tracking
    // - Construction time calculation
    // - Completion callbacks (unit spawning)
    // - Cancellation handling
}

// Order pipeline for building:
// Player places building order → Validate position → Deduct cost
//   → Queue construction → Builder arrives → Progress over time
//   → Complete → Building placed
// ❌ MOST STEPS MISSING
```

**Impact**: Player builds 1 map per game, can't expand.

**Fix Effort**: 6-8 days
- Implement construction order validation
- Implement construction time tracking
- Implement construction progress visuals
- Implement completion callbacks
- Implement cancellation with refund

---

### 7️⃣ AI & SKIRMISH - 🔴 NOT STARTED

**Current State**: Skeleton AI system exists, no actual AI logic  
**Status**: 🔴 5% done

#### What Works ✅
- `AIPlayer` class hierarchy
- `Team` system for grouping units
- `TeamPrototype` definitions
- `AISkirmishPlayer` skeleton

#### What Doesn't Work ❌
- **No decision-making**: AI doesn't choose actions
- **No base building**: AI doesn't place structures
- **No unit recruitment**: AI doesn't build armies
- **No attacking**: AI doesn't engage enemies
- **No resource management**: AI doesn't worry about money
- **No skirmish support**: Multiplayer AI completely missing

#### Implementation Gaps
```csharp
// AI system is empty:
public sealed class AIPlayer : Player
{
    public void Update(in TimeInterval gameTime) 
    { 
        // ❌ EMPTY - no AI logic
    }
    
    // MISSING:
    // - Base building strategy
    // - Unit recruitment decisions
    // - Attack planning
    // - Defense response
    // - Resource harvesting targets
    // - Tech tree progression
}

// Skirmish mode doesn't work:
// ❌ No AI opponent spawning
// ❌ No difficulty levels
// ❌ No AI personality settings
// ❌ No replay system
```

**Impact**: Single-player skirmish games impossible.

**Fix Effort**: 12-15 days (HIGH COMPLEXITY)
- Implement base building decision tree
- Implement unit recruitment logic
- Implement army combat tactics
- Implement resource gathering priority
- Implement difficulty scaling
- **DEFERRED to Phase 2** (not critical for first playable)

---

### 8️⃣ AUDIO SYSTEM - 🟡 PARTIAL

**Current State**: Audio playback exists but not integrated with gameplay  
**Status**: 🟡 50% done

#### What Works ✅
- `AudioSystem` class with streaming support
- Sound/music loading infrastructure
- Submixers for volume categories (SFX, Music, Voice)
- Platform audio abstraction

#### What Doesn't Work ❌
- **No unit sounds**: Units don't make sounds when moving/attacking
- **No ambient sounds**: Map ambient doesn't play
- **No UI feedback**: Buttons don't click
- **No voice acting**: No unit responses to orders
- **No music transitions**: Doesn't react to gameplay state
- **No 3D audio**: No spatial positioning

#### Implementation Gaps
```csharp
// Audio not connected to game events:
public sealed class AudioSystem : GameSystem
{
    // Works: PlaySound(soundName)
    // Missing: Integration with:
    // - Unit movement starts
    // - Weapon firing
    // - Building completion
    // - Unit death
    // - Combat state changes
    // - Game win/lose
}
```

**Impact**: Game is silent. Low priority for first playable.

**Fix Effort**: 3-4 days (LOW PRIORITY)
- **DEFERRED to Phase 2** - not critical for playability

---

### 9️⃣ SCRIPTING ENGINE - 🔴 NOT STARTED

**Current State**: Detailed design doc exists, no implementation  
**Status**: 🔴 0% done

#### What Works ❌
- NOTHING - design only

#### What Doesn't Work ❌
- **No script parsing**: `.scb` files not read
- **No condition evaluation**: Script conditions don't trigger
- **No action execution**: Script actions don't run
- **No mission support**: Missions can't be implemented
- **No campaign support**: Campaign progression impossible

#### Implementation Gaps
- See [PHASE04_SCRIPTING_ENGINE.md](../phases/PHASE04_SCRIPTING_ENGINE.md)
- Full implementation designed but not started

**Impact**: Campaign/missions impossible. Single player story blocked.

**Fix Effort**: 20+ days (VERY HIGH COMPLEXITY)
- **DEFERRED to Phase 2+** - not critical for first playable skirmish

---

### 🔟 PATHFINDING - ❌ NOT STARTED

**Current State**: No pathfinding system  
**Status**: 🔴 0% done

#### What Works ❌
- NOTHING

#### What Doesn't Work ❌
- **No path calculation**: A* algorithm not implemented
- **No terrain analysis**: Walkability map not built
- **No dynamic obstacles**: Moving units aren't considered
- **No team formations**: Units walk in single file
- **No collision avoidance**: Units overlap

#### Implementation Gaps
- No pathfinding module in movement system
- No terrain walkability analysis
- No dynamic obstacle detection

**Impact**: Without pathfinding, movement orders meaningless.

**Fix Effort**: 8-10 days (MEDIUM COMPLEXITY)
- **CRITICAL** for first playable (Phase 1B)

---

## Part 2: PRIORITY MATRIX

### CRITICAL BLOCKERS (Must fix for playable game)
| System | Status | Complexity | Effort | Impact |
|--------|--------|------------|--------|--------|
| Input System | 🟡 40% | LOW | 3-4d | 🔴 Can't interact |
| Game Loop | 🔴 5% | MEDIUM | 5-7d | 🔴 Nothing happens |
| Unit Spawning | 🟡 30% | MEDIUM | 2-3d | 🔴 No units on map |
| Unit Movement | ❌ 0% | HIGH | 8-10d | 🔴 Units stuck |
| Pathfinding | ❌ 0% | HIGH | 8-10d | 🔴 Movement impossible |
| Building System | 🟡 20% | MEDIUM | 6-8d | 🟠 Can't expand base |
| Combat System | 🟡 25% | HIGH | 8-10d | 🟠 Can't attack enemies |
| **SUBTOTAL** | | | **41-52 days** | |

### HIGH PRIORITY (Needed soon after)
| System | Status | Complexity | Effort | Impact |
|--------|--------|------------|--------|--------|
| Economy | 🟡 15% | MEDIUM | 5-7d | 🟡 Trivial with infinite money |
| AI Basic | 🔴 5% | VERY HIGH | 15-20d | 🟡 Can't play skirmish |
| Scripting | 🔴 0% | VERY HIGH | 20+ days | 🟡 No campaign |
| **SUBTOTAL** | | | **40-47 days** | |

### MEDIUM PRIORITY (Polish)
| System | Status | Complexity | Effort | Impact |
|--------|--------|------------|--------|--------|
| Audio | 🟡 50% | LOW | 3-4d | 🟢 Nice to have |
| Advanced AI | 🔴 0% | VERY HIGH | 15+ days | 🟢 Better opponents |
| UI Polish | 🟡 60% | LOW | 5-7d | 🟢 Better UX |
| **SUBTOTAL** | | | **23-31 days** | |

---

## Part 3: MINIMUM PLAYABLE GAME (MPG) REQUIREMENTS

### What Must Work for "Playable Skirmish"

✅ **Rendering** (DONE)
- Maps, terrain, water, roads, particles, GUI all render correctly

❌ **→ Input & Selection** (CRITICAL FIX #1)
- Click to select units on map
- Drag-select multiple units
- Right-click to move selected units
- Hotkeys for unit groups (1-9)

❌ **→ Game Loop & State** (CRITICAL FIX #2)
- GameLogic.Update() ticks all game objects
- Objects have health/state that persists
- Destroyed units/buildings are removed

❌ **→ Unit Spawning** (CRITICAL FIX #3)
- Map initial units spawn at game start
- Player 1 and Player 2 initialized
- Units appear on map with correct team colors

❌ **→ Unit Movement** (CRITICAL FIX #4 - includes pathfinding)
- Units move toward clicked location
- Pathfinding calculates valid paths
- Units navigate terrain obstacles
- Multiple units don't all take same path

❌ **→ Basic Combat** (CRITICAL FIX #5)
- Units with weapons can target enemies
- Weapons fire on target
- Damage is dealt and health decreases
- Units die when health = 0
- Dead units disappear

❌ **→ Building System** (CRITICAL FIX #6)
- Player can place buildings via UI command
- Building placement validates terrain
- Construction takes time
- Completed building appears on map
- Can upgrade/sell buildings

❌ **→ Economy (Basic)** (CRITICAL FIX #7)
- Harvesters collect resources
- Resources delivered to player account
- Unit/building production costs money
- Money is deducted when units/buildings created

**Subtotal for MPG**: **41-52 days**

### What Can Wait Until Phase 2+

- ⏳ Campaign/Missions (Scripting engine)
- ⏳ AI Opponents (AI system)
- ⏳ Multiplayer (Network code)
- ⏳ Advanced Audio
- ⏳ Replays
- ⏳ Advanced UI polish

---

## Part 4: PHASED IMPLEMENTATION PLAN

### PHASE 1A: Input & Game Loop Foundation (Week 1-2, ~10 days)

**Goal**: Make game interactive and have active state

#### PHASE01A_01: Input System Connection
```
Priority: CRITICAL
Effort: 3-4 days
Complexity: LOW

Tasks:
1. Connect SelectionSystem → GameLogic
2. Implement right-click move order generation
3. Add order queue to GameObject
4. Connect OrderGeneratorSystem to SelectionMessageHandler
5. Add visual feedback for selected units (highlight)

Files:
- src/OpenSage.Game/Logic/OrderGeneratorSystem.cs (NEW)
- src/OpenSage.Game/Logic/SelectionSystem.cs (MODIFY)
- src/OpenSage.Game/Logic/Orders/Order.cs (EXTEND)

Acceptance Criteria:
☐ Click units on map to select (visual feedback)
☐ Right-click moves selected units to location
☐ Multiple units can be selected
☐ Selection clears on map click with no units
```

#### PHASE01A_02: Game Loop & Object Update
```
Priority: CRITICAL
Effort: 5-7 days
Complexity: MEDIUM

Tasks:
1. Implement GameLogic.Update() with object iteration
2. Implement Module.Update() calls for all objects
3. Implement object sleepy scheduler
4. Add death/destruction callbacks
5. Integrate with existing performance profiler

Files:
- src/OpenSage.Game/Logic/GameLogic.cs (EXTEND)
- src/OpenSage.Game/Logic/Object/Modules/ModuleBase.cs (EXTEND)
- src/OpenSage.Game/Logic/GameObjectManager.cs (NEW)

Acceptance Criteria:
☐ GameLogic.Update() runs every frame
☐ Objects tick their modules
☐ Dead objects are removed from lists
☐ Health/status persists frame-to-frame
```

**Subtotal Phase 1A**: **8-11 days**

---

### PHASE 1B: Movement & Pathfinding (Week 2-3, ~10-12 days)

**Goal**: Units move to clicked locations

#### PHASE01B_01: Pathfinding System
```
Priority: CRITICAL
Effort: 8-10 days
Complexity: HIGH

Tasks:
1. Build terrain walkability map from terrain data
2. Implement A* pathfinding algorithm
3. Cache paths for performance
4. Handle dynamic obstacles
5. Integrate with unit movement orders

Files:
- src/OpenSage.Game/Logic/Pathfinding/PathfindingSystem.cs (NEW)
- src/OpenSage.Game/Logic/Pathfinding/WalkabilityMap.cs (NEW)
- src/OpenSage.Game/Logic/Pathfinding/AStarPathfinder.cs (NEW)

Acceptance Criteria:
☐ Pathfinding system builds walkability from terrain
☐ A* finds valid paths between any two points
☐ Path avoids terrain obstacles
☐ Performance: Path calculation < 5ms for typical maps
```

#### PHASE01B_02: Unit Locomotion Module
```
Priority: CRITICAL
Effort: 3-4 days
Complexity: MEDIUM

Tasks:
1. Implement LocomotionModule for unit movement
2. Connect Order.Move → LocomotionModule
3. Add path-following logic
4. Handle path completion callbacks
5. Add collision avoidance between units

Files:
- src/OpenSage.Game/Logic/Object/Modules/LocomotionModule.cs (NEW)
- src/OpenSage.Game/Logic/Object/Modules/LocomotionModuleData.cs (NEW)

Acceptance Criteria:
☐ Units move along calculated paths
☐ Units stop at destination
☐ Units avoid obstacles
☐ Units don't overlap with each other
```

**Subtotal Phase 1B**: **11-14 days**

---

### PHASE 1C: Combat System (Week 3-4, ~10-12 days)

**Goal**: Units can shoot and kill each other

#### PHASE01C_01: Weapon Firing System
```
Priority: CRITICAL
Effort: 5-6 days
Complexity: MEDIUM

Tasks:
1. Implement WeaponModule firing cycle
2. Implement target acquisition
3. Implement line-of-sight checking
4. Implement projectile creation
5. Add weapon reload/cooldown

Files:
- src/OpenSage.Game/Logic/Object/Modules/WeaponModule.cs (NEW)
- src/OpenSage.Game/Logic/Combat/WeaponFiringSystem.cs (NEW)
- src/OpenSage.Game/Logic/Combat/ProjectileManager.cs (NEW)

Acceptance Criteria:
☐ Units with weapons acquire targets
☐ Weapons fire on valid targets
☐ Projectiles travel toward target
☐ Firing has reload/cooldown
```

#### PHASE01C_02: Damage & Death System
```
Priority: CRITICAL
Effort: 4-5 days
Complexity: MEDIUM

Tasks:
1. Implement damage application (body.AttemptDamage)
2. Integrate damage types with armor
3. Implement death callbacks
4. Add corpse/destruction visuals
5. Remove dead units from game

Files:
- src/OpenSage.Game/Logic/Combat/DamageSystem.cs (NEW)
- src/OpenSage.Game/Logic/Object/Body/Body.cs (EXTEND)

Acceptance Criteria:
☐ Projectile hits deal damage
☐ Health decreases correctly
☐ Units die when health = 0
☐ Dead units disappear and leave corpse
```

**Subtotal Phase 1C**: **9-11 days**

---

### PHASE 1D: Building System (Week 4, ~7-9 days)

**Goal**: Player can place and build structures

#### PHASE01D_01: Construction System
```
Priority: CRITICAL
Effort: 6-8 days
Complexity: MEDIUM

Tasks:
1. Implement ConstructionOrderGenerator
2. Implement ConstructionSystem to track queued builds
3. Add construction time tracking
4. Add builder unit assignment
5. Handle construction completion

Files:
- src/OpenSage.Game/Logic/OrderGenerators/ConstructBuildingOrderGenerator.cs (EXTEND)
- src/OpenSage.Game/Logic/Construction/ConstructionSystem.cs (NEW)
- src/OpenSage.Game/Logic/Construction/ConstructionQueue.cs (NEW)

Acceptance Criteria:
☐ Player can place building via UI command
☐ Placement validates terrain
☐ Construction takes configurable time
☐ Building appears on map when complete
☐ Can cancel construction with partial refund
```

#### PHASE01D_02: Building Placement Validation
```
Priority: CRITICAL
Effort: 2-3 days
Complexity: LOW

Tasks:
1. Validate building footprint against terrain
2. Check terrain slope/flattness
3. Check for existing objects in way
4. Visual feedback for valid/invalid placement
5. Show ghost building during placement

Files:
- src/OpenSage.Game/Logic/Building/BuildingPlacementValidator.cs (NEW)

Acceptance Criteria:
☐ Buildings only place on valid terrain
☐ Rejects placement on slopes/rough terrain
☐ Visual feedback shows placement validity
☐ Ghost building shows before placement
```

**Subtotal Phase 1D**: **8-11 days**

---

### PHASE 1E: Economy & Resources (Week 4-5, ~6-8 days)

**Goal**: Resources flow and drive economy

#### PHASE01E_01: Supply Collection
```
Priority: CRITICAL (for gameplay feel)
Effort: 4-5 days
Complexity: MEDIUM

Tasks:
1. Implement HarvesterModule for supply gathering
2. Add supply deposit/return logic
3. Track ore/crystal supplies on map
4. Handle supply depletion
5. Add harvester idle behavior when no supply

Files:
- src/OpenSage.Game/Logic/Object/Modules/HarvesterModule.cs (NEW)
- src/OpenSage.Game/Logic/Resources/SupplySystem.cs (NEW)

Acceptance Criteria:
☐ Harvesters move to ore patches
☐ Harvest ore over time
☐ Return to supply center with cargo
☐ Deposit cargo and gain money
☐ Ore patches deplete and eventually disappear
```

#### PHASE01E_02: Production Cost Integration
```
Priority: CRITICAL
Effort: 2-3 days
Complexity: LOW

Tasks:
1. Deduct unit cost from player money on creation
2. Deduct building cost from player money on placement
3. Handle insufficient funds (cancel order)
4. Add refund on cancellation
5. Show cost feedback in UI

Files:
- src/OpenSage.Game/Logic/Object/ObjectFactory.cs (MODIFY)
- src/OpenSage.Game/Logic/Construction/ConstructionSystem.cs (MODIFY)

Acceptance Criteria:
☐ Creating units costs money
☐ Placing buildings costs money
☐ Can't create if insufficient funds
☐ Canceling refunds partial cost
```

**Subtotal Phase 1E**: **6-8 days**

---

## Summary: Estimated Timeline to "Minimum Playable Game"

### Phase 1: MVP Skirmish Game (4-5 weeks)

| Phase | Component | Effort | Status |
|-------|-----------|--------|--------|
| 1A | Input & Game Loop | 8-11d | Week 1-2 |
| 1B | Movement & Pathfinding | 11-14d | Week 2-3 |
| 1C | Combat System | 9-11d | Week 3-4 |
| 1D | Building System | 8-11d | Week 4 |
| 1E | Economy (Basic) | 6-8d | Week 4-5 |
| | **TOTAL MVP** | **42-55 days** | **4-5 weeks** |

**Parallelizable**: 1A → {1B, 1C (partial)} → 1D → 1E

### Post-MVP (Phase 2+): Polish & Content

- ⏳ AI System (Skirmish opponents) - 15-20 days
- ⏳ Scripting Engine (Campaign/missions) - 20+ days
- ⏳ Advanced Audio - 3-4 days
- ⏳ UI Polish & Fixes - 5-7 days
- ⏳ Network Play - 10-15 days
- ⏳ Replays - 5-7 days

---

## Part 5: ACTION ITEMS (Starting Points)

### Immediate Actions (This Week)

1. **Create OrderGeneratorSystem** - Routes input → game orders
   - File: `src/OpenSage.Game/Logic/OrderGeneratorSystem.cs`
   - Connect SelectionSystem.OnEndDragSelection → order generation

2. **Extend GameLogic.Update()** - Make game loop active
   - File: `src/OpenSage.Game/Logic/GameLogic.cs`
   - Call ObjectManager.UpdateAll() each tick
   - Integrate with performance profiler

3. **Implement PathfindingSystem skeleton**
   - File: `src/OpenSage.Game/Logic/Pathfinding/PathfindingSystem.cs`
   - Builds on existing TerrainData
   - Ready for A* implementation

### Key Files to Review

- [src/OpenSage.Game/Logic/SelectionSystem.cs](src/OpenSage.Game/Logic/SelectionSystem.cs) - How selection works
- [src/OpenSage.Game/Logic/GameLogic.cs](src/OpenSage.Game/Logic/GameLogic.cs) - Main game loop stub
- [src/OpenSage.Game/Logic/Object/GameObject.cs](src/OpenSage.Game/Logic/Object/GameObject.cs) - Base game object
- [src/OpenSage.Game/Terrain/HeightMap.cs](src/OpenSage.Game/Terrain/HeightMap.cs) - Terrain data for pathfinding

### References

- Original Game: `references/generals_code/` - Has AI, combat, building logic
- Documentation: [docs/WORKDIR/phases/](../phases/) - Detailed phase plans
- Module System: `src/OpenSage.Game/Logic/Object/Modules/` - How to add new systems

---

## Conclusion

**Status**: OpenSAGE is a beautiful rendering engine with no gameplay.

**Path Forward**: 4-5 weeks of focused effort → "Minimum Playable Skirmish Game" with:
- ✅ Unit selection & movement
- ✅ Combat between players
- ✅ Building construction
- ✅ Resource gathering
- ✅ Basic economy

**Critical Dependencies**:
1. Input System ← Foundation
2. Pathfinding ← Unblocks Movement
3. Combat ← Unblocks Skirmish
4. Building ← Unblocks Base Management
5. Economy ← Completes gameplay loop

**Success Metric**: Players can select units, move them, fight enemies, build structures, and play 15-20 min skirmish games.
