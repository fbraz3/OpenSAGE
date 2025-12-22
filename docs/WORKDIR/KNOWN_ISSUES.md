# Known Issues — Runtime notes

This file collects recent runtime issues, quick mitigations applied, and recommended follow-ups so developers can revisit them later.

**1. Code page / encoding failure**
- **Symptom:** Exception: "No data is available for encoding 1252" during launcher startup.
- **Repro:** Run launcher on some .NET runtimes without `CodePagesEncodingProvider` registered.
- **Quick fix applied:** Call `System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);` in `Program.Main`.
- **Recommended follow-up:** Confirm this is acceptable across all targeted platforms and document in developer guide.
- **Files touched:** src/OpenSage.Launcher/Program.cs

**2. INI parsing — missing files & enum tokens**
- **Symptom A:** Null or missing `FileSystemEntry` caused NRE when loading optional INIs.
- **Quick fix applied:** Make `ContentManager.LoadIniFile` tolerant: log missing entries and continue instead of throwing.
- **Symptom B:** IniParseException for Zero Hour enum values (e.g., `SUBDUAL_MISSILE`).
- **Quick fix applied:** Enum mapping generation updated to include each declared enum field and any `IniEnumAttribute` names (fixes duplicate-valued enum members mapping).
- **Recommended follow-up:** Add unit tests for `IniParser` covering Zero Hour INI tokens and optional INI handling.
- **Files touched:** src/OpenSage.Game/Content/ContentManager.cs, src/OpenSage.Game/Data/Ini/IniParser.Enums.cs

**3. SleepyUpdateList heap invariant violation**
- **Symptom:** Runtime assertion: "Sleepy updates are broken" (heap parent/child ordering invalid) from `SleepyUpdateList.Validate()` during logic ticks.
- **Quick mitigation applied:** Replace hard assert/crash with a warning log and `Remake()` to rebuild the heap, allowing execution to continue.
- **Recommended follow-up:** Instrument `SleepyUpdateList` and `UpdateModule` to log every Add/Remove/Update change (with module id and `IndexInLogic`) to reproduce and find the root cause; write targeted unit/regression test.
- **Files touched:** src/OpenSage.Game/Logic/GameLogic.cs (SleepyUpdateList.Validate())

**4. Unimplemented physics bounce sound**
- **Symptom:** `NotImplementedException` in `PhysicsBehavior.DoBounceSound(Vector3&)` during game logic update.
- **Status:** Not implemented; this caused the run to stop after earlier fixes allowed execution to progress.
- **Quick options:** implement a no-op to continue, or implement audio integration properly.
- **Files touched:** src/OpenSage.Game/Logic/Object/Behaviors/PhysicsBehavior.cs

**5. General guidance / next steps**
- **Immediate:** If you want further runs, apply the no-op for `DoBounceSound()` to let execution proceed and discover additional unimplemented areas.
- **Diagnostics:** Add structured logging in `SleepyUpdateList` (Add/Remove/Rebalance) and in `UpdateModule` to capture `IndexInLogic` changes.
- **Testing:** Add unit tests for `IniParser` and a small deterministic test harness exercising `SleepyUpdateList` (add/remove sequence) to reproduce the heap corruption.
- **Documentation:** Add a short entry to `docs/developer-guide.md` describing the need for the code pages encoding provider and how to run with original game assets.

---

If you want, I can now apply the quick no-op fix to `DoBounceSound()` so the launcher continues; otherwise I'll instrument `SleepyUpdateList` next to find the root cause.