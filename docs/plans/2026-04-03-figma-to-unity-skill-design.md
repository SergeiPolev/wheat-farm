# Figma-to-Unity Skill Design

**Date:** 2026-04-03
**Status:** Approved
**Scope:** Universal OpenCode skill for converting Figma mockups to Unity uGUI prefabs

## Problem

Building UI in Unity from Figma mockups is manual and error-prone. Current tools (Unity UI MCP `build_ui_from_json`) fail on complex layouts -- elements misposition, layouts don't apply, no validation feedback. The result doesn't match the mockup.

## Goals

- **100% visual fidelity** -- each module must match Figma pixel-perfectly
- **Human-in-the-loop** -- user confirms at each stage, answers clarifying questions
- **Universal** -- works on any Unity project with any Figma file
- **Modular** -- build individual components first, compose into screens
- **Persistent knowledge** -- sprite registry and component library in Obsidian

## Non-Goals

- Fully autonomous (no human oversight)
- Runtime UI generation
- UI Toolkit / UXML support (uGUI only for now)

## Agent Team Architecture

Based on Anthropic's Orchestrator-Workers + Evaluator-Optimizer patterns. Four specialized agents collaborate through a Conductor that manages the user relationship.

### Agent Roles

```
                    +---------------+
        User <------+   CONDUCTOR   +------> User
                    | (orchestrator)|
                    +-------+-------+
                            |
              +-------------+-------------+
              v             v             v
        +-----------+ +-----------+ +-----------+
        |  ANALYZER | |  BUILDER  | |    QA     |
        |           | |           | |           |
        | Figma     | | Unity UI  | | Vision +  |
        | tree      | | MCP       | | compare   |
        | patterns  | | elements  | | feedback  |
        +-----------+ +-----------+ +-----------+
```

| Agent | Role | Tools | Communicates with |
|-------|------|-------|-------------------|
| **Conductor** | Orchestrator. Talks to user, asks questions, manages flow. Decides what to build next, when to validate, when to ask. Never builds or analyzes directly. | question tool, Obsidian | User, all agents |
| **Analyzer** | Parses Figma tree, recognizes patterns (lists, grids, reusable components), inventories sprites in project and Obsidian, extracts exact sizes/colors/fonts | figma-mcp, unityMCP (asset search), Obsidian, csharp-analyzer | Conductor |
| **Builder** | Builds UI elements one by one. Receives exact specs (size, color, anchor, sprite path). No decision-making -- follows instructions precisely. | unity-ui-mcp (element-by-element API) | Conductor |
| **QA** | Takes screenshots of built modules, compares against Figma reference images. Returns structured diff: what matches, what doesn't, with pixel measurements. | unityMCP (screenshot), figma-mcp (export_images) | Conductor, Builder |

### Communication Protocol

Agents do NOT talk to each other directly. All communication goes through Conductor:

```
Conductor -> Analyzer: "analyze this Figma frame"
Analyzer -> Conductor: structured report (blocks, patterns, sprites)
Conductor -> User: "here's what I found, questions: [...]"
User -> Conductor: answers
Conductor -> Builder: "build TaskRow with these exact specs: [...]"
Builder -> Conductor: "done, prefab saved at X"
Conductor -> QA: "validate TaskRow against this Figma region"
QA -> Conductor: "ProgressBar fill is 5px too wide, color is #F0C700 not #FFC700"
Conductor -> Builder: "fix ProgressBar: width -5px, color #FFC700"
Conductor -> QA: "re-validate"
QA -> Conductor: "matches reference"
Conductor -> User: "TaskRow done. [screenshot]. Approve?"
```

### Why This Team Structure?

1. **Separation of concerns** -- Analyzer doesn't need Unity tools, Builder doesn't need Figma access
2. **Conductor as bottleneck is intentional** -- every decision goes through one place, user has one point of contact
3. **QA as independent evaluator** -- Builder can't mark its own work as done. Fresh eyes catch errors Builder is blind to.
4. **Builder is "dumb"** -- receives precise instructions, doesn't interpret Figma data itself. Reduces compounding errors.

