# Placement Preview System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Preview system for placement tools: textured semi-transparent ghost with x-ray silhouette + dashed outline through occluders, and honest brush-cell preview with radius ring for all brush tools.

**Architecture:** Per spec `docs/superpowers/specs/2026-06-11-placement-preview-system-design.md`. Two-pass ghost shader (visible textured + ZTest Greater fill), URP 17 RenderGraph ScriptableRendererFeature for the dashed occlusion outline (PC renderer only), `PlacementGhostService` extracted from `PlacementTool`, unified `BrushPreviewService` driven by exact per-tool cell predicates shared between preview and apply.

**Tech Stack:** Unity 6 (6000.x), URP 17 (RenderGraph API), VContainer DI, R3, NUnit (Unity Test Framework, EditMode). Project root: `D:\UnityProjects\wheat-farm`. All gameplay code in `Assets/Scripts` (namespaces `WheatFarm.*`, one asmdef per feature folder). **Unity Editor has the project open — use Unity MCP tools (`read_console`, `run_tests`, `manage_editor`, `manage_graphics`) to compile-check, run tests, add the layer, and add the renderer feature. After every script change wait for compilation and check the console for errors.**

**Verification loop for every task:** after editing scripts → wait until `mcpforunity://editor/state` shows `is_compiling == false` → `read_console(types=["error"])` must be empty → run tests via `run_tests(mode="EditMode")` → commit.

---

## File Map

| File | Action | Responsibility |
|---|---|---|
| `Assets/Scripts/Tests/EditMode/WheatFarm.Tests.EditMode.asmdef` | Create | EditMode test assembly |
| `Assets/Scripts/Features/Farming/BrushPredicates.cs` | Create | Pure static per-tool cell predicates (single source of truth) |
| `Assets/Scripts/Tests/EditMode/BrushPredicatesTests.cs` | Create | Predicate unit tests |
| `Assets/Scripts/Features/Farming/BrushService.cs` | Modify | `GetAffectableCells` (Unlocked only) + `CanApply` gate in apply |
| `Assets/Scripts/Tests/EditMode/BrushServiceTests.cs` | Create | Preview/apply parity contract test |
| `Assets/Scripts/Player/Tools/*.cs` (7 tools) | Modify | Implement `CanApply` via `BrushPredicates` |
| `Assets/Project/Shaders/GhostPreview.shader` | Create | 2-pass ghost (visible textured + ZTest Greater fill) |
| `Assets/Scripts/Player/Preview/GhostMaterialFactory.cs` | Create | Ghost material creation + cache |
| `Assets/Scripts/Tests/EditMode/GhostMaterialFactoryTests.cs` | Create | Property copy + cache tests |
| `Assets/Scripts/Player/Preview/PlacementGhostService.cs` | Create | Ghost lifecycle (Show/UpdatePose/SetValid/Hide) |
| `Assets/Scripts/Player/Tools/PlacementTool.cs` | Modify | Delegate ghost to service; brush preview source |
| `Assets/Project/Shaders/BrushCellPreview.shader` | Create | Unlit transparent cell quad |
| `Assets/Project/Shaders/BrushRing.shader` | Create | Ring SDF for brush radius |
| `Assets/Scripts/Player/Preview/BrushPreviewService.cs` | Create | Per-frame instanced cell quads + ring + footprint |
| `Assets/Scripts/Player/FarmInteractionController.cs` | Modify | Generalized per-frame preview dispatch, UI-hover hiding |
| `Assets/Scripts/Infrastructure/Scopes/FarmScope.cs` | Modify | Register new services |
| `Assets/Scripts/Rendering/WheatFarm.Rendering.asmdef` | Create | Asmdef referencing URP runtime |
| `Assets/Scripts/Rendering/GhostOutlineRenderFeature.cs` | Create | RenderGraph mask + composite passes |
| `Assets/Project/Shaders/GhostOcclusionMask.shader` | Create | Override material: white, ZTest Greater |
| `Assets/Project/Shaders/GhostOutlineComposite.shader` | Create | Fullscreen: dashed edge + soft fill |
| `Assets/Project/Settings/PC_Renderer.asset` | Modify | Add GhostOutlineRenderFeature (NOT Mobile_Renderer) |
| ProjectSettings TagManager | Modify | Add layer `PlacementPreview` (via `manage_editor add_layer`) |

Spec predicates (exact, from spec §4 «Распределение фильтров»):

- planting: `!Occupied && !HasPlant`
- paths: `!HasPlant && (!Occupied || GroundState >= PathStone) && GroundState != targetPathState`
- bulldoze: `GroundState >= PathStone || HasPlant`
- sickle: `IsHarvestable`
- uproot: `HasPlant`
- water/fertilizer/dye: `HasPlant`

---

### Task 1: EditMode test assembly

**Files:**
- Create: `Assets/Scripts/Tests/EditMode/WheatFarm.Tests.EditMode.asmdef`

- [ ] **Step 1.1: Create the asmdef**

```json
{
    "name": "WheatFarm.Tests.EditMode",
    "rootNamespace": "WheatFarm.Tests",
    "references": [
        "WheatFarm.Core",
        "WheatFarm.Farming",
        "WheatFarm.Buildings",
        "WheatFarm.Inventory",
        "WheatFarm.Player",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "R3.Unity"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll", "R3.dll"],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "versionDefines": [],
    "noEngineReferences": false
}
```

> R3 delivery (verified): UPM asmdef `R3.Unity` (`com.cysharp.r3`) + NuGetForUnity precompiled DLL `Assets/Packages/R3.1.3.0/lib/netstandard2.1/R3.dll`. Keep `R3.Unity` in `references` and `R3.dll` in `precompiledReferences`; there is NO asmdef named `R3` — do not reference it. `WheatFarm.Farming` exposes R3 types in public APIs (`ReactiveProperty<BrushSize>`), so the test assembly needs both. Verify with a trivial smoke test (`Assert.Pass()`) that the assembly compiles and appears in Test Runner.

- [ ] **Step 1.2: Verify compilation + test discovery**

Wait for compile, `read_console(types=["error"])` → empty. `run_tests(mode="EditMode")` → finishes (0 tests is fine if no smoke test added).

- [ ] **Step 1.3: Commit**

```bash
git add Assets/Scripts/Tests
git commit -m "test: add EditMode test assembly"
```

---

### Task 2: BrushPredicates — pure per-tool cell filters (TDD)

**Files:**
- Create: `Assets/Scripts/Features/Farming/BrushPredicates.cs`
- Test: `Assets/Scripts/Tests/EditMode/BrushPredicatesTests.cs`

- [ ] **Step 2.1: Write failing tests**

