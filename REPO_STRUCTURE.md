# OpenSAGE Repository Structure

**Last Updated**: December 12, 2025  
**Status**: Clean and organized

## Root Level
Essential project files only:
```
├── README.md                  # Project overview
├── CONTRIBUTING.md            # Contribution guidelines
├── LICENSE.md                 # MIT License
├── LICENSE-EA.md              # EA License
├── REPO_STRUCTURE.md          # This file
└── global.json                # .NET version specification
```

## Source Code
```
src/
├── OpenSage.Game/                    # Main game engine
├── OpenSage.Graphics/                # Graphics systems (Veldrid + BGFX)
├── OpenSage.Launcher/                # Game launcher
├── OpenSage.Mods.*/                  # Game-specific mods
├── OpenSage.FileFormats*/            # File format parsers
├── OpenSage.Game.Tests/              # Unit tests
└── OpenSage.Tools.*/                 # Development tools
```

## Documentation Structure

### `docs/` - Main Documentation
```
docs/
├── README.md                          # Documentation index
├── coding-style.md                    # Code style guide (ESSENTIAL)
├── developer-guide.md                 # Developer reference (ESSENTIAL)
├── map-position-selection.md          # Feature documentation
├── Map Format.txt                     # File format reference
│
├── phases/                            # Phase documentation
│   ├── Phase_4_Integration_and_Testing.md    # Phase 4 (completed)
│   │
│   ├── BGFX_MIGRATION_EXECUTIVE_SUMMARY.md   # Phase 5 active
│   ├── BGFX_MIGRATION_INDEX.md                # Phase 5 active
│   ├── BGFX_MIGRATION_ROADMAP.md              # Phase 5 active
│   ├── Phase_5_BGFX_Parallel_Implementation.md # Phase 5 active
│   ├── PHASE_5A_Weekly_Execution_Plan.md      # Phase 5 active
│   ├── PHASE_5_DOCUMENTATION_INDEX.md         # Phase 5 active
│   ├── SESSION_COMPLETION_REPORT.md           # Phase 5 active
│   │
│   └── support/                       # Historical Phase 1-4
│       ├── Phase_2_Architectural_Design.md
│       ├── Phase_3_Core_Implementation.md
│       ├── PHASE_3_DOCUMENTATION_INDEX.md
│       ├── PHASE_3_GAP_ANALYSIS.md
│       ├── PHASE_4_EXECUTION_PLAN.md
│       ├── PHASE_4_RESEARCH_SUMMARY.md
│       ├── PHASE_4_WEEK_24_SESSION_COMPLETION.md
│       ├── DOCUMENTATION_ROADMAP.md
│       └── ... (others)
│
└── misc/                              # Research and analysis (80+ files)
    ├── ANALYSIS_*.md                  # Graphics analysis documents
    ├── BGFX_*.md                      # BGFX research
    ├── GRAPHICS_*.md                  # Graphics system research
    ├── VELDRID_*.md                   # Veldrid analysis
    ├── PHASE_4_*.md                   # Phase 4 analysis
    ├── WEEK_*.md                      # Weekly research
    ├── SESSION_*.md                   # Session notes
    ├── RESEARCH_*.md                  # Research findings
    └── ... (complete research archive)
```

## Key Directories

### `lib/` - Native Libraries
```
lib/
├── bgfx/                              # BGFX graphics library
│   ├── macos/
│   │   ├── arm64/libbgfx.dylib
│   │   └── x86_64/libbgfx.dylib
│   ├── windows/
│   │   └── x64/bgfx.dll
│   └── linux/
│       └── x64/libbgfx.so
└── linux-x64/                         # SDL2 and other libs
```

### `art/` - Game Assets
Game art, models, textures, etc.

### `ShaderCache/` - Compiled Shaders
Cached shader binaries for faster loading

## Documentation Categories

### Essential (Read First)
- `docs/coding-style.md` - Code standards everyone must follow
- `docs/developer-guide.md` - Architecture and subsystem overview
- `docs/README.md` - Documentation index

### Phase 5 (Active Work)
Located in `docs/phases/`:
- `PHASE_5_READY_TO_GO.md` - Executive summary (20 min read)
- `PHASE_5_PLANNING_COMPLETE.md` - Overview and planning
- `Phase_5_BGFX_Parallel_Implementation.md` - Full technical spec
- `PHASE_5A_Weekly_Execution_Plan.md` - Week 26 tasks
- `PHASE_5_DOCUMENTATION_INDEX.md` - Navigation guide

### Historical (Reference Only)
Located in `docs/phases/support/`:
- Phase 1-4 documentation
- Planning documents from previous phases
- Use for context and understanding previous decisions

### Research & Analysis (Archive)
Located in `docs/misc/`:
- Graphics system analysis
- BGFX and Veldrid research
- Session notes and completion reports
- Use when understanding research decisions

## Quick Navigation

### To find Phase 5 documentation:
```
docs/phases/
├── BGFX_MIGRATION_EXECUTIVE_SUMMARY.md  ← Start here
├── PHASE_5_READY_TO_GO.md               ← Executive summary
├── PHASE_5_PLANNING_COMPLETE.md         ← Strategic overview
├── Phase_5_BGFX_Parallel_Implementation.md ← Full spec
└── PHASE_5A_Weekly_Execution_Plan.md    ← Week 26 tasks
```

### To find code style:
```
docs/coding-style.md
```

### To find previous phase info:
```
docs/phases/support/Phase_X_...
```

### To find research notes:
```
docs/misc/
```

## Statistics

- **Total Documentation**: 5 essential + 10+ active Phase 5 + 80+ archive
- **Root Files**: 4 (minimal, clean)
- **Commits for Organization**: 2 major refactors (moving 60+ files)
- **Repository Size**: Clean and optimized

## Benefits of This Structure

1. **Clean Root**: Only essential project files at root level
2. **Active Work**: Phase 5 docs immediately visible in `docs/phases/`
3. **Historical Archive**: All previous phase docs in `docs/phases/support/`
4. **Research Preserved**: Complete research archive in `docs/misc/`
5. **Easy Navigation**: Clear structure, quick links, organized by purpose
6. **Professional**: Looks clean to new contributors and stakeholders

## Next Steps

- **Week 26 onwards**: Follow Phase 5A tasks in `PHASE_5A_Weekly_Execution_Plan.md`
- **Reference**: Use `docs/coding-style.md` for code standards
- **Questions**: Check relevant docs in their category above
- **Archive**: Everything from Phase 1-4 safe in `docs/phases/support/`

---

**Reorganized by**: Documentation Team  
**Date**: December 12, 2025  
**Status**: ✅ COMPLETE
