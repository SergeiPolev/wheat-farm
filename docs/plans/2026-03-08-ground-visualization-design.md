# Ground Visualization Design

## Problem

All unlocked cells look identical — a flat static Ground Plane with no reaction to farming. The player gets no visual feedback for planting, watering, or fertilizing. Farming Simulator-style ground state visualization is needed.

## Solution

Per-cell ground tiles rendered via the existing GPU Instanced Indirect pipeline, reusing the `gr` matrix already present in `MeshProperties` but currently unused.

## Ground States

| Value (cropState.z) | State | Visual |
|---------------------|-------|--------|
| 0 | Grass (default) | Green grass texture |
| 1 | Tilled (planted) | Dark tilled soil with furrows |
| 2 | Watered | Wet soil (darker, slight sheen) |
| 3 | Fertilized | Soil with yellow/brown tint |

## Architecture

### Dual Draw per Chunk

Each `ChunkCropRenderer` makes **two** `DrawMeshInstancedIndirect` calls with the **same** ComputeBuffer (`_PerInstanceData`):

1. **Ground pass** — flat Quad mesh, ground material, `vertInstancingGroundSetup()` (reads `gr` matrix). Always visible for all cells in unlocked chunks. `cropState.z` selects texture from atlas.
2. **Crop pass** — plant mesh, crop material, `vertInstancingSetup()` (reads `m` matrix). Visible only when `cropState.y > 0`.

Zero memory duplication — both passes read the same buffer.

### Render Order

```
Ground Plane (static, render queue 2000) — background beyond chunks
  |
Ground Tiles (instanced, render queue 2001) — per-cell, on top of Ground Plane
  |
Crops (instanced, render queue 2002) — plants on top of ground tiles
```

### Ground ShaderGraph

New `Ground Instanced.shadergraph`:
- Uses `PROCEDURAL_INSTANCING_ON` keyword
- Calls `vertInstancingGroundSetup()` (existing, currently commented out in GetStructedBuffer.hlsl)
- Reads `cropState.z` to select UV region from a 2x2 texture atlas (4 states)
- Renders slightly below Y=0 via `gr` matrix to avoid z-fighting with the static Ground Plane

### Texture Atlas

Single texture with 4 tiles (2x2 grid):
- Top-left: grass
- Top-right: tilled soil
- Bottom-left: watered soil
- Bottom-right: fertilized soil

UV offset calculated in shader: `uv = baseUV * 0.5 + offset[state]`

### C# Changes

**SubCellState** — add `GroundState` field (enum or int: 0=Grass, 1=Tilled, 2=Watered, 3=Fertilized)

**PlantSystem:**
- `Plant()` -> sets `GroundState = Tilled`, writes `cropState.z = 1`
- `Water()` -> sets `GroundState = Watered`, writes `cropState.z = 2`, marks chunk dirty
- `Fertilize()` -> sets `GroundState = Fertilized`, writes `cropState.z = 3`, marks chunk dirty
- `Harvest()` (non-renewable crops) -> resets `GroundState = Grass`, `cropState.z = 0`
- `Uproot()` -> resets `GroundState = Grass`, `cropState.z = 0`

**ChunkSystem.InitializeChunkMeshProps():**
- Initialize `gr` matrix for ALL cells (flat quad at cell position, always visible)
- Set `cropState.z = 0` (grass by default)

**ChunkCropRenderer:**
- Add second cloned material (ground material) from `FarmRenderConfig.GroundMaterial`
- Add second args buffer for ground draw call
- `Draw()` calls `DrawMeshInstancedIndirect` twice: ground first, crops second

**FarmRenderConfig:**
- Add `GroundMesh` field (Quad mesh)
- Add `GroundMaterial` field (Ground Instanced material)

### HLSL Changes

**GetStructedBuffer.hlsl:**
- Uncomment `vertInstancingGroundSetup()` function
- Ensure it reads `gr` matrix and applies it correctly

## Existing Infrastructure Used

- `MeshProperties.gr` — second TRS matrix, already in struct and HLSL
- `vertInstancingGroundMatrices()` — already implemented in HLSL
- `vertInstancingGroundSetup()` — already implemented, just commented out
- `cropState.z` — unused channel, perfect for ground state encoding
- Same ComputeBuffer — no additional GPU memory per chunk
