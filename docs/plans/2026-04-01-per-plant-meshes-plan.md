# Per-Plant-Type Meshes — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Each plant type renders with its own mesh and material via multi-pass GPU instanced indirect rendering.

**Architecture:** PlantData gets MeshId field. ChunkCropRenderer issues one DrawMeshInstancedIndirect per unique (mesh, material) pair. ShaderGraph already filters by `cropState.x == _Id`.

**Tech Stack:** Unity GPU Instanced Indirect, ShaderGraph, VContainer DI, ScriptableObjects

**Design doc:** `docs/plans/2026-04-01-per-plant-meshes-design.md`

---

## Phase A: Data Layer (PlantData + Config)

### Task A1: Add MeshId to PlantData

**Files:**
- Modify: `Assets/Scripts/Core/Data/PlantData.cs`

**Steps:**

1. Add `[Header("Rendering")] public int MeshId = 1;` field to PlantData, between Visuals and Growth headers.

2. Commit: `feat(A1): add MeshId field to PlantData`

### Task A2: Add CropMeshEntry to FarmRenderConfig

**Files:**
- Modify: `Assets/Scripts/Features/Farming/FarmRenderConfig.cs`

**Steps:**

1. Add a serializable struct `CropMeshEntry` with fields: `int MeshId`, `Mesh Mesh`, `Material Material`.

2. Add `CropMeshEntry[] CropMeshEntries` array to FarmRenderConfig, under the existing CropMesh/CropMaterial fields. Keep old fields as fallback.

3. Add helper method `GetEntries()` that returns CropMeshEntries if non-empty, otherwise returns a single-element array built from the legacy CropMesh/CropMaterial with MeshId=1.

4. Commit: `feat(A2): CropMeshEntry array in FarmRenderConfig`

### Task A3: Write MeshId into cropState.x

**Files:**
- Modify: `Assets/Scripts/Features/Farming/PlantSystem.cs`

**Steps:**

1. In `Plant()` method (~line 126): replace `props.cropState.x = 1;` with `props.cropState.x = data.MeshId;` (the `data` parameter is already `PlantData`).

2. In `Tick()` / `RebuildMatrix()`: when rebuilding the matrix for growing plants, `cropState.x` must stay correct. Currently it's not overwritten during growth — only `cropState.y` (growth) changes. Verify this by reading the Tick method. If cropState.x is not touched during growth, no change needed.

3. In save/load restore (`FarmSaveManager.RestoreFromData`): the line `props.cropState = new Vector4(1f, ...)` hardcodes 1. Fix to look up the PlantData and use its MeshId. This requires injecting PlantDatabase (already injected as `_plantDb`).

4. Commit: `feat(A3): write PlantData.MeshId into cropState.x`

---

## Phase B: Multi-Pass Renderer

### Task B1: Refactor ChunkCropRenderer for multi-pass

**Files:**
- Modify: `Assets/Scripts/Features/Farming/ChunkCropRenderer.cs`

**Steps:**

1. Add inner struct `CropPass` with fields: `ComputeBuffer ArgsBuffer`, `Material Material`, `Mesh Mesh`, `int MeshId`.

2. Replace single crop fields (`_cropArgsBuffer`, `_cropMaterial`, `_cropMesh`) with `List<CropPass> _cropPasses`.

3. Change constructor signature: instead of `Mesh cropMesh, Material cropSharedMaterial`, accept `FarmRenderConfig.CropMeshEntry[] entries`.

4. In `InitializeBuffers()`: for each entry, create a `CropPass` with its own `CreateArgsBuffer(entry.Mesh, count)` and `new Material(entry.Material)`. Set `_PerInstanceData` buffer on each cloned material.

5. In `Draw()`: iterate `_cropPasses` and issue `DrawMeshInstancedIndirect` for each.

6. In `Dispose()`: release all CropPass args buffers and destroy all cloned materials.

7. Commit: `refactor(B1): ChunkCropRenderer multi-pass crop rendering`

### Task B2: Update FarmRenderSystem to pass entries

