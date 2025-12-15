---
applyTo: '**/*.md'
---

## Documentation Guidelines

- All documentation must be in English
- Use Markdown format
- Don't add documentation files directly in the root `docs/` folder
- The root folder `/` should only contain project-level files (README.md, LICENSE, etc.)
- **Planning Documents**: Place in `docs/PLANNING/` (architecture, phase plans, roadmaps)
- **Development Diary**: Update `docs/DEV_BLOG/YYYY-MM-DIARY.md` with daily entries
  - Create new file each month (YYYY-MM-DIARY.md)
  - Order entries newest to oldest (recent at top after Overview section)
  - Keep entries informal and concise
- **Session Reports & Analysis**: Place in `docs/ETC/` (investigation results, session summaries)
- **Phase Checklist Updates**: At the end of each session working on a phase, update the corresponding `docs/PLANNING/phases/PHASEXX_*.md` file to mark completed tasks with `[x]`

**Key Rule**: DEV_BLOG is for diary only. Session summaries, progress reports, and analysis go to MISC.

## Documentation Updates

- **Dev diary** (`docs/DEV_BLOG/YYYY-MM-DIARY.md`): Informal session notes, newest first
- **Session reports** (`docs/ETC/PHASEXX_SESSIONX_*.md`): Formal summary after significant progress
- **Phase planning** (`docs/PLANNING/phases/PHASEXX_*.md`): Update `[x]` checklist at session end
- **Technical discoveries**: Place in `docs/ETC/` (e.g., `CRITICAL_VFS_DISCOVERY.md`)

## Documentation Organization

### `docs/PLANNING/` - Planning & Design
**Purpose**: Project planning, architecture, roadmaps
- Phase planning documents (PHASEXX_*.md)
- ROADMAP.md - Overall project roadmap
- ARCHITECTURE.md - System architecture decisions
- PROJECT_OVERVIEW.md - High-level project description

### `docs/PLANNING/phases/` - Phase-Specific Plans
**Purpose**: Detailed phase plans and checklists
**Guideline**: use `PHASEXX_purpose.md` format for filenames - XX is phase number, purpose is brief description
**Restriction**: Avoid using `weeks` for phase work segmentation, don't try to guess completion times in calendar weeks, just ignore this information entirely
**Rationale**: Sprints provide a standardized Agile framework terminology, ensuring consistency with sprint-based development methodologies

**Naming Examples:**
- PHASE01_INITIAL_RESEARCH.md
- PHASE02_ENGINE_SELECTION.md
- PHASE03_PROTOTYPING.md
- etc.

**Not here**: Session reports, progress logs, investigation notes

### `docs/DEV_BLOG/` - Development Diary ONLY
**Purpose**: Chronological development diary entries
- YYYY-MM-DIARY.md - Monthly diary (ONE file per month)
  - Entries newest to oldest (most recent at top, after Overview)
  - Informal, daily/session notes
  - Short summaries of work done
- README.md - Index of available diaries

**Only this goes here**: Diary entries and README

**Not here**: Session reports, summaries, analysis, phase progress

### `docs/ETC/` - Research, Analysis & Reports
**Purpose**: Investigation notes, analysis, session reports, reference materials
- SESSION_REPORTS/ subdirectory for PHASEXX_SESSIONX_*.md files
  - PHASE11_SESSION1_REPORT.md
  - PHASE12_SESSION2_COMPLETE.md
  - PHASE13_SESSION3_COMPLETE.md
- ANALYSIS/ for technical investigations
  - VULKAN_ANALYSIS.md
  - MULTITHREADING_ANALYSIS.md
  - etc.
- Technical analysis and discovery notes
- Reference materials
- Lessons learned documents
- Investigation findings

**Goes here**: Phase session reports, discovery documents, analysis, reference

**Not here**: Diary entries, planning documents, phase planning