```csharp
using NUnit.Framework;
using WheatFarm.Farming;

namespace WheatFarm.Tests
{
    public class BrushPredicatesTests
    {
        private static SubCellState Cell(
            string plantId = null, float growth = 0f,
            GroundState ground = GroundState.Grass, bool occupied = false)
        {
            var c = SubCellState.Empty;
            c.PlantId = plantId;
            c.Growth = growth;
            c.GroundState = ground;
            c.Occupied = occupied;
            return c;
        }

        [Test] public void Plantable_EmptyGrass_True() =>
            Assert.IsTrue(BrushPredicates.Plantable(Cell()));

        [Test] public void Plantable_Occupied_False() =>
            Assert.IsFalse(BrushPredicates.Plantable(Cell(occupied: true)));

        [Test] public void Plantable_HasPlant_False() =>
            Assert.IsFalse(BrushPredicates.Plantable(Cell(plantId: "wheat")));

        [Test] public void PathPaintable_EmptyGrass_True() =>
            Assert.IsTrue(BrushPredicates.PathPaintable(Cell(), GroundState.PathStone));

        [Test] public void PathPaintable_RepaintOtherPathType_True_DespiteOccupied()
        {
            // Path cells are Occupied — repaint must still work (fixes latent bug)
            var cell = Cell(ground: GroundState.PathWood, occupied: true);
            Assert.IsTrue(BrushPredicates.PathPaintable(cell, GroundState.PathStone));
        }

        [Test] public void PathPaintable_SamePathType_False()
        {
            var cell = Cell(ground: GroundState.PathStone, occupied: true);
            Assert.IsFalse(BrushPredicates.PathPaintable(cell, GroundState.PathStone));
        }

        [Test] public void PathPaintable_OccupiedByBuilding_False() =>
            Assert.IsFalse(BrushPredicates.PathPaintable(Cell(occupied: true), GroundState.PathStone));

        [Test] public void PathPaintable_HasPlant_False() =>
            Assert.IsFalse(BrushPredicates.PathPaintable(Cell(plantId: "wheat"), GroundState.PathStone));

        [Test] public void Bulldozable_PathCell_True_DespiteOccupied() =>
            Assert.IsTrue(BrushPredicates.Bulldozable(Cell(ground: GroundState.PathBrick, occupied: true)));

        [Test] public void Bulldozable_CropCell_True() =>
            Assert.IsTrue(BrushPredicates.Bulldozable(Cell(plantId: "wheat")));

        [Test] public void Bulldozable_EmptyGrass_False() =>
            Assert.IsFalse(BrushPredicates.Bulldozable(Cell()));

        [Test] public void Bulldozable_OccupiedNoPathNoPlant_False() =>
            Assert.IsFalse(BrushPredicates.Bulldozable(Cell(occupied: true)));

        [Test] public void Harvestable_GrownPlant_True() =>
            Assert.IsTrue(BrushPredicates.Harvestable(Cell(plantId: "wheat", growth: 1f)));

        [Test] public void Harvestable_YoungPlant_False() =>
            Assert.IsFalse(BrushPredicates.Harvestable(Cell(plantId: "wheat", growth: 0.5f)));

        [Test] public void PlantTargeting_HasPlant_True() =>
            Assert.IsTrue(BrushPredicates.PlantTargeting(Cell(plantId: "wheat")));

        [Test] public void PlantTargeting_NoPlant_False() =>
            Assert.IsFalse(BrushPredicates.PlantTargeting(Cell()));
    }
}
```

- [ ] **Step 2.2: Run tests — expect compile FAIL** (`BrushPredicates` does not exist). `run_tests(mode="EditMode")` or check console errors.

- [ ] **Step 2.3: Implement**

```csharp
namespace WheatFarm.Farming
{
    /// <summary>
    /// Per-tool cell applicability predicates — the single source of truth shared
    /// by brush application (BrushService) and brush preview (BrushPreviewService),
    /// so the preview never lies about which cells a stroke will change.
    /// Exact predicates per spec docs/superpowers/specs/2026-06-11-placement-preview-system-design.md §4.
    /// </summary>
    public static class BrushPredicates
    {
        public static bool Plantable(in SubCellState cell) =>
            !cell.Occupied && !cell.HasPlant;

        public static bool PathPaintable(in SubCellState cell, GroundState targetPath) =>
            !cell.HasPlant
            && (!cell.Occupied || cell.GroundState >= GroundState.PathStone)
            && cell.GroundState != targetPath;

        public static bool Bulldozable(in SubCellState cell) =>
            cell.GroundState >= GroundState.PathStone || cell.HasPlant;

        public static bool Harvestable(in SubCellState cell) => cell.IsHarvestable;

        /// <summary>Uproot, water, fertilize, dye — any cell with a plant.</summary>
        public static bool PlantTargeting(in SubCellState cell) => cell.HasPlant;
    }
}
```

- [ ] **Step 2.4: Run tests — expect ALL PASS.**

- [ ] **Step 2.5: Commit**

```bash
git add Assets/Scripts/Features/Farming/BrushPredicates.cs Assets/Scripts/Tests
git commit -m "feat: per-tool brush cell predicates (shared preview/apply filters)"
```

---

### Task 3: BrushService refactor — shared enumeration + CanApply gate (TDD)

**Files:**
- Modify: `Assets/Scripts/Features/Farming/BrushService.cs`
- Modify: 7 tool files in `Assets/Scripts/Player/Tools/`
- Test: `Assets/Scripts/Tests/EditMode/BrushServiceTests.cs`

- [ ] **Step 3.1: Write failing parity test**

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WheatFarm.Farming;

namespace WheatFarm.Tests
{
    /// <summary>Records applied cells; CanApply = Bulldozable (the predicate that
    /// exposes the old Occupied-skip bug, since path cells are Occupied).</summary>
    internal class RecordingBulldozeAction : IBrushAction
    {
        public readonly List<(Vector2Int chunk, int x, int y)> Applied = new();

        public bool CanApply(ChunkData chunk, int cellX, int cellY) =>
            BrushPredicates.Bulldozable(chunk.Cells[chunk.CellIndex(cellX, cellY)]);

        public void Apply(ChunkData chunk, int cellX, int cellY) =>
            Applied.Add((chunk.ChunkCoord, cellX, cellY));
    }

    public class BrushServiceTests
    {
        private ChunkSystem _chunks;
        private BrushService _brush;

        [SetUp]
        public void SetUp()
        {
            _chunks = new ChunkSystem(chunkWorldSize: 4f, subCellResolution: 8);
            _chunks.TryUnlockChunk(Vector2Int.zero);
            _brush = new BrushService(_chunks);
            _brush.CurrentSize.Value = BrushSize.Medium;
        }

        [TearDown]
        public void TearDown()
        {
            _brush.Dispose();
            _chunks.Dispose();
        }

        private void SetCell(int x, int y, GroundState ground, bool occupied, string plantId = null)
        {
            var chunk = _chunks.GetChunk(Vector2Int.zero);
            int idx = chunk.CellIndex(x, y);
            chunk.Cells[idx].GroundState = ground;
            chunk.Cells[idx].Occupied = occupied;
            chunk.Cells[idx].PlantId = plantId;
        }

