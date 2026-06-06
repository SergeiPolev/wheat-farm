# Figma-to-Unity Agent Team Skill — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Create an OpenCode skill that orchestrates 4 AI agents (Conductor, Analyzer, Builder, QA) to convert Figma mockups into pixel-perfect Unity uGUI prefabs with human validation at every stage.

**Architecture:** Conductor skill (SKILL.md) orchestrates three sub-agents via Task tool. Existing `figma-to-unity-ui` and `unity-ui-mcp` skills provide reference material. Obsidian vault stores sprite registry and component library.

**Tech Stack:** OpenCode skills, Task tool (subagent_type: explore/general), Figma MCP, Unity UI MCP, Unity MCP, Obsidian MCP

**Design doc:** `docs/plans/2026-04-03-figma-to-unity-skill-design.md`

---

### Task 1: Restructure skill directory

**Files:**
- Modify: `~/.config/opencode/skills/superpowers/figma-to-unity-ui/SKILL.md`
- Create: `~/.config/opencode/skills/superpowers/figma-to-unity-ui/prompts/analyzer.md`
- Create: `~/.config/opencode/skills/superpowers/figma-to-unity-ui/prompts/builder.md`
- Create: `~/.config/opencode/skills/superpowers/figma-to-unity-ui/prompts/qa.md`
- Keep: `~/.config/opencode/skills/superpowers/figma-to-unity-ui/reference/` (existing, unchanged)

**Step 1:** Create `prompts/` directory

**Step 2:** Verify existing reference files are intact (anchor-mapping.md, layout-rules.md, prediction.md)

**Step 3:** Commit: `feat: prepare figma-to-unity skill directory for agent team`

---

### Task 2: Write Analyzer agent prompt

**File:** `~/.config/opencode/skills/superpowers/figma-to-unity-ui/prompts/analyzer.md`

**Purpose:** System prompt for the Analyzer sub-agent. Receives Figma URL, returns structured analysis.

**Content must cover:**
1. Call `figma_check_token` first
2. Call `figma_get_frame` to get full node tree
3. Call `figma_export_images` for reference PNG
4. Pattern recognition rules:
   - Count repeated INSTANCE nodes with same component name → report as "Nx [Name] — likely a list/grid"
   - Identify auto-layout containers → report direction, spacing, padding
   - Identify leaf nodes (VECTOR, small RECTANGLE with fills) → report as icons/sprites
   - Identify TEXT nodes → extract font family, size, weight, color, content
   - Identify visual layers (backgrounds, borders, shadows) by z-order
5. Sprite inventory:
   - Call `manage_asset search filter_type=Sprite` to find existing sprites
   - Call `search-vault` on Obsidian for sprite registry
   - Match Figma icon names against found sprites
6. Output format: structured markdown with blocks, patterns, sprites, questions

**Step 1:** Write the prompt file with all sections above

**Step 2:** Verify prompt references correct MCP tool names (figma_get_frame, figma_export_images, etc.)

**Step 3:** Commit: `feat: add Analyzer agent prompt for Figma parsing`

---

### Task 3: Write Builder agent prompt

**File:** `~/.config/opencode/skills/superpowers/figma-to-unity-ui/prompts/builder.md`

**Purpose:** System prompt for the Builder sub-agent. Receives precise build specs, constructs uGUI elements.

**Content must cover:**
1. ALWAYS use element-by-element API, NEVER `build_ui_from_json`
2. Workflow per module:
   - `create_prefab_ui(prefab_name, save_path)`
   - For each element: `add_ui_element` → `set_rect_transform` → `set_ui_style`
   - Optional: `set_layout_group` for containers
   - `save_prefab`
3. Rules from existing reference files:
   - Anchor mapping (from reference/anchor-mapping.md)
   - Layout rules (from reference/layout-rules.md)
   - PPUM calculation: `PPUM = 250 / corner_radius_px`
   - Image.color = white for colored sprites, tint only for neutral sprites
   - Image.preserveAspect = true for icons
   - Sliced vs Simple image type rules
4. Builder does NOT interpret Figma data — follows specs exactly
5. Reports back: prefab path, element count, any errors

**Step 1:** Write the prompt file

**Step 2:** Ensure it references correct unity-ui-mcp tool names (create_prefab_ui, add_ui_element, set_rect_transform, set_ui_style, set_layout_group, save_prefab)

**Step 3:** Commit: `feat: add Builder agent prompt for Unity UI construction`

---

### Task 4: Write QA agent prompt

**File:** `~/.config/opencode/skills/superpowers/figma-to-unity-ui/prompts/qa.md`

**Purpose:** System prompt for the QA sub-agent. Screenshots built UI, compares against Figma reference.

