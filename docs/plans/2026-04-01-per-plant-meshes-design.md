# Per-Plant-Type Meshes — Design

## Problem

All crops render using the same `pyramid.fbx` mesh. We have 4 unique FBX models (Corn1_P, Sunflower1_P, Tomatoes1_P, Carrot1_P) and 4 materials with distinct `_Id` values, but the renderer ignores them.

## Solution

**Multi-pass rendering per chunk.** Each unique (mesh, material) pair gets its own `DrawMeshInstancedIndirect` call. The ShaderGraph already filters by `cropState.x == _Id` — instances where the ID doesn't match are collapsed to origin (zero-scale matrix), so they don't render.

## Architecture

### Data flow

```
PlantData.MeshId → PlantSystem writes cropState.x → ShaderGraph checks cropState.x == material._Id
PlantData.Mesh   → ChunkCropRenderer draws with this mesh
PlantData.Material → ChunkCropRenderer draws with this material (cloned per chunk)
```

### Changes

**PlantData.cs** — add `int MeshId` field. This value is written into `cropState.x` and must match the material's `_Id` property.

**PlantSystem.cs** — replace `props.cropState.x = 1` with `props.cropState.x = plantData.MeshId`. Requires PlantDatabase lookup by PlantId during Tick() matrix rebuild.

**FarmRenderConfig.cs** — add `CropMeshEntry[]` array: list of all (meshId, mesh, material) combos. This is the render config's knowledge of what draw passes exist.

**ChunkCropRenderer.cs** — instead of one crop draw call, iterate over all CropMeshEntries. Each entry gets its own `argsBuffer` + cloned `Material`. The shared `_PerInstanceData` ComputeBuffer stays — all passes read from it.

**FarmRenderSystem.cs** — pass the CropMeshEntry array when creating ChunkCropRenderers.

### Materials needed

| Plant | MeshId (_Id) | Mesh | Material |
|-------|-------------|------|----------|
| Wheat | 1 | pyramid.fbx | Grass.mat (_Id=1) |
| Corn | 2 | Corn1_P.fbx | Corn.mat (update _Id→2) |
| Sunflower | 3 | Sunflower1_P.fbx | Sunflower.mat (new, _Id=3) |
| Tomato | 4 | Tomatoes1_P.fbx | Tomato.mat (_Id=4) |
| Rose | 1 | pyramid.fbx | Grass.mat (fallback) |
| Cherry | 1 | pyramid.fbx | Grass.mat (fallback) |

Current Corn.mat has `_Id=3`, Wheat.mat has `_Id=2`. We remap to simpler numbering starting from 1.

### Draw calls

- Before: 25 chunks × (1 ground + 1 crop) = 50 draw calls
- After: 25 chunks × (1 ground + up to 4 crop passes) = 25-125 crop draw calls
- Typical: most chunks have 1-2 plant types → ~50-75 total

### Save/load compatibility

`cropState.x` was always saved as part of SubCellSaveData's GPU sync. The value changes from hardcoded `1` to per-plant MeshId, but old saves where all cropState.x=1 will still render as Wheat (MeshId=1). No migration needed.