        [Test]
        public void Apply_ReachesOccupiedPathCells()
        {
            // Regression: path cells set Occupied=true; old code skipped them entirely.
            SetCell(4, 4, GroundState.PathStone, occupied: true);
            var action = new RecordingBulldozeAction();
            var center = _chunks.CellToWorld(Vector2Int.zero, 4, 4);

            _brush.ApplyAtWorldPos(center, action);

            Assert.Contains((Vector2Int.zero, 4, 4), action.Applied);
        }

        [Test]
        public void Apply_SkipsCellsWhereCanApplyFalse()
        {
            // Empty grass: Bulldozable == false everywhere
            var action = new RecordingBulldozeAction();
            _brush.ApplyAtWorldPos(_chunks.CellToWorld(Vector2Int.zero, 4, 4), action);
            Assert.IsEmpty(action.Applied);
        }

        [Test]
        public void PreviewAndApply_SameCellSet()
        {
            // Mixed field: paths (occupied), crops, occupied-by-building, empty
            SetCell(3, 4, GroundState.PathWood, occupied: true);
            SetCell(4, 4, GroundState.Grass, occupied: false, plantId: "wheat");
            SetCell(5, 4, GroundState.Grass, occupied: true);   // building — not bulldozable by brush
            var action = new RecordingBulldozeAction();
            var center = _chunks.CellToWorld(Vector2Int.zero, 4, 4);

            var previewSet = _brush.GetAffectableCells(center)
                .Where(c => action.CanApply(c.chunk, c.cellX, c.cellY))
                .Select(c => (c.chunk.ChunkCoord, c.cellX, c.cellY))
                .ToHashSet();

            _brush.ApplyAtWorldPos(center, action);

            CollectionAssert.AreEquivalent(previewSet, action.Applied);
            Assert.IsTrue(previewSet.Contains((Vector2Int.zero, 3, 4)), "path cell in preview");
            Assert.IsTrue(previewSet.Contains((Vector2Int.zero, 4, 4)), "crop cell in preview");
            Assert.IsFalse(previewSet.Contains((Vector2Int.zero, 5, 4)), "building cell excluded");
        }

        [Test]
        public void GetAffectableCells_ExcludesLockedChunks()
        {
            // Brush at the chunk edge reaches into the locked neighbor chunk
            var edge = _chunks.CellToWorld(Vector2Int.zero, 7, 4);
            foreach (var (chunk, _, _) in _brush.GetAffectableCells(edge))
                Assert.AreEqual(Vector2Int.zero, chunk.ChunkCoord);
        }
    }
}
```

> If `ChunkSystem.GetCellsInRadius`/`CellToWorld` touch UnityEngine APIs unavailable in EditMode, they don't — they're pure math (verified). `TryUnlockChunk` calls `InitializeChunkMeshProps` which is also pure array math.

- [ ] **Step 3.2: Run tests — expect compile FAIL** (`CanApply` not on `IBrushAction`, `GetAffectableCells` missing).

- [ ] **Step 3.3: Refactor `BrushService.cs`**

Replace `IBrushAction` and `BrushService.ApplyAtWorldPos` (keep `BrushSize`, `IBrushService` members; add `GetAffectableCells` to the interface):

```csharp
    /// <summary>
    /// Action applied to each cell within brush radius.
    /// CanApply is the per-tool cell filter — also used by the brush preview,
    /// so preview and apply always agree (see BrushPredicates).
    /// </summary>
    public interface IBrushAction
    {
        bool CanApply(ChunkData chunk, int cellX, int cellY);
        void Apply(ChunkData chunk, int cellX, int cellY);
    }

    public interface IBrushService
    {
        ReactiveProperty<BrushSize> CurrentSize { get; }
        float WorldRadius { get; }
        void ApplyAtWorldPos(UnityEngine.Vector3 worldPos, IBrushAction action);

        /// <summary>All brush cells in unlocked chunks (no per-tool filtering).</summary>
        IEnumerable<(ChunkData chunk, int cellX, int cellY)> GetAffectableCells(UnityEngine.Vector3 worldPos);
    }
```

```csharp
        public IEnumerable<(ChunkData chunk, int cellX, int cellY)> GetAffectableCells(UnityEngine.Vector3 worldPos)
        {
            var cells = _chunkSystem.GetCellsInRadius(worldPos, WorldRadius);
            foreach (var (chunkCoord, cellX, cellY) in cells)
            {
                var chunk = _chunkSystem.GetChunk(chunkCoord);
                if (chunk == null || !chunk.Unlocked) continue;
                yield return (chunk, cellX, cellY);
            }
        }

        public void ApplyAtWorldPos(UnityEngine.Vector3 worldPos, IBrushAction action)
        {
            foreach (var (chunk, cellX, cellY) in GetAffectableCells(worldPos))
            {
                if (!action.CanApply(chunk, cellX, cellY)) continue;
                action.Apply(chunk, cellX, cellY);
                chunk.Dirty = true;
            }
        }
```

Note: the old blanket `if (Cells[idx].Occupied) continue;` is gone — that's the point.

- [ ] **Step 3.4: Add `CanApply` to all 7 IBrushAction tools**

Each tool gets a `CanApply` using `BrushPredicates`. Cell-state guards already inside `Apply` bodies may stay (harmless), but remove ones that contradict the new predicates:

- `WateringCanTool`, `FertilizerTool`, `DyeTool`, `UprootTool`:
```csharp
        public bool CanApply(ChunkData chunk, int cellX, int cellY) =>
            BrushPredicates.PlantTargeting(chunk.Cells[chunk.CellIndex(cellX, cellY)]);
```
- `SickleTool`:
```csharp
        public bool CanApply(ChunkData chunk, int cellX, int cellY) =>
            BrushPredicates.Harvestable(chunk.Cells[chunk.CellIndex(cellX, cellY)]);
```
- `BulldozeTool`:
```csharp
        public bool CanApply(ChunkData chunk, int cellX, int cellY) =>
            BrushPredicates.Bulldozable(chunk.Cells[chunk.CellIndex(cellX, cellY)]);
```
- `PlacementTool` — dispatch by mode; add a helper for the selected path state and reuse it in `ApplyPath`:
```csharp
        private GroundState SelectedPathState => _selectedPlaceable.PathSubtype switch
        {
            1 => GroundState.PathWood,
            2 => GroundState.PathBrick,
            _ => GroundState.PathStone
        };

        public bool CanApply(ChunkData chunk, int cellX, int cellY)
        {
            ref readonly var cell = ref chunk.Cells[chunk.CellIndex(cellX, cellY)];
            if (_selectedPlaceable != null && _selectedPlaceable.Category == PlaceableCategory.Path)
                return BrushPredicates.PathPaintable(cell, SelectedPathState);
            if (_selectedPlant != null)
                return BrushPredicates.Plantable(cell);
            return false;
        }
