# OpenSAGE Copilot Instructions

## Project Overview

OpenSAGE is a free, open-source re-implementation of the SAGE game engine used in Command & Conquer: Generals and other RTS games. The project focuses on parsing game data formats, 3D rendering, and game logic systems. It's written in C# targeting .NET 10 and uses Veldrid for cross-platform graphics (Metal on macOS, Direct3D11 on Windows, OpenGL on Linux).

## Core Architecture

### Game Loop Orchestration (Game.cs)
The `Game` class is the central orchestrator coordinating multiple systems:

```
Game (main loop controller)
├─ GraphicsSystem (rendering pipeline)
├─ GameLogic (authoritative game state)
├─ Scene3D (3D world representation)
├─ GameEngine (core resources and managers)
├─ AudioSystem
├─ ScriptingSystem (Lua engine)
├─ SelectionSystem (player selection)
└─ OrderGeneratorSystem (command handling)
```

**Multi-frequency update pattern:**
- **Render loop (60Hz)**: Calls `Game.Update()` and `Game.Render()` every frame
- **Logic tick (5Hz)**: Synchronous authoritative state updates via `LocalLogicTick()`
- **Scripting tick**: Map scripts and AI execution

Reference: `src/OpenSage.Game/Game.cs`, `src/OpenSage.Launcher/Program.cs` (main game loop at line 248-265)

### Entity-Component-Module Pattern
Game objects use a strict module hierarchy for composable behavior:

```csharp
GameObject (logical entity)
├─ BehaviorModule (extends ObjectModule)
│  ├─ AIUpdate
│  ├─ PhysicsBehavior
│  ├─ ContainModule
│  └─ DrawModule
└─ UpdateModule (scheduled updates with sleep times)
```

**Key patterns:**
- `ModuleBase.OnObjectCreated()` → called when module's owner object is created
- `UpdateModule.Update()` → returns `UpdateSleepTime` (None, Frames(n), or Forever) to control next execution
- Modules implement state persistence via `IPersistableObject`

Reference: `src/OpenSage.Game/Logic/Object/GameObject.cs`, `src/OpenSage.Game/Logic/Object/Modules/ModuleBase.cs`

### Sleepy Update System
Efficient scheduling of module updates via `GameLogic._sleepyUpdates`:
- Modules can defer execution with `UpdateSleepTime.Frames(n)`
- `GameLogic` maintains a priority queue, waking modules only when `NextCallFrame` is reached
- Use `SetWakeFrame()` to explicitly reschedule module updates

This separates deterministic logic (5Hz) from variable-rate rendering, critical for network synchronization.

## Critical Conventions

### Coding Style (docs/coding-style.md)
- **Allman braces** (brace on new line), 4-space indentation
- **Field naming**: `_camelCase` (private), `s_camelCase` (static), no prefix for public
- **Always specify visibility**: `private string _foo` not `string _foo`
- **Namespace imports**: System.* first, then alphabetical, outside `namespace` declaration
- Use `var` only when type is obvious: `var stream = new FileStream(...)`
- Use language keywords: `int` not `Int32`, `string` not `String`

### File Organization
- One public type per file (with nested partial classes allowed)
- Fields declared at top of type
- Example: ObservableLinkedList pattern in `docs/coding-style.md`

### Module Implementation Pattern
When creating new modules:
```csharp
public class MyModule : BehaviorModule
{
    public override void OnObjectCreated(GameObject gameObject)
    {
        base.OnObjectCreated(gameObject);
        // Initialize module
    }

    internal override UpdateSleepTime Update(in TimeInterval time)
    {
        // Return when to next update this module
        return UpdateSleepTime.Frames(5); // update in 5 frames
    }

    public override void Load(StatePersister reader)
    {
        reader.PersistVersion(1);
        reader.BeginObject("Base");
        base.Load(reader);
        reader.EndObject();
        // Persist module state
    }
}
```

## Essential Workflows