**Content must cover:**
1. Instantiate prefab in scene (or use prefab stage)
2. Take screenshot via `manage_camera(action="screenshot", include_image=true)`
3. Compare screenshot against Figma reference PNG (provided by Conductor)
4. Structured diff output format:
   ```
   Validation: [ModuleName]
   Overall: [match %]
   
   Issues:
     1. [ElementName] — [what's wrong] ([measurement])
     2. ...
   
   Matching:
     - [what's correct]
   ```
5. Check list:
   - Colors match (hex comparison)
   - Sizes proportional (within 2px tolerance)
   - Text content correct
   - Sprites visible (not white/pink/missing)
   - Hierarchy structure matches Figma nesting
   - Layout spacing correct
6. QA does NOT fix issues — only reports them

**Step 1:** Write the prompt file

**Step 2:** Commit: `feat: add QA agent prompt for visual validation`

---

### Task 5: Rewrite Conductor SKILL.md

**File:** `~/.config/opencode/skills/superpowers/figma-to-unity-ui/SKILL.md`

**Purpose:** Main skill file. Defines Conductor behavior — orchestrates agents, talks to user.

**Major changes from existing:**
- Remove direct Figma parsing (Analyzer does this now)
- Remove direct building (Builder does this now)
- Add agent dispatch via Task tool
- Add human-in-the-loop checkpoints
- Add Obsidian knowledge management

**SKILL.md structure:**

```markdown
---
name: figma-to-unity-ui
description: Use when building Unity UI from Figma designs — orchestrates Analyzer, Builder, and QA agents with human validation at every stage
---

# Figma to Unity UI (Agent Team)

## Overview
Orchestrates 3 specialized agents to convert Figma mockups into Unity uGUI prefabs.
Human confirms every block before proceeding.

## Prerequisites
- figma-mcp connected (Analyzer needs it)
- unity-ui-mcp connected (Builder needs it)  
- unityMCP connected (QA needs it)
- obsidian MCP (optional, for sprite/component registry)

## Agent Dispatch
- Analyzer: Task(subagent_type="explore", prompt=<read prompts/analyzer.md> + context)
- Builder: Task(subagent_type="general", prompt=<read prompts/builder.md> + specs)
- QA: Task(subagent_type="general", prompt=<read prompts/qa.md> + screenshots)

## Phase 1: Intelligence
[Dispatch Analyzer, present results to user, ask questions]

## Phase 2: Preparation  
[Export sprites, configure imports, check fonts]

## Phase 3: Modular Build
[For each block: dispatch Builder → dispatch QA → show user → approve/fix loop]

## Phase 4: Composition
[Assemble blocks, final QA, user approval]

## Phase 5: Knowledge Update
[Update Obsidian sprite registry + component library]
```

**Step 1:** Read current SKILL.md content (already done above)

**Step 2:** Write new SKILL.md with full Conductor orchestration logic

**Step 3:** Ensure flowchart for the build→QA→fix loop

**Step 4:** Commit: `feat: rewrite figma-to-unity SKILL.md as Conductor with agent team`

---

### Task 6: Test with ProgressOffer screen

**Purpose:** End-to-end test of the skill on the ProgressOffer Figma frame we already analyzed.

**Steps:**
1. Start new conversation
2. Load figma-to-unity-ui skill
3. Provide Figma URL: `https://www.figma.com/design/BjU1V8nNHnbgKyD6EnKOKd/My-Perfect-Hotel-UI?node-id=38519-39497`
4. Verify Analyzer identifies: 5x TaskBlock, ProgressWheel, RibbonBanner, CTABanner, Timer, ExitButton
5. Verify Conductor asks right questions (dynamic list? sprites found?)
6. Verify Builder constructs TaskRow module element-by-element
7. Verify QA takes screenshot and compares
8. Verify user approval checkpoint works
9. Document failures and iterate on skill

**Step 1:** Test Phase 1 (Analyzer) in isolation

**Step 2:** Test Phase 3 (Builder + QA loop) on single TaskRow module

**Step 3:** If issues found, update relevant prompt files

**Step 4:** Commit fixes: `fix: adjust agent prompts based on test results`

---

### Task 7: Create Obsidian templates

**Files:**
- Create note: `wheat-farm` vault → `UI/Sprite Registry.md`
- Create note: `wheat-farm` vault → `UI/Component Library.md`

**Step 1:** Create Sprite Registry note with header table

**Step 2:** Create Component Library note with header table  

**Step 3:** Add existing sprites from today's session (Crystal, Star, Lock, etc.)

**Step 4:** Commit: `docs: add Obsidian UI knowledge base templates`

---

## Execution Notes

- Tasks 1-5 are the skill creation (can be done sequentially)
- Task 6 is testing (requires Unity + Figma MCP running)
- Task 7 is knowledge base setup (independent)
- Tasks 2, 3, 4 are independent and can be parallelized
- Task 5 depends on 2, 3, 4 (SKILL.md references prompt files)