## Prerequisites (MCP Servers)

| Server | Purpose | Used by | Required |
|--------|---------|---------|----------|
| `figma-mcp` | Parse Figma trees, export sprites | Analyzer, QA | Yes |
| `unity-ui-mcp` | Create uGUI prefabs (element-by-element API) | Builder | Yes |
| `unityMCP` | Asset management, screenshots, refresh | Analyzer, QA, Builder | Yes |
| `obsidian` | Sprite registry, component library | Conductor, Analyzer | Optional |

## Pipeline Phases

### Phase 1 -- Intelligence (Analyzer + Conductor + User)

#### 1a. Parse Figma Tree

Conductor sends Figma URL to Analyzer. Analyzer returns:

```
Input: Figma URL (frame or component)
Tools: figma_get_frame, figma_export_images, figma_list_frames
Output: 
  - Node tree (structured)
  - Reference PNG (@2x)
  - Frame dimensions
```

#### 1b. Pattern Recognition

Analyzer identifies:

- **Repeated structures** -- N identical children with same component type -> "5x TaskBlock -- likely a list"
- **INSTANCE/COMPONENT nodes** -- reusable Figma components -> potential nested prefab candidates
- **Leaf elements** -- icons (small VECTOR/RECTANGLE), buttons (with text children), labels
- **Layout containers** -- nodes with `autoLayout` -> VerticalLayoutGroup/HorizontalLayoutGroup
- **Visual layers** -- background, border, shadow, content (z-order matters)
- **Dynamic elements** -- progress bars (filled images), toggles, sliders

#### 1c. Sprite Inventory

Analyzer checks what already exists:

1. Scan project: `manage_asset search filter_type=Sprite path=Assets/`
2. Check Obsidian vault: `search-vault query="sprite registry" vault="<project>"`
3. For each Figma icon/image node:
   - Match by name against existing sprites
   - Mark as "found in project", "found in Obsidian registry", or "needs export"

#### 1d. Conductor Presents Plan to User

Structured summary with questions:

```
Screen: [name] ([figma_width]x[figma_height] -> [target_width]x[target_height])

Blocks identified:
  1. [BlockName] -- [description] ([width]x[height])
  2. [BlockName] -- [description]
  ...

Patterns detected:
  - [N]x [ComponentName] -- repeated structure. Is this a dynamic list 
    (build one template, clone at runtime)?
  - [ComponentName] is a Figma INSTANCE -- make it a reusable nested prefab?

Sprites needed:  [icon1] [icon2] [icon3]
Sprites found:   [icon1 found at Assets/Sprites/UI/crystal.png]
Sprites missing: [icon2 -- need to export from Figma or provide manually]

Questions:
  - [PatternName] repeats [N] times -- template + cloning or static?
  - [ElementName] -- static image or dynamic fill (progress bar)?
  - Target resolution: [1920x1080]?
  - Adaptation strategy: scale proportionally / maintain aspect / custom?
```

User approves, corrects, or asks to re-analyze.
Conductor does NOT proceed until user confirms.

### Phase 2 -- Preparation (Analyzer + Builder + User)

#### 2a. Export Missing Sprites

For sprites not found in project:
1. If Figma node ID available -> Analyzer calls `figma_export_images` (PNG @2x)
2. If node ID unavailable -> Conductor asks user to export from Figma manually
3. Sprites saved to project folder (convention: `Assets/Project/Sprites/UI/`)

#### 2b. Configure Import Settings

Builder or Conductor configures each sprite:
```
manage_asset action=modify path=[sprite] 
  properties={textureType: Sprite, spriteMode: Single, mipmapEnabled: false}
```

#### 2c. Font Check

Analyzer extracts font families from Figma tree.
Conductor checks project for matching TMP Font Assets.
If missing -> informs user: "Font [X] not found. Import [X].ttf and create TMP Font Asset for accuracy. Using default for now."

### Phase 3 -- Modular Build (Builder + QA + Conductor + User)

