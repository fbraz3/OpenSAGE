---
applyTo: 'docs/**/*.md'
---

# OpenSAGE Documentation Structure & Organization

This document describes the organizational structure and cross-reference patterns for OpenSAGE project documentation, particularly the BGFX migration project documentation.

## Directory Hierarchy

```
/docs/
├── README.md                          # Central documentation hub
├── coding-style.md                    # Code style conventions  
├── developer-guide.md                 # Architecture and development guide
├── Map Format.txt                     # Game map file format
├── map-position-selection.md          # Map position selection implementation
│
├── phases/                            # Phase planning documents (strategic level)
│   ├── README.md                      # Phase overview and navigation
│   ├── Phase_1_Migration_Strategy.md  # Phase 1 implementation strategy
│   ├── Phase_2_Architectural_Design.md
│   ├── Phase_3_Core_Implementation.md
│   ├── Phase_4_Integration_and_Testing.md
│   │
│   └── support/                       # Phase 1 research & deep-dive analysis
│       ├── README.md                  # Support documents navigation
│       ├── Phase_1_Research_and_Planning.md
│       ├── Phase_1_Technical_Feasibility_Report.md
│       ├── Phase_1_Requirements_Specification.md
│       ├── Phase_1_Risk_Assessment.md
│       ├── Feature_Audit.md           # (renamed from Phase_1_Feature_Audit.md)
│       ├── Performance_Baseline.md    # (renamed from Phase_1_Performance_Baseline.md)
│       ├── Shader_Compatibility.md    # (renamed from Phase_1_Shader_Compatibility.md)
│       └── Dependency_Analysis.md     # (renamed from Phase_1_Dependency_Analysis.md)
│
└── misc/                              # Executive summaries & miscellaneous
    ├── README.md                      # Executive documentation navigation
    ├── Phase_1_Completion_Summary.md
    ├── BGFX_PROJECT_SUMMARY.md        # Project overview
    ├── IMPLEMENTATION_ROADMAP.md      # Complete project timeline
    ├── VELDRID_vs_BGFX_COMPARISON.md  # Technical comparison
    └── COMPLETE_DOCUMENTATION_PACKAGE.md
```

## Document Organization Logic

### Three-Tier Categorization

**Tier 1: Strategic Planning** (`/docs/phases/`)
- Phase-level planning documents
- Implementation strategy
- High-level architecture decisions
- Go/No-Go decision documents
- Phase gate documents (Phase 2, 3, 4)

**Tier 2: Research & Analysis** (`/docs/phases/support/`)
- Detailed technical feasibility analysis
- Feature audits and compatibility matrices
- Performance baselines and metrics
- Risk assessments and mitigation strategies
- Dependency analysis
- Deep-dive shader compatibility studies

**Tier 3: Executive & Miscellaneous** (`/docs/misc/`)
- Executive summaries and completions
- Project overviews and roadmaps
- Technical comparisons
- Status dashboards
- High-level project documentation
- Business-focused summaries

### File Naming Conventions

**Main Phase Documents** (in `/docs/phases/`):
- `Phase_1_Migration_Strategy.md` - Implementation focus
- `Phase_2_Architectural_Design.md`
- `Phase_3_Core_Implementation.md`
- `Phase_4_Integration_and_Testing.md`

**Phase 1 Deep-Dive Documents** (in `/docs/phases/support/`):
- `Phase_1_Technical_Feasibility_Report.md` - Executive feasibility
- `Phase_1_Requirements_Specification.md` - Formal requirements
- `Phase_1_Research_and_Planning.md` - Research overview
- `Phase_1_Risk_Assessment.md` - Risk management
- `Feature_Audit.md` - Feature compatibility (shortened name)
- `Performance_Baseline.md` - Performance analysis (shortened name)
- `Shader_Compatibility.md` - Shader analysis (shortened name)
- `Dependency_Analysis.md` - NuGet/dependency audit (shortened name)

**Executive Documents** (in `/docs/misc/`):
- `Phase_1_Completion_Summary.md` - Executive summary
- `BGFX_PROJECT_SUMMARY.md` - Project overview
- `IMPLEMENTATION_ROADMAP.md` - Timeline/roadmap
- `VELDRID_vs_BGFX_COMPARISON.md` - Technical comparison

## Cross-Reference Patterns

### Relative Path Convention

All cross-references use **relative paths** from the referencing document's location:

**From `/docs/phases/README.md` to support documents**:
```markdown
[Phase_1_Research_and_Planning.md](support/Phase_1_Research_and_Planning.md)
[Feature_Audit.md](support/Feature_Audit.md)
```

**From `/docs/phases/support/Phase_1_Technical_Feasibility_Report.md` to sibling support docs**:
```markdown
[Feature_Audit.md](Feature_Audit.md)
[Shader_Compatibility.md](Shader_Compatibility.md)
```

**From `/docs/phases/support/Phase_1_Risk_Assessment.md` to parent phase doc**:
```markdown
[../Phase_1_Migration_Strategy.md](../Phase_1_Migration_Strategy.md)
```

**From `/docs/misc/Phase_1_Completion_Summary.md` to phases documents**:
```markdown
[../phases/support/Phase_1_Technical_Feasibility_Report.md](../phases/support/Phase_1_Technical_Feasibility_Report.md)
[../phases/Phase_1_Migration_Strategy.md](../phases/Phase_1_Migration_Strategy.md)
```

### Link Format Rules