### Building & Running
```bash
# Build the project
cd src
dotnet build

# Run the launcher (requires game installation)
cd /Users/felipebraz/PhpstormProjects/pessoal/OpenSAGE
# Define game path via environment variable:
export CNC_GENERALS_PATH=/path/to/generals
export BFME_PATH=/path/to/bfme

# Launch with options
./bin/publish/OpenSage.Launcher --renderer Metal --game generals

# Debug with developer mode (F11 in-game)
dotnet run --project src/OpenSage.Launcher/OpenSage.Launcher.csproj -- --developermode
```

**Available options:**
- `--renderer {Metal|Vulkan|OpenGL|Direct3D11|OpenGLES}`
- `--game {CncGenerals|CncGeneralsZh|Bfme|Bfme2|Bfme2Rotwk}`
- `--map "path/to/map.map"` (skip menu, load map directly)
- `--noshellmap` (disable loading shell map, faster startup)
- `--fullscreen`, `--novsync`, `--developermode`

### Testing
Located in `src/OpenSage.Game.Tests/`:
```bash
dotnet test src/OpenSage.Game.Tests/
```

Use `MockedGameTest` base class for testing game logic without full initialization:
```csharp
public class MyLogicTest : MockedGameTest
{
    [Test]
    public void TestBehavior()
    {
        // Uses in-memory game instance with mocked systems
    }
}
```

## Key Dependencies & Integration Points

### Veldrid Graphics (4.9.0)
Cross-platform graphics abstraction. On macOS, primarily uses Metal backend.
- `GraphicsDevice` → Veldrid graphics device
- `Swapchain` → frame buffer management
- `CommandList` → GPU command recording

### Content Management (ContentManager.cs)
Asset loading system with support for:
- `.w3d` models, `.dds`/`.tga` textures
- `.map` map files
- `.ini` data files
- `.apt` UI animations
- `.csf` text localization

FileSystem abstraction allows composite file systems (disk + archives).

### Scripting & UI (AptCallbacks)
UI callbacks marked with `[AptCallbacks(SageGame.Bfme, ...)]`:
```csharp
[AptCallbacks(SageGame.Bfme)]
static class AptMainMenu
{
    public static void OnInitialized(string param, ActionContext context, AptWindow window, IGame game)
    {
        // Called after APT UI initialization
    }
}
```

## Common Debugging Patterns

### Inspect Game State
- `Game.GameLogic._objects` → all active GameObjects
- `Game.Scene3D` → terrain, water, camera, lighting
- `Game.AssetStore` → loaded assets and definitions
- Developer mode: **F11** in-game toggles developer UI with frame stats

### Hotkeys
- **F9**: Toggle logic simulation (freeze/unfreeze game logic)
- **F10**: Single-step logic frame (when frozen)
- **F11**: Toggle developer mode
- **Pause**: Restart game

### Common Fixes
- **Graphics issues**: Check `GraphicsSystem.RenderPipeline` and `RenderContext`
- **Logic desync**: Ensure `UpdateModule` returns correct `UpdateSleepTime`; verify `StatePersister` for save/load
- **Asset loading**: Check `ContentManager` and `AssetStore` for missing data files

## Repository Standards

### Pull Requests
- Use **Rebase and Merge** (squash only for unrelated commits)
- Commit message format: conventional (feat:, fix:, refactor:)
- All commits must build successfully
- One focused change per PR (separate unrelated fixes)

### Code Style Enforcement
- EditorConfig file (`.editorconfig`) at repository root handles auto-formatting
- No PRs for style changes alone
- Prefer `nameof(...)` over string literals

### Environment Setup
- Requires .NET 10.0.100+
- macOS: SDL2 via Homebrew: `brew install sdl2`
- Linux: libc-dev, libsdl2-dev, libopenal-dev
- Game files not included; users must own original games (C&C Generals, BFME, etc.)

## Where to Explore

- **Architecture deep-dive**: `docs/developer-guide.md`
- **Module implementations**: `src/OpenSage.Game/Logic/Object/Modules/`
- **Rendering pipeline**: `src/OpenSage.Game/Graphics/`
- **Data format parsing**: `src/OpenSage.FileFormats*/` and `docs/Map Format.txt`
- **Scripting/UI**: `src/OpenSage.Game/Logic/Object/` and AptCallbacks pattern in `src/OpenSage.Mods.Bfme/Gui/`