```
  In `PlacementTool.Apply`, delete the now-duplicated checks (`HasPlant || Occupied` early returns and the `GroundState == pathState` skip) — `CanApply` covers them; keep the seed-availability check (resource, not cell state). In `ApplyPath`, replace the inline `switch` with `SelectedPathState` and keep `cell.Occupied = true;` + GPU sync + `UpdateGroundNeighborFlags`.

- [ ] **Step 3.5: Compile-check + run tests — expect ALL PASS** (Task 2 + Task 3 tests).

- [ ] **Step 3.6: Manual sanity via MCP** — enter play mode (`manage_editor play`), paint a stone path, switch to brick, repaint over it (was impossible before), bulldoze it. `read_console` for errors. Stop play mode.

- [ ] **Step 3.7: Commit**

```bash
git add Assets/Scripts/Features/Farming Assets/Scripts/Player/Tools Assets/Scripts/Tests
git commit -m "refactor: shared brush cell enumeration + per-tool CanApply filters

Fixes latent bug: path cells set Occupied=true, so brush bulldoze and
path repaint never reached them through the old blanket Occupied skip."
```

---

### Task 4: GhostPreview shader + GhostMaterialFactory (TDD)

**Files:**
- Create: `Assets/Project/Shaders/GhostPreview.shader`
- Create: `Assets/Scripts/Player/Preview/GhostMaterialFactory.cs`
- Test: `Assets/Scripts/Tests/EditMode/GhostMaterialFactoryTests.cs`

- [ ] **Step 4.1: Create the shader**

```shaderlab
Shader "WheatFarm/GhostPreview"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _ValidityTint ("Validity Tint", Color) = (0.2, 0.9, 0.2, 1)
        _Alpha ("Visible Alpha", Range(0,1)) = 0.55
        _OccludedAlpha ("Occluded Fill Alpha", Range(0,1)) = 0.35
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
        }

        // Pass 1: occluded part — flat highlight fill, visible through anything
        Pass
        {
            Name "OccludedFill"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Greater
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ValidityTint;
                half _Alpha;
                half _OccludedAlpha;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings  { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return half4(_ValidityTint.rgb, _OccludedAlpha);
            }
            ENDHLSL
        }

        // Pass 2: visible part — textured semi-transparent, tinted by validity
        Pass
        {
            Name "Visible"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ValidityTint;
                half _Alpha;
                half _OccludedAlpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                Light mainLight = GetMainLight();
                half ndl = saturate(dot(normalize(IN.normalWS), mainLight.direction));
                half3 lit = tex.rgb * _BaseColor.rgb * (ndl * mainLight.color + 0.45);
                half3 tinted = lerp(lit, lit * _ValidityTint.rgb, 0.6);
                return half4(tinted, _Alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
```

- [ ] **Step 4.2: Write failing factory tests**

```csharp
using NUnit.Framework;
using UnityEngine;
using WheatFarm.Player.Preview;

namespace WheatFarm.Tests
{
    public class GhostMaterialFactoryTests
    {
        private GhostMaterialFactory _factory;
        private Material _source;

        [SetUp]
        public void SetUp()
        {
            _factory = new GhostMaterialFactory();
            _source = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _source.SetColor("_BaseColor", new Color(0.3f, 0.5f, 0.7f, 1f));
            _source.SetTexture("_BaseMap", Texture2D.whiteTexture);
        }

        [TearDown]
        public void TearDown()
        {
            _factory.Dispose();
            Object.DestroyImmediate(_source);
        }

        [Test]
        public void Get_CopiesBaseMapAndColor()
        {
            var ghost = _factory.Get(_source);
            Assert.AreEqual("WheatFarm/GhostPreview", ghost.shader.name);
            Assert.AreEqual(Texture2D.whiteTexture, ghost.GetTexture("_BaseMap"));
            Assert.AreEqual(new Color(0.3f, 0.5f, 0.7f, 1f), ghost.GetColor("_BaseColor"));
        }

        [Test]
        public void Get_SameSource_ReturnsCachedInstance()
        {
            Assert.AreSame(_factory.Get(_source), _factory.Get(_source));
        }

        [Test]
        public void Get_SourceWithoutBaseMap_DoesNotThrow()
        {
            var bare = new Material(Shader.Find("Sprites/Default"));
            Assert.DoesNotThrow(() => _factory.Get(bare));
            Object.DestroyImmediate(bare);
        }
    }
}
```

- [ ] **Step 4.3: Run tests — expect compile FAIL** (`GhostMaterialFactory` missing).

- [ ] **Step 4.4: Implement the factory**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace WheatFarm.Player.Preview
{
    /// <summary>
    /// Creates ghost materials from source materials, preserving the original
    /// texture and base color so the ghost looks like the object, not a flat blob.
    /// Caches per source material; validity tint is applied per-renderer via
    /// MaterialPropertyBlock, so cached materials never need mutation.
    /// </summary>
    public class GhostMaterialFactory : IDisposable
    {
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly Dictionary<Material, Material> _cache = new();
        private Shader _ghostShader;

        public Material Get(Material source)
        {
            if (_cache.TryGetValue(source, out var cached) && cached != null)
                return cached;

            if (_ghostShader == null)
                _ghostShader = Shader.Find("WheatFarm/GhostPreview");

            var ghost = new Material(_ghostShader);
            if (source.HasProperty(BaseMapId))
                ghost.SetTexture(BaseMapId, source.GetTexture(BaseMapId));
            if (source.HasProperty(BaseColorId))
                ghost.SetColor(BaseColorId, source.GetColor(BaseColorId));

            _cache[source] = ghost;
            return ghost;
        }

        public void Dispose()
        {
            foreach (var mat in _cache.Values)
            {
                if (mat == null) continue;
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(mat);
                else
                    UnityEngine.Object.DestroyImmediate(mat); // EditMode tests dispose outside play mode
            }
            _cache.Clear();
        }
    }
}
```

- [ ] **Step 4.5: Run tests — expect ALL PASS.**

- [ ] **Step 4.6: Commit**

```bash
git add Assets/Project/Shaders/GhostPreview.shader* Assets/Scripts/Player/Preview Assets/Scripts/Tests
git commit -m "feat: ghost preview shader (visible + x-ray fill) and material factory"
```

---

### Task 5: PlacementPreview layer + PlacementGhostService + PlacementTool refactor

**Files:**
- Create: `Assets/Scripts/Player/Preview/PlacementGhostService.cs`
- Modify: `Assets/Scripts/Player/Tools/PlacementTool.cs`
- Modify: `Assets/Scripts/Infrastructure/Scopes/FarmScope.cs`

- [ ] **Step 5.1: Add the layer via MCP**

`manage_editor(action="add_layer", layer_name="PlacementPreview")`. Verify via `mcpforunity://project/info` or re-running (idempotent error is fine).

> Spec's "exclude layer from physics raycasts" is defensive only: ghost colliders are destroyed, brush quads have no colliders, and `FarmInteractionController`'s building-click `Physics.Raycast` is maskless. Consciously dropped — do NOT invent colliders or masks for preview objects.

- [ ] **Step 5.2: Implement `PlacementGhostService`**

```csharp
using UnityEngine;

namespace WheatFarm.Player.Preview
{
    public interface IPlacementGhostService
    {
        void Show(GameObject prefab);
        void UpdatePose(Vector3 position, float rotationY);
        void SetValid(bool valid);
        void SetVisible(bool visible);
        void Hide();
    }

    /// <summary>
    /// Owns the placement ghost instance: strips physics/scripts, swaps materials
    /// to GhostPreview (textures preserved), applies validity tint via
    /// MaterialPropertyBlock, and feeds _PreviewHighlightColor to the outline feature.
    /// </summary>
    public class PlacementGhostService : IPlacementGhostService, System.IDisposable
    {
        private static readonly int ValidityTintId = Shader.PropertyToID("_ValidityTint");
        private static readonly int PreviewHighlightColorId = Shader.PropertyToID("_PreviewHighlightColor");

        private static readonly Color ValidColor = new(0.2f, 0.9f, 0.2f, 1f);
        private static readonly Color InvalidColor = new(0.9f, 0.2f, 0.2f, 1f);

        private readonly GhostMaterialFactory _materials = new();
        private readonly MaterialPropertyBlock _mpb = new();

        private GameObject _instance;
        private Renderer[] _renderers = System.Array.Empty<Renderer>();
        private bool _valid;

        public void Show(GameObject prefab)
        {
            Hide();
            if (prefab == null) return;

            _instance = Object.Instantiate(prefab);
            _instance.name = $"Ghost_{prefab.name}";

            foreach (var col in _instance.GetComponentsInChildren<Collider>(true))
                Object.Destroy(col);
            foreach (var mb in _instance.GetComponentsInChildren<MonoBehaviour>(true))
                Object.Destroy(mb);

            int layer = LayerMask.NameToLayer("PlacementPreview");
            foreach (var t in _instance.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;

            _renderers = _instance.GetComponentsInChildren<Renderer>(true);
            foreach (var r in _renderers)
            {
                var src = r.sharedMaterials;
                var mats = new Material[src.Length];
                for (int i = 0; i < src.Length; i++)
                    mats[i] = _materials.Get(src[i]);
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            _valid = false;
            ApplyTint(InvalidColor);
        }

        public void UpdatePose(Vector3 position, float rotationY)
        {
            if (_instance == null) return;
            _instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0, rotationY, 0));
        }

        public void SetValid(bool valid)
        {
            if (_instance == null || valid == _valid) return;
            _valid = valid;
            ApplyTint(valid ? ValidColor : InvalidColor);
        }

        public void SetVisible(bool visible)
        {
            if (_instance != null && _instance.activeSelf != visible)
                _instance.SetActive(visible);
        }

        public void Hide()
        {
            if (_instance != null)
                Object.Destroy(_instance);
            _instance = null;
            _renderers = System.Array.Empty<Renderer>();
        }

        private void ApplyTint(Color color)
        {
            _mpb.Clear();
            _mpb.SetColor(ValidityTintId, color);
            foreach (var r in _renderers)
                if (r != null)
                    r.SetPropertyBlock(_mpb);
            Shader.SetGlobalColor(PreviewHighlightColorId, color);
        }

        public void Dispose()
        {
            Hide();
            _materials.Dispose();
        }
    }
}
```

- [ ] **Step 5.3: Refactor `PlacementTool`**

- Delete fields `_ghostInstance`, `_ghostMaterial`, `_ghostValid`, colors, and methods `CreateGhost`, `SetGhostTint`, `DestroyGhost`.
- Inject `IPlacementGhostService ghost` (new ctor param, store `_ghost`).
- `SelectPlaceable`: `_ghost.Hide(); if (placeable.Category != PlaceableCategory.Path && placeable.Prefab != null) _ghost.Show(placeable.Prefab);`
- `SelectPlant`, `ClearSelection`, `OnUnequip`: `_ghost.Hide();`
- `UpdatePreview`:
```csharp
        public void UpdatePreview(Vector3 cursorWorldPos)
        {
            if (_selectedPlaceable == null || _selectedPlaceable.Category == PlaceableCategory.Path)
                return;
            Vector3 snappedPos = SnapPosition(cursorWorldPos);
            _ghost.UpdatePose(snappedPos, _pendingRotation);
            _ghost.SetValid(_placementService.CanPlace(_selectedPlaceable, cursorWorldPos));
        }
```

- [ ] **Step 5.4: Register in `FarmScope.Configure`** (next to tool registrations):

```csharp
            builder.Register<WheatFarm.Player.Preview.PlacementGhostService>(Lifetime.Singleton)
                .As<WheatFarm.Player.Preview.IPlacementGhostService, System.IDisposable>();
```

- [ ] **Step 5.5: Compile-check, run all tests (PASS), play-mode sanity via MCP:** select a building in the catalog, screenshot (`manage_camera screenshot include_image=true`) — ghost shows textures, tints green/red; walk behind the Mill — occluded part shows flat fill through the building. Stop play.

- [ ] **Step 5.6: Commit**

```bash
git add Assets/Scripts ProjectSettings/TagManager.asset
git commit -m "feat: PlacementGhostService — textured ghost with x-ray fill, extracted from PlacementTool"
```

---

### Task 6: Brush preview — cell quads, radius ring, footprint, controller dispatch

**Files:**
- Create: `Assets/Project/Shaders/BrushCellPreview.shader`, `Assets/Project/Shaders/BrushRing.shader`
- Create: `Assets/Scripts/Player/Preview/BrushPreviewService.cs`
- Modify: `Assets/Scripts/Player/Tools/ITool.cs` (add `IBrushPreviewSource`)
- Modify: tools implementing the source; `FarmInteractionController.cs`; `FarmScope.cs`

- [ ] **Step 6.1: Cell quad shader**

```shaderlab
Shader "WheatFarm/BrushCellPreview"
{
    Properties { _Color ("Color", Color) = (1,1,1,0.5) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+5" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "CellQuad"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Soft border inside each cell so adjacent cells read as a grid
                float2 d = abs(IN.uv - 0.5);
                float border = smoothstep(0.5, 0.42, max(d.x, d.y));
                half pulse = 0.85 + 0.15 * sin(_Time.y * 3.0);
                return half4(_Color.rgb, _Color.a * border * pulse);
            }
            ENDHLSL
        }
    }
}
```

- [ ] **Step 6.2: Ring shader**

```shaderlab
Shader "WheatFarm/BrushRing"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,0.9)
        _Thickness ("Thickness (0-0.5 of radius)", Range(0.01,0.5)) = 0.06
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+6" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Ring"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Thickness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings  { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float r = length(IN.uv - 0.5) * 2.0;          // 0 center, 1 at quad edge
                float ring = smoothstep(_Thickness, _Thickness * 0.5, abs(r - (1.0 - _Thickness)));
                return half4(_Color.rgb, _Color.a * ring);
            }
            ENDHLSL
        }
    }
}
```

- [ ] **Step 6.3: Add `IBrushPreviewSource` to `ITool.cs`**

```csharp
    /// <summary>
    /// Brush tools that want a cell-highlight preview implement this in addition
    /// to IBrushAction. CanApply (from IBrushAction) decides which cells light up.
    /// </summary>
    public interface IBrushPreviewSource
    {
        /// <summary>False disables the preview (e.g. PlacementTool in building-ghost mode).</summary>
        bool PreviewActive { get; }
        /// <summary>Cell highlight color (path tint for paths, tool color otherwise).</summary>
        UnityEngine.Color PreviewCellColor { get; }
    }
```

- [ ] **Step 6.4: Implement the source on tools**

- `WateringCanTool`: `PreviewActive => true;` `PreviewCellColor => new Color(0.3f, 0.6f, 1f, 0.45f);`
- `FertilizerTool`: amber `new Color(0.85f, 0.65f, 0.2f, 0.45f)`
- `DyeTool`: use its current dye color if exposed, else magenta `new Color(0.8f, 0.3f, 0.8f, 0.45f)`
- `SickleTool`: gold `new Color(0.95f, 0.85f, 0.3f, 0.45f)`
- `UprootTool` / `BulldozeTool`: red `new Color(0.9f, 0.25f, 0.2f, 0.45f)`
- `PlacementTool` (inject `FarmRenderConfig config` for path tints):
```csharp
        // Trees are placed singly via TreePlacementService, not the brush — no cell preview for them
        public bool PreviewActive =>
            (_selectedPlant != null && _selectedPlant.Category != PlantCategory.Tree) ||
            (_selectedPlaceable != null && _selectedPlaceable.Category == PlaceableCategory.Path);

        public Color PreviewCellColor
        {
            get
            {
                if (_selectedPlaceable != null && _selectedPlaceable.Category == PlaceableCategory.Path)
                {
                    var prop = _selectedPlaceable.PathSubtype switch
                    {
                        1 => "_TintPathWood",
                        2 => "_TintPathBrick",
                        _ => "_TintPathStone"
                    };
                    var mat = _config != null ? _config.GroundMaterial : null;
                    var c = (mat != null && mat.HasProperty(prop)) ? mat.GetColor(prop) : Color.gray;
                    c.a = 0.55f;
                    return c;
                }
                return new Color(0.2f, 0.9f, 0.2f, 0.4f); // plant mode
            }
        }
```

- [ ] **Step 6.5: Implement `BrushPreviewService`**

```csharp
using System.Collections.Generic;
using UnityEngine;
using WheatFarm.Farming;

namespace WheatFarm.Player.Preview
{
    public interface IBrushPreviewService
    {
        /// <summary>Call every frame while a brush tool is active; draws this frame only.</summary>
        void RenderBrush(Vector3 worldPos, IBrushAction action, Color cellColor);
        /// <summary>One-frame footprint quads (building ghost), colored by validity.</summary>
        void RenderFootprint(Vector3 center, Vector2 worldSize, bool valid);
    }

    /// <summary>
    /// Immediate-mode brush preview: instanced cell quads over exactly the cells
    /// the stroke will change (BrushService enumeration + tool CanApply) plus a
    /// radius ring. Nothing persists — skipping a frame hides the preview.
    /// </summary>
    public class BrushPreviewService : IBrushPreviewService, System.IDisposable
    {
        private const float QuadY = 0.03f; // above ground tiles at y≈0.01

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Color ValidColor = new(0.2f, 0.9f, 0.2f, 0.35f);
        private static readonly Color InvalidColor = new(0.9f, 0.2f, 0.2f, 0.35f);

        private readonly IBrushService _brush;
        private readonly IChunkSystem _chunks;
        private readonly Mesh _quad;
        private readonly Material _cellMaterial;
        private readonly Material _ringMaterial;
        private readonly MaterialPropertyBlock _mpb = new();
        private readonly List<Matrix4x4> _matrices = new(256);
        private readonly int _layer;

        public BrushPreviewService(IBrushService brush, IChunkSystem chunks)
        {
            _brush = brush;
            _chunks = chunks;
            _quad = BuildQuad();
            _cellMaterial = new Material(Shader.Find("WheatFarm/BrushCellPreview"));
            _ringMaterial = new Material(Shader.Find("WheatFarm/BrushRing"));
            _layer = LayerMask.NameToLayer("PlacementPreview");
        }

        public void RenderBrush(Vector3 worldPos, IBrushAction action, Color cellColor)
        {
            _matrices.Clear();
            float cell = _chunks.CellWorldSize;
            var scale = new Vector3(cell, 1f, cell);

            foreach (var (chunk, x, y) in _brush.GetAffectableCells(worldPos))
            {
                if (!action.CanApply(chunk, x, y)) continue;
                var p = _chunks.CellToWorld(chunk.ChunkCoord, x, y);
                p.y = QuadY;
                _matrices.Add(Matrix4x4.TRS(p, Quaternion.identity, scale));
            }

            if (_matrices.Count > 0)
            {
                _mpb.Clear();
                _mpb.SetColor(ColorId, cellColor);
                var rp = new RenderParams(_cellMaterial)
                {
                    layer = _layer,
                    matProps = _mpb,
                    shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                    receiveShadows = false
                };
                Graphics.RenderMeshInstanced(rp, _quad, 0, _matrices);
            }

            // Radius ring (always shown, even over empty cells)
            float d = _brush.WorldRadius * 2f;
            var ringPos = new Vector3(worldPos.x, QuadY + 0.005f, worldPos.z);
            var ringRp = new RenderParams(_ringMaterial)
            {
                layer = _layer,
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false
            };
            Graphics.RenderMesh(ringRp, _quad, 0,
                Matrix4x4.TRS(ringPos, Quaternion.identity, new Vector3(d, 1f, d)));
        }

        public void RenderFootprint(Vector3 center, Vector2 worldSize, bool valid)
        {
            _mpb.Clear();
            _mpb.SetColor(ColorId, valid ? ValidColor : InvalidColor);
            var rp = new RenderParams(_cellMaterial)
            {
                layer = _layer,
                matProps = _mpb,
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false
            };
            var pos = new Vector3(center.x, QuadY, center.z);
            Graphics.RenderMesh(rp, _quad, 0,
                Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(worldSize.x, 1f, worldSize.y)));
        }

        private static Mesh BuildQuad()
        {
            // XZ-plane unit quad centered at origin (built-in Quad faces +Z)
            var m = new Mesh { name = "BrushPreviewQuad" };
            m.vertices = new[]
            {
                new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f),
                new Vector3(-0.5f, 0,  0.5f), new Vector3(0.5f, 0,  0.5f)
            };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            return m;
        }

        public void Dispose()
        {
            if (Application.isPlaying)
            {
                Object.Destroy(_cellMaterial);
                Object.Destroy(_ringMaterial);
                Object.Destroy(_quad);
            }
            else
            {
                Object.DestroyImmediate(_cellMaterial);
                Object.DestroyImmediate(_ringMaterial);
                Object.DestroyImmediate(_quad);
            }
        }
    }
}
```

> Footprint hookup: in `PlacementTool.UpdatePreview` (building mode), after `SetValid`, call
> `_brushPreview.RenderFootprint(snappedPos, FootprintWorldSize(), canPlace)` where
> `FootprintWorldSize()` = `GridSize * _chunkSystem.ChunkWorldSize` for `PlacementLevel.Chunk`
> placeables, else `Vector2.one * _chunkSystem.CellWorldSize`. Inject `IBrushPreviewService` into `PlacementTool`.

- [ ] **Step 6.6: Generalize `FarmInteractionController` preview dispatch**

Replace `HandlePlacementPreview` with:

```csharp
        private void HandlePreview()
        {
            bool overUI = UnityEngine.EventSystems.EventSystem.current != null &&
                          UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            Vector3? hitPoint = overUI ? null : GetGroundHitPoint();

            // Building ghost (Placement tool only)
            if (_placementTool != null && _toolService.CurrentToolId.CurrentValue == ToolId.Placement)
            {
                _ghostService.SetVisible(hitPoint.HasValue);
                if (hitPoint.HasValue)
                    _placementTool.UpdatePreview(hitPoint.Value);
            }

            // Brush cell preview (any tool that is IBrushAction + IBrushPreviewSource)
            if (!hitPoint.HasValue) return;
            var tool = _toolService.CurrentTool.CurrentValue;
            if (tool is IBrushAction action && tool is IBrushPreviewSource src && src.PreviewActive)
                _brushPreview.RenderBrush(hitPoint.Value, action, src.PreviewCellColor);
        }
```

Inject `IPlacementGhostService _ghostService` and `IBrushPreviewService _brushPreview` via `[Inject] Construct(...)`. Call `HandlePreview()` from `Update` in place of `HandlePlacementPreview()`.

- [ ] **Step 6.7: Register in `FarmScope`:**

```csharp
            builder.Register<WheatFarm.Player.Preview.BrushPreviewService>(Lifetime.Singleton)
                .As<WheatFarm.Player.Preview.IBrushPreviewService, System.IDisposable>();
```

- [ ] **Step 6.8: Compile-check, run tests (PASS), play-mode verification via MCP:**
  - Select stone path → cells under brush highlight with stone tint + ring visible; Q/E changes ring size; cells under a building do NOT highlight.
  - Select watering can → blue highlight only on planted cells.
  - Move cursor over a UI panel → preview disappears.
  - Screenshot each state (`include_image=true`), check console for errors/allocs warnings. Stop play.

- [ ] **Step 6.9: Commit**

```bash
git add Assets/Project/Shaders Assets/Scripts
git commit -m "feat: unified brush preview — honest cell highlight, radius ring, building footprint"
```

---

### Task 7: GhostOutlineRenderFeature — dashed occlusion outline (PC only)

**Files:**
- Create: `Assets/Scripts/Rendering/WheatFarm.Rendering.asmdef`
- Create: `Assets/Scripts/Rendering/GhostOutlineRenderFeature.cs`
- Create: `Assets/Project/Shaders/GhostOcclusionMask.shader`
- Create: `Assets/Project/Shaders/GhostOutlineComposite.shader`
- Modify: `Assets/Project/Settings/PC_Renderer.asset` (add feature)

- [ ] **Step 7.1: Asmdef**

```json
{
    "name": "WheatFarm.Rendering",
    "rootNamespace": "WheatFarm.Rendering",
    "references": [
        "Unity.RenderPipelines.Universal.Runtime",
        "Unity.RenderPipelines.Core.Runtime"
    ],
    "autoReferenced": true
}
```

- [ ] **Step 7.2: Mask shader** (override material — white where preview is occluded)

```shaderlab
Shader "Hidden/WheatFarm/GhostOcclusionMask"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "OcclusionMask"
            ZWrite Off
            ZTest Greater
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings  { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target { return half4(1, 1, 1, 1); }
            ENDHLSL
        }
    }
}
```

- [ ] **Step 7.3: Composite shader** (fullscreen — dashed edge + soft fill)

```shaderlab
Shader "Hidden/WheatFarm/GhostOutlineComposite"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "GhostOutlineComposite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _PreviewHighlightColor;
            float _OutlineThickness;   // px
            float _DashDensity;        // stripes per screen diagonal-ish
            float _DashSpeed;
            float _FillStrength;

            float SampleMask(float2 uv) { return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).r; }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 texel = _BlitTexture_TexelSize.xy * _OutlineThickness;

                float m = SampleMask(uv);
                float dil = m;
                dil = max(dil, SampleMask(uv + float2( texel.x, 0)));
                dil = max(dil, SampleMask(uv + float2(-texel.x, 0)));
                dil = max(dil, SampleMask(uv + float2(0,  texel.y)));
                dil = max(dil, SampleMask(uv + float2(0, -texel.y)));
                dil = max(dil, SampleMask(uv + texel * 0.707));
                dil = max(dil, SampleMask(uv - texel * 0.707));
                dil = max(dil, SampleMask(uv + float2(texel.x, -texel.y) * 0.707));
                dil = max(dil, SampleMask(uv + float2(-texel.x, texel.y) * 0.707));

                // Outline ribbon just OUTSIDE the silhouette
                float edge = saturate(dil - m);

                // Marching-ants: animated diagonal stripes in screen space
                float2 px = uv * _ScreenParams.xy;
                float dash = step(0.5, frac((px.x + px.y) / max(_DashDensity, 1.0) + _Time.y * _DashSpeed));

                float alpha = edge * dash + m * _FillStrength;
                return float4(_PreviewHighlightColor.rgb, alpha * _PreviewHighlightColor.a);
            }
            ENDHLSL
        }
    }
}
```

> Note: `_BlitTexture_TexelSize` — if URP's Blit.hlsl version in this project doesn't declare it, declare `float4 _BlitTexture_TexelSize;` and set it from C# (`material.SetVector`) using the mask RT descriptor size. Verify against the actual include on first compile.

- [ ] **Step 7.4: Render feature (RenderGraph, URP 17)**

```csharp
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace WheatFarm.Rendering
{
    /// <summary>
    /// Draws preview-layer objects occluded by scene geometry as a highlighted
    /// silhouette with an animated dashed outline. PC renderer only; the ghost
    /// shader's own ZTest Greater pass is the Mobile fallback.
    /// </summary>
    public class GhostOutlineRenderFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class OutlineSettings
        {
            public LayerMask previewLayer;
            [Range(1f, 8f)] public float outlineThicknessPx = 2.5f;
            [Min(2f)] public float dashDensityPx = 14f;
            public float dashSpeed = 0.8f;
            [Range(0f, 1f)] public float fillStrength = 0.2f;
        }

        public OutlineSettings settings = new();
        [SerializeField] private Shader maskShader;      // Hidden/WheatFarm/GhostOcclusionMask
        [SerializeField] private Shader compositeShader; // Hidden/WheatFarm/GhostOutlineComposite

        private Material _maskMaterial;
        private Material _compositeMaterial;
        private GhostOutlinePass _pass;

        public override void Create()
        {
            if (maskShader == null) maskShader = Shader.Find("Hidden/WheatFarm/GhostOcclusionMask");
            if (compositeShader == null) compositeShader = Shader.Find("Hidden/WheatFarm/GhostOutlineComposite");
            if (maskShader == null || compositeShader == null) return;

            _maskMaterial = CoreUtils.CreateEngineMaterial(maskShader);
            _compositeMaterial = CoreUtils.CreateEngineMaterial(compositeShader);
            _pass = new GhostOutlinePass(_maskMaterial, _compositeMaterial, settings)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null) return;
            if (renderingData.cameraData.cameraType != CameraType.Game) return;
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_maskMaterial);
            CoreUtils.Destroy(_compositeMaterial);
        }

        private class GhostOutlinePass : ScriptableRenderPass
        {
            private static readonly ShaderTagId[] ShaderTags =
            {
                new("UniversalForward"), new("SRPDefaultUnlit"), new("UniversalForwardOnly")
            };

            private static readonly int OutlineThicknessId = Shader.PropertyToID("_OutlineThickness");
            private static readonly int DashDensityId = Shader.PropertyToID("_DashDensity");
            private static readonly int DashSpeedId = Shader.PropertyToID("_DashSpeed");
            private static readonly int FillStrengthId = Shader.PropertyToID("_FillStrength");

            private readonly Material _maskMaterial;
            private readonly Material _compositeMaterial;
            private readonly OutlineSettings _settings;

            public GhostOutlinePass(Material mask, Material composite, OutlineSettings settings)
            {
                _maskMaterial = mask;
                _compositeMaterial = composite;
                _settings = settings;
            }

            private class MaskPassData { public RendererListHandle RendererList; }
            private class CompositePassData { public TextureHandle Mask; public Material Material; }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();
                var renderingData = frameData.Get<UniversalRenderingData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var lightData = frameData.Get<UniversalLightData>();

                // --- Pass A: occlusion mask ---
                var desc = cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                desc.msaaSamples = 1;
                desc.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8_UNorm;
                TextureHandle mask = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph, desc, "_GhostOcclusionMask", true);

                using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>(
                           "Ghost Occlusion Mask", out var passData))
                {
                    var drawSettings = RenderingUtils.CreateDrawingSettings(
                        new System.Collections.Generic.List<ShaderTagId>(ShaderTags),
                        renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
                    drawSettings.overrideMaterial = _maskMaterial;

                    var filterSettings = new FilteringSettings(RenderQueueRange.all, _settings.previewLayer);
                    passData.RendererList = renderGraph.CreateRendererList(
                        new RendererListParams(renderingData.cullResults, drawSettings, filterSettings));

                    builder.UseRendererList(passData.RendererList);
                    builder.SetRenderAttachment(mask, 0);
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
                    builder.SetRenderFunc((MaskPassData data, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.DrawRendererList(data.RendererList);
                    });
                }

                // --- Pass B: composite outline over camera color ---
                using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>(
                           "Ghost Outline Composite", out var passData))
                {
                    passData.Mask = mask;
                    passData.Material = _compositeMaterial;

                    builder.UseTexture(mask);
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                    builder.SetRenderFunc((CompositePassData data, RasterGraphContext ctx) =>
                    {
                        data.Material.SetFloat(OutlineThicknessId, _settings.outlineThicknessPx);
                        data.Material.SetFloat(DashDensityId, _settings.dashDensityPx);
                        data.Material.SetFloat(DashSpeedId, _settings.dashSpeed);
                        data.Material.SetFloat(FillStrengthId, _settings.fillStrength);
                        Blitter.BlitTexture(ctx.cmd, data.Mask, new Vector4(1, 1, 0, 0), data.Material, 0);
                    });
                }
            }
        }
    }
}
```

> API verification step: before/while writing, use `unity_reflect`/`unity_docs lookup` for `RenderGraph`, `AddRasterRenderPass`, `UniversalRenderer.CreateRenderGraphTexture`, `RenderingUtils.CreateDrawingSettings` overloads in **this** project's URP 17.0.4 — signatures shifted between 6000.0.x patch releases. Fix to match what reflection reports, not what this plan guesses.

- [ ] **Step 7.5: Add feature to PC_Renderer only**

Use `manage_graphics` (Features group: add) targeting `Assets/Project/Settings/PC_Renderer.asset`, type `WheatFarm.Rendering.GhostOutlineRenderFeature`. Then set `settings.previewLayer` to the `PlacementPreview` layer mask and assign the two shader references (via `manage_asset modify` or instruct manual assignment in inspector). **Do not add to Mobile_Renderer.**

- [ ] **Step 7.6: Verify in play mode via MCP**

Select a building, position ghost partially behind the Mill, screenshot: occluded region shows fill + animated dashed contour in validity color (green over free ground, red over an occupied spot). Toggle the feature off in PC_Renderer → x-ray fill still present (shader fallback), outline gone. Check console. Stop play.

- [ ] **Step 7.7: Commit**

```bash
git add Assets/Scripts/Rendering Assets/Project/Shaders Assets/Project/Settings
git commit -m "feat: dashed occlusion outline render feature for preview layer (PC renderer)"
```

---

### Task 8: Final integration pass + tuning checkpoint

- [ ] **Step 8.1: Full regression via MCP play mode:** plant crops (green cells), water (blue, only planted cells), paths (tint preview + ring + repaint + bulldoze), building ghost (textures, validity, footprint, rotation by scroll, x-ray + dashes behind Mill/Bakery), UI hover hides everything. `run_tests(mode="EditMode")` → all pass. `read_console` → clean.
- [ ] **Step 8.2: Tuning checkpoint with the project owner:** dash density/speed, outline thickness, fill strength, ghost alphas are taste parameters exposed in `GhostOutlineRenderFeature.settings` and shader properties — ask the owner to eyeball a screenshot set and adjust.
- [ ] **Step 8.3: Save/load sanity:** repainted/bulldozed paths persist correctly through a `FarmSaveManager`/`SaveLoadController` round-trip (GroundState + Occupied serialize already — verify a save → load in play mode).
- [ ] **Step 8.4: Final commit** (any tuning values), e.g. `git commit -m "feat: placement preview system — tuning pass"`.

---

## Risks / Known Unknowns

1. **URP 17 RenderGraph API drift** — Task 7 code is the riskiest; verify signatures via `unity_reflect` before fixing compile errors blindly.
2. **`_BlitTexture_TexelSize` availability** in Blit.hlsl — fallback documented in Task 7.3.
3. **R3 reference in test asmdef** (Task 1) — resolve per actual package layout.
4. **`DrawRendererList` with empty list** is a no-op (fine); feature runs even with no preview active — acceptable cost (clear + fullscreen blit). Optional later: skip passes when a global "preview active" flag is off.
5. **`PlacementTool` constructor grows** (ghost service, brush preview, render config) — VContainer handles it; if it exceeds ~8 deps consider a parameter object, but YAGNI for now.