1. **Always use markdown links**, not backtick references:
   ❌ Bad: `See Phase_1_Feature_Audit.md for details`
   ✅ Good: `See [Feature_Audit.md](Feature_Audit.md) for details`

2. **Use relative paths** for portability:
   ❌ Bad: `[Feature_Audit](../../docs/phases/support/Feature_Audit.md)`
   ✅ Good: `[Feature_Audit.md](Feature_Audit.md)` (from same directory)

3. **Include `.md` extension** for clarity:
   ❌ Bad: `[Feature Audit](Feature_Audit)`
   ✅ Good: `[Feature_Audit.md](Feature_Audit.md)`

4. **Use descriptive link text** when helpful:
   ❌ Bad: `[click here](Phase_1_Technical_Feasibility_Report.md)`
   ✅ Good: `[Phase_1_Technical_Feasibility_Report.md](Phase_1_Technical_Feasibility_Report.md)` or `[Technical Feasibility Report](Phase_1_Technical_Feasibility_Report.md)`

## Document Update Guidelines

### When Moving/Renaming Phase 1 Support Documents

If support documents are renamed or relocated:

1. **Update all back-references** in:
   - `/docs/phases/support/Phase_1_Technical_Feasibility_Report.md` (appendix section)
   - `/docs/phases/support/Phase_1_Risk_Assessment.md` (appendix section)
   - `/docs/misc/Phase_1_Completion_Summary.md` (deliverables index)
   - `/docs/phases/README.md` (document navigation)
   - `/docs/README.md` (central hub directory structure)

2. **Update README files** in affected directories:
   - `/docs/phases/support/README.md`
   - `/docs/phases/README.md`
   - `/docs/misc/README.md`

3. **Validate all links** by running:
   ```bash
   cd /docs && grep -rn "OLD_FILE_NAME" --include="*.md" .
   ```

### When Creating New Phase Documents

For Phase 2, 3, or 4 expansion:

1. **Create planning document** in `/docs/phases/`:
   - `Phase_X_Architectural_Design.md` (or appropriate phase focus)

2. **Create support subdirectory** if deep-dive analysis is needed:
   - `/docs/phases/Phase_X_support/` (optional, use consistent naming)

3. **Update parent README files**:
   - `/docs/phases/README.md` - Add phase overview section
   - `/docs/README.md` - Update directory structure and reading guides
   - `/docs/phases/support/README.md` - Add links if new support docs created

4. **Follow established naming** for consistency with Phase 1 pattern

## Navigation Structure

### README.md Files

Every directory with multiple markdown files should have a `README.md` that:

1. **Lists all documents** in the directory with purpose/content summary
2. **Provides reading recommendations** organized by role (e.g., Project Managers, Technical Team)
3. **Shows directory structure** for context
4. **Links to other directories** using relative paths

### Central Hub (`/docs/README.md`)

The main README serves as the entry point and should:

1. **Categorize all documentation** by type and directory
2. **Provide role-based reading guides** (PMs, architects, engineers, QA)
3. **Show complete directory structure**
4. **Link to specialized README** files in subdirectories
5. **Explain the three-tier organization** (strategic, research, executive)

## Cross-Reference Validation

### Automated Checks

To verify all cross-references are valid:

```bash
# Find all markdown files with broken links
cd /docs && for file in $(find . -name "*.md"); do
  grep -o '\[.*\](\./[^)]*\.md)' "$file" | while read link; do
    path=$(echo "$link" | sed 's/.*(\(.*\)).*/\1/')
    if [ ! -f "$(dirname "$file")/$path" ]; then
      echo "Broken link in $file: $link"
    fi
  done
done
```

### Manual Review Checklist

When updating documentation:

- [ ] All file paths use relative notation (`./`, `../`, or no prefix for same dir)
- [ ] All links end with `.md` extension
- [ ] No absolute paths (e.g., `/docs/phases/...`)
- [ ] Support document references updated in all locations
- [ ] README files in parent directories updated
- [ ] Central `/docs/README.md` updated if structure changed

## Future Expansion

### Phase 2-4 Documentation Pattern

As Phases 2, 3, and 4 progress, follow the Phase 1 pattern:

**Phase 2 Example**:
```
/docs/phases/
├── Phase_2_PoC_and_Bindings.md        (main planning)
└── Phase_2_support/                    (if detailed analysis needed)
    ├── BGFX_Bindings_Design.md
    ├── Shader_Compilation_Pipeline.md
    └── Integration_Patterns.md
```

**Phase 3 Example**:
```
/docs/phases/
├── Phase_3_Core_Integration.md        (main planning)
└── Phase_3_support/                    (if detailed analysis needed)
    ├── Veldrid_Replacement_Strategy.md
    ├── Feature_Migration_Plan.md
    └── Performance_Optimization.md
```

This maintains the **three-tier hierarchy** while allowing each phase to have its own support documentation.

## Summary

The OpenSAGE documentation structure uses:

1. **Three-tier organization** by document type and audience (strategic/research/executive)
2. **Relative path references** for all cross-links
3. **Consistent naming conventions** (Phase_X_ prefix for deep-dive, shortened names for support docs)
4. **README files** at each level for navigation and context
5. **Clear separation** between planning docs (phases/) and analysis docs (phases/support/)

This structure enables:

✅ Easy navigation by document type  
✅ Clear role-based entry points  
✅ Portable documentation (relative paths)  
✅ Scalability for additional phases  
✅ Consistent cross-referencing patterns  
✅ Quick location of related documents  

When creating or modifying documentation, maintain these patterns to ensure consistency and ease of navigation.