**Files:**
- Modify: `Assets/Scripts/Features/Farming/FarmRenderSystem.cs`

**Steps:**

1. In `Tick()`, where `new ChunkCropRenderer(...)` is called: replace `_config.CropMesh, _config.CropMaterial` with `_config.GetEntries()`.

2. Update the null check at top of Tick(): instead of checking `CropMesh == null`, check `GetEntries().Length == 0`.

3. Commit: `refactor(B2): FarmRenderSystem passes CropMeshEntries`

---

## Phase C: Asset Configuration

### Task C1: Create missing materials

**Files:**
- Create: `Assets/Project/Materials/Crops/Sunflower.mat`

**Steps:**

1. In Unity: duplicate Grass.mat → Sunflower.mat. Set `_Id = 3`.

2. Corn.mat: change `_Id` from 3 → 2.

3. Sunflower.mat: `_Id = 3`.

4. Tomato.mat: `_Id = 4` (already correct).

5. Wheat.mat: change `_Id` from 2 → 1 to match Grass.mat, OR just use Grass.mat for wheat. Simplest: Wheat uses Grass.mat (_Id=1). Remove Wheat.mat usage.

Final mapping:
- MeshId 1 → Grass.mat (_Id=1) — Wheat, Rose, Cherry (fallback)
- MeshId 2 → Corn.mat (_Id=2)
- MeshId 3 → Sunflower.mat (_Id=3)
- MeshId 4 → Tomato.mat (_Id=4)

6. Commit: `feat(C1): crop materials with correct _Id values`

### Task C2: Assign meshes and materials on Plant_*.asset

**Steps:**

1. Set MeshId, Mesh, Material on each Plant_*.asset:

| Asset | MeshId | Mesh | Material |
|-------|--------|------|----------|
| Plant_Wheat | 1 | pyramid.fbx (Plane submesh) | Grass.mat |
| Plant_Corn | 2 | Corn1_P.fbx | Corn.mat |
| Plant_Sunflower | 3 | Sunflower1_P.fbx | Sunflower.mat |
| Plant_Tomato | 4 | Tomatoes1_P.fbx | Tomato.mat |
| Plant_Rose | 1 | pyramid.fbx | Grass.mat |
| Plant_Cherry | 1 | pyramid.fbx | Grass.mat |

2. Configure FarmRenderConfig.asset: add CropMeshEntries array with 4 entries (MeshId 1-4 with corresponding mesh/material pairs).

3. Commit: `feat(C2): assign per-plant meshes and materials`

### Task C3: Fix FarmSaveManager cropState.x restore

**Files:**
- Modify: `Assets/Scripts/Infrastructure/Save/FarmSaveManager.cs`

**Steps:**

1. In `RestoreFromData()`, chunk restore section (~line 221): replace `props.cropState = new Vector4(1f, saved.Growth, saved.GroundState, 0f)` with:
```csharp
var plantData = _plantDb?.GetById(saved.PlantId);
int meshId = plantData?.MeshId ?? 1;
props.cropState = new Vector4(meshId, saved.Growth, saved.GroundState, 0f);
```

2. Commit: `fix(C3): restore correct MeshId in cropState.x on load`

---

## Phase D: Verification + Cleanup

### Task D1: Play-mode verification

**Steps:**

1. Enter Play Mode. Plant wheat → should render with pyramid.fbx.
2. Switch to Corn via CatalogTabBar → plant → should render with Corn1_P.fbx mesh.
3. Switch to Sunflower → plant → should render with Sunflower1_P.fbx mesh.
4. Switch to Tomato → plant → should render with Tomatoes1_P.fbx mesh.
5. Mix multiple plant types in one chunk → all should render correctly (no z-fighting, no wrong meshes).
6. Save (F5), reload (F9) → all plants should restore with correct meshes.

### Task D2: Update CLAUDE.md

**Steps:**

1. Update Core Systems table: remove "Requires multi-material rendering" from What's Next.
2. Update Crop Rendering Pipeline section to mention multi-pass.
3. Note new material mapping.
4. Commit: `docs(D2): update CLAUDE.md with per-plant mesh rendering`