This is the core loop. For each block identified in Phase 1:

#### 3a. Conductor Dispatches Build

Conductor sends Builder precise specs for one module:
```
Module: TaskRow
Size: 380x60
Background: Panel, color #CB84FB40, rounded (ppum 8.3)
Children:
  - CrystalIcon: Image, 28x28, anchor middle-left, offset (8,0), sprite Crystal.png, tint #FFFFFF80
  - RewardAmount: Text "20", 16px Bold, #FFFFFF, anchor middle-left, offset (38,0)
  - TaskDesc: Text "Collect 12 stars", 13px, #FFFFFF, anchor middle-center, offset (10,6)
  - ProgressBar: Panel 230x16, anchor bottom-center, color #30154D99
    - Fill: Image 172x10, anchor left, color #FFC700
    - StarIcon: Image 24x24, anchor right, sprite CommonIconStar.png
    - ProgressText: Text "9/12", 10px Bold, #FFFFFF, center
```

Builder executes element-by-element:
```
create_prefab_ui -> add_ui_element -> set_rect_transform -> set_ui_style
  (repeat for each child)
-> save_prefab
```

#### 3b. QA Validates Module

Conductor asks QA to validate:
1. QA instantiates prefab under a Canvas in scene
2. Takes screenshot via `manage_camera(action="screenshot")`
3. Compares against cropped region of Figma reference PNG
4. Returns structured diff:

```
Validation: TaskRow
Overall: 78% match

Issues:
  1. ProgressBar.Fill -- width 172px but should be 172px relative to bar (OK actually)
  2. CrystalIcon -- not visible (sprite not assigned? check asset path)
  3. RewardAmount -- positioned 2px too low
  4. Background -- corner radius too sharp (ppum should be 7.5 not 8.3)
  
Matching:
  - Colors: all correct
  - Text content: correct
  - Overall layout structure: correct
```

#### 3c. Fix Loop (Builder + QA)

Conductor interprets QA feedback and sends fixes to Builder:
```
Conductor -> Builder: "Fix CrystalIcon sprite path, shift RewardAmount up 2px, change bg ppum to 7.5"
Conductor -> QA: "re-validate"
```

Loop continues until QA reports >= 95% match.

#### 3d. User Approval

Conductor presents to user:
- Screenshot of built module
- Side-by-side with Figma reference
- "This module is ready. Approve?"

User approves -> next module.
User rejects -> Conductor asks what's wrong, sends corrections to Builder.

#### Module Build Order (bottom-up)

1. **Leaf components** -- buttons, badges, icons, progress bars
2. **Composite components** -- TaskRow (contains badge + text + bar)
3. **Containers** -- TaskList (contains N x TaskRow), CTA banner, header
4. **Full screen** -- compose all containers with correct layout

### Phase 4 -- Composition (Builder + QA + User)

1. Builder creates screen-level prefab
2. Adds validated modules as children (or nested prefab references)
3. Sets overall layout (anchors, positions relative to screen)
4. QA does full-screen screenshot vs full Figma reference PNG
5. Conductor presents to user for final approval

### Phase 5 -- Knowledge Update (Conductor + Obsidian)

#### Obsidian Sprite Registry

Create/update note in project vault:

```markdown
# Sprite Registry

| Sprite | Asset Path | Source | Size | Figma Node |
|--------|-----------|--------|------|------------|
| Crystal | Assets/Sprites/UI/Crystal.png | My Perfect Hotel UI | 120x120 | -- |
| CommonIconStar | Assets/Sprites/UI/Star.png | My Perfect Hotel UI | 77x80 | -- |
```

#### Obsidian Component Library

```markdown
# UI Component Library

| Component | Prefab Path | Source Screen | Reusable |
|-----------|------------|--------------|----------|
| TaskRow | Assets/UI/Prefabs/TaskRow.prefab | ProgressOffer | Yes (template) |
| BuyButton | Assets/UI/Prefabs/BuyButton.prefab | ProgressOffer | Yes |
| RibbonBanner | Assets/UI/Prefabs/RibbonBanner.prefab | ProgressOffer | Yes |
```

