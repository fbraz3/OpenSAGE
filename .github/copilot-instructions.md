# OpenSAGE Copilot Instructions

## Snapshot
- OpenSAGE is a .NET 10 C# re-implementation of the SAGE engine that focuses on parsing `.map`, `.w3d`, `.ini`, `.apt`, and other formats while rebuilding the rendering and logic layers around Veldrid (see [README.md](../README.md) and [docs/developer-guide.md](../docs/developer-guide.md)).
- Veldrid, Veldrid.ImageSharp, and Metal/OpenGL/D3D11 renderers sit beside `src/OpenSage.Game`, `src/OpenSage.Rendering`, and `src/OpenSage.Graphics`, so expect logic and graphics code to be interdependent in their respective folders.

## Architecture & core flows
- `src/OpenSage.Game/Game.cs` drives the 60 Hz render loop, delegates 5 Hz authority to `GameLogic`, and still calls Lua/scripting ticks; `src/OpenSage.Launcher/Program.cs` wires the launcher binary to these systems and exposes runtime options.
- Game objects follow the module hierarchy in `src/OpenSage.Game/Logic/Object/Modules/ModuleBase.cs`; `BehaviorModule` reports to `GameLogic` while `UpdateModule` returns `UpdateSleepTime` values so the sleepy scheduler only wakes modules when needed.
- Rendering pulls from `src/OpenSage.Graphics/`, math helpers in `src/OpenSage.Mathematics/`, and Veldrid 4.9.0 abstractions; data formats live under `src/OpenSage.FileFormats*/` with helpers such as `ContentManager.cs` and documentation in `docs/Map Format.txt`.

## Workflows
- **Build/run**: `cd src && dotnet build`; start the launcher via `dotnet run --project src/OpenSage.Launcher/OpenSage.Launcher.csproj -- --developermode` after exporting `CNC_GENERALS_PATH`, `BFME_PATH`, etc., as described in [docs/developer-guide.md](../docs/developer-guide.md).
- **Runtime flags**: pass `--renderer {Metal|Vulkan|OpenGL|Direct3D11|OpenGLES}`, `--game {CncGenerals|CncGeneralsZh|Bfme|Bfme2|Bfme2Rotwk}`, `--map`, `--noshellmap`, `--fullscreen`, or `--novsync` to the published launcher under `bin/publish/OpenSage.Launcher`.
- **Testing**: run `dotnet test src/OpenSage.Game.Tests/` and prefer `MockedGameTest` for logic verification without full graphics initialization.
- **Debugging**: F11 toggles developer mode; F9 freezes logic, F10 steps, and Pause restarts while `Game.GameLogic._objects`, `Game.Scene3D`, and `Game.AssetStore` remain handy inspection targets.

## Code & documentation conventions
- Follow `docs/coding-style.md`: Allman braces, four-space indentation, `_camelCase` private fields (`s_` for static), explicit visibility, `System.*` imports first, `var` only when obvious, and `nameof(...)` instead of literals; keep fields at the top of types.
- Documentation obeys `.github/instructions/docs.instructions.md`: diaries belong in `docs/DEV_BLOG/YYYY-MM-DIARY.md`, planning and checklists in `docs/PLANNING/` and `docs/PLANNING/phases/`, and technical/session reports in `docs/ETC/`—never add ad-hoc docs to the root of `docs/`.
- naming convention for phase plans: use `PHASEXX_purpose.md` format for filenames - XX is phase number, purpose is brief description (e.g., `PHASE01_INITIAL_RESEARCH.md`).
- When you add module-level notes, reference the relevant directory (e.g., `src/OpenSage.Game/Logic/Object/Modules/`, `src/OpenSage.Mods.Bfme/Gui/` for APT callbacks) so reviewers can trace behavior quickly.

## Resources & references
- Mirror EA behavior via the local copy under `references/generals_code` or the upstream `electronicarts/CnC_Generals_Zero_Hour` repo (use `deepwiki` if searching remotely). Use `grep_search`/`file_search` against `references/generals_code` before guessing behavior.
- Data pipelines often mix `ContentManager.cs`, `OpenSage.Game/GameLogic`, and `src/OpenSage.FileFormats.*`, so glancing through `docs/Map Format.txt` and `src/OpenSage.FileFormats.W3d/` usually clarifies expectations.

## Collaboration expectations
- Every change must build; use conventional commits (`feat:`, `fix:`, etc.) and expect PRs to follow Rebase and Merge with one focused change per submission.
- Cite files when describing behavior (module registration, render passes) and document why you chose a path (e.g., linking `src/OpenSage.Graphics/RenderContext.cs` when describing a render pass).
- Ask maintainers before touching native asset loaders—the tree does not bundle original game files and expects legally acquired installations per [README.md](../README.md).