## Implementation as OpenCode Skill

### Skill Structure

```
skills/
  figma-to-unity/
    SKILL.md              -- main skill instructions (Conductor behavior)
    prompts/
      analyzer.md         -- system prompt for Analyzer agent
      builder.md          -- system prompt for Builder agent  
      qa.md               -- system prompt for QA agent
    reference/
      figma-to-ugui-mapping.md   -- how Figma properties map to uGUI
      ppum-calculator.md         -- corner radius to PPUM conversion
      anchor-mapping.md          -- Figma constraints to Unity anchors
```

### Agent Implementation

In OpenCode, agents map to **Task tool with subagent_type**:

- Conductor = the main skill (runs in primary context)
- Analyzer = `Task(subagent_type="explore", prompt="[analyzer system prompt] + [figma URL]")`
- Builder = `Task(subagent_type="general", prompt="[builder system prompt] + [build specs]")`
- QA = `Task(subagent_type="general", prompt="[qa system prompt] + [screenshot] + [reference]")`

Conductor maintains state between agent calls and manages user interaction.

### Skill Trigger

```
Use when: user provides a Figma URL and wants to build Unity UI from it
Trigger phrases: "build UI from Figma", "convert Figma to Unity", Figma URL in message
Required: figma-mcp and unity-ui-mcp servers connected
```

## Key Design Decisions

### Why agent team, not single agent?

1. **Separation of concerns** -- Analyzer interprets Figma, Builder operates Unity, QA validates visually
2. **Independent validation** -- Builder can't grade its own work. QA provides unbiased assessment.
3. **Reduced context bloat** -- each agent only loads tools it needs. Analyzer doesn't need Unity UI MCP.
4. **Parallel potential** -- Analyzer can parse next module while Builder constructs current one
5. **Matches Anthropic's Orchestrator-Workers + Evaluator-Optimizer patterns**

### Why Conductor as single point of contact?

1. User has ONE conversation partner, not three
2. All decisions funnel through one place -- easier to maintain consistency
3. Conductor can prioritize: "this is good enough" vs "needs more iteration"
4. Conductor manages Obsidian knowledge -- single source of truth

### Why element-by-element, not JSON blob?

`build_ui_from_json` failed on complex layouts in our testing (VerticalLayout didn't apply, sprites didn't bind, elements collapsed to single point). Element-by-element API (`add_ui_element` + `set_rect_transform` + `set_ui_style`) gives full control and allows per-element validation.

### Why human-in-the-loop?

1. AI vision can't reliably judge 100% fidelity from screenshot comparison alone
2. User knows domain intent -- "this is a dynamic list" vs "these are 5 static items"
3. Sprite matching requires project knowledge -- "crystal icon is already in the project as gem.png"
4. User catches issues early, before they compound across modules
5. Builds trust -- user sees progress incrementally

### Resolution Adaptation

Figma designs may target different resolutions than Unity project.

Strategy:
- Extract **ratios** from Figma (element_size / frame_size)
- Get target from `get_editor_config` (target_screen)
- Apply ratios to target resolution
- Ask user to confirm: "Figma is 1242x2688 (mobile). Target is 1920x1080. Scale proportionally?"

## Success Criteria

- Each module screenshot vs Figma fragment: < 2px deviation on positions, exact color match
- Unity hierarchy matches Figma's logical structure (names, nesting)
- All sprites correctly assigned (no missing/white/pink images)
- User confirms each module before proceeding
- Sprite registry and component library updated in Obsidian after completion
- Full screen composition matches reference at > 95% visual similarity

## Open Questions

- Should modules be saved as separate nested prefabs or inline in the screen prefab?
- How to handle Figma effects (drop shadows, blurs) that don't map cleanly to uGUI?
- Should the skill auto-detect which MCP servers are available and degrade gracefully?
- How to handle Figma components that are used across multiple screens (shared library)?
- Maximum iteration count for QA loop before escalating to user?
