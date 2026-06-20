# Этап B — Текстуры и нормали земли/дорожек (Implementation Plan)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Заменить 2×2 атлас земли на `Texture2DArray` (albedo+normal, слайс на каждый из 7 `GroundState`), добавить мировой UV и нормали для дорожек, скругление кромок и мягкий бленд на стыках разных типов дорожек.

**Architecture:** GPU-инстансная отрисовка земли (`DrawMeshInstancedIndirect`, общий `_PerInstanceData`) сохраняется. Тип тайла читается из `cropState.z` (`groundState`), как сейчас. Для бленда стыков структура `MeshProperties` расширяется одним полем `uint neighborTypes` (4 ниббла N/E/S/W = `GroundState` соседа), синхронно в C# и HLSL. Текстуры собираются эдитор-утилитой в два `Texture2DArray` и назначаются в `GroundMaterial`; плейсхолдеры генерятся процедурно.

**Tech Stack:** Unity URP, HLSL (`GroundInstanced.shader` + `GetStructedBuffer.hlsl`), C# (`WheatFarm.Farming`, `WheatFarm.Editor`), NUnit EditMode, проверка шейдера — Unity MCP скриншоты.

**Критично по окружению:** реальный проект на `D:\UnityProjects\wheat-farm`. Файловые инструменты Read/Edit/Write/Glob молча работают со СТАРОЙ копией на `E:`. Всё чтение/правка на D: — только через `Bash` (cat/sed/heredoc) или Unity MCP. Тесты не гоняются в Play Mode. Коммитить только явные пути (`git add <path>` + `git diff --cached`), т.к. в дереве бывают чужие незакоммиченные правки.

**Ветка:** создать `feature/ground-textures` от `main` (этапы A/C независимы; стадия A уже в `main`).

---

## File Structure

| Файл | Роль | Действие |
|---|---|---|
| `Assets/Scripts/Core/Data/MeshProperties.cs` | GPU-структура инстанса | Modify: +`uint neighborTypes`, `Size()` +4 |
| `Assets/Project/Shaders/GetStructedBuffer.hlsl` | HLSL-зеркало структуры + геттеры | Modify: +поле, +`GetNeighborTypes_float` |
| `Assets/Scripts/Features/Farming/ChunkSystem.cs` | Источник правды клеток, заполнение MeshProps | Modify: init `neighborTypes`, `ComputeNeighborTypes` |
| `Assets/Scripts/Features/Farming/GroundTextureSet.cs` | SO: пары albedo+normal на состояние | Create |
| `Assets/Scripts/Features/Farming/FarmRenderConfig.cs` | Конфиг рендера | Modify: ссылка на `GroundTextureSet` (опц.) |
| `Assets/Scripts/Editor/GroundTextureArrayBuilder.cs` | Сборка Texture2DArray + назначение в материал | Create (menu) |
| `Assets/Scripts/Editor/GroundPlaceholderTextures.cs` | Процедурные плейсхолдеры (камень/доски/кирпич + Sobel-нормали) | Create (menu) |
| `Assets/Project/Shaders/GroundInstanced.shader` | Шейдер земли | Modify: array-семплинг, мировой UV, нормали, кромки, бленд |
| `Assets/Project/Materials/Ground/Ground.mat` | Материал земли | Modify через билдер (назначить массивы), не руками |
| `Assets/Scripts/Tests/EditMode/GroundTextureArrayBuilderTests.cs` | Тест сборки массива | Create |
| `Assets/Scripts/Tests/EditMode/WheatFarm.Tests.EditMode.asmdef` | asmdef тестов | Modify: +ref `WheatFarm.Editor` |
| `Assets/Scripts/Editor/WheatFarm.Editor.asmdef` | asmdef эдитора | (без изменений; уже ref Core+Farming) |

**Замечание по asmdef:** EditMode-тесты не ссылаются на `WheatFarm.Editor`. Чтобы тестировать билдер, добавить `WheatFarm.Editor` в `references` тестового asmdef, а чистую логику сборки массива вынести в статический метод `GroundTextureArrayBuilder.BuildArray(IReadOnlyList<Texture2D> slices, out string error)`, не зависящий от `AssetDatabase`.

---

## Часть 0: расширение MeshProperties (фундамент бленда)

Делается первой и изолированно: рискованная синхронизация C#/HLSL-структуры. После неё поведение НЕ меняется (поле заполнено нулями, шейдер его пока не читает) — это даёт чистую точку проверки компиляции и stride.

### Task 1: Добавить поле `neighborTypes` в MeshProperties (C# + HLSL)

**Files:**
- Modify: `Assets/Scripts/Core/Data/MeshProperties.cs`
- Modify: `Assets/Project/Shaders/GetStructedBuffer.hlsl`
- Test: `Assets/Scripts/Tests/EditMode/MeshPropertiesTests.cs` (Create)

- [ ] **Step 1: Написать падающий тест на размер структуры**

```csharp
// Assets/Scripts/Tests/EditMode/MeshPropertiesTests.cs
using NUnit.Framework;
using WheatFarm.Core.Data;

namespace WheatFarm.Tests
{
    public class MeshPropertiesTests
    {
        [Test]
        public void Size_MatchesExplicitLayout_WithNeighborTypes()
        {
            // 2 матрицы (64 каждая) + 3 float4 (16 каждая) + 1 uint (4)
            const int expected = 64 * 2 + 16 * 3 + 4; // 180
            Assert.AreEqual(expected, MeshProperties.Size());
        }
    }
}
```

- [ ] **Step 2: Прогнать тест — должен упасть**

Запуск через Unity MCP: `run_tests` (mode=EditMode, фильтр `MeshPropertiesTests`).
Ожидание: FAIL — `Size()` вернёт 176 (поля ещё нет).

- [ ] **Step 3: Добавить поле и обновить Size()**

```csharp
// MeshProperties.cs — внутри struct, ПОСЛЕ cropState (порядок полей = порядок в HLSL!)
public Vector4 cropState;
public uint neighborTypes;   // 4 ниббла: N(0..3) E(4..7) S(8..11) W(12..15) = GroundState соседа

public static int Size()
{
    return
        sizeof(float) * 4 * 4 + // matrix m
        sizeof(float) * 4 * 4 + // groundMatrix gr
        sizeof(float) * 4 +     // color
        sizeof(float) * 4 +     // uv
        sizeof(float) * 4 +     // cropState
        sizeof(uint);           // neighborTypes
}
```
Обновить doc-комментарий структуры: `180 bytes` (старый «160 bytes» был неверен — фактическая сумма была 176).

- [ ] **Step 4: Синхронно обновить HLSL-зеркало**

В `GetStructedBuffer.hlsl` в `struct MeshProperties` добавить поле ПОСЛЕ `cropstate` (тот же порядок) и геттер:

```hlsl
struct MeshProperties
{
    float4x4 m;
    float4x4 gr;
    float4 color;
    float4 uv;
    float4 cropstate;
    uint neighborTypes;
};
```
```hlsl
// рядом с GetCropState_float
void GetNeighborTypes_float(float ID, out float Out)
{
    Out = (float)_PerInstanceData[(uint)ID].neighborTypes;
}
```

- [ ] **Step 5: Прогнать тест — должен пройти, проверить компиляцию шейдеров**

Unity MCP: `run_tests` (EditMode, `MeshPropertiesTests`) → PASS.
Затем `read_console` — НЕТ ошибок компиляции шейдеров (`GroundInstanced`, `Ground.shadergraph`, `GroundPainter.shadergraph`, краб-материалы, читающие `GetStructedBuffer.hlsl`).
Затем `run_tests` (EditMode, весь набор) → прежние тесты по-прежнему зелёные (структура добавлена в конец, ничего не сломано).

- [ ] **Step 6: Визуальная проверка отсутствия регрессии (stride)**

Unity MCP: войти в Play Mode, скриншот фермы. Земля/растения выглядят как раньше (если stride C#↔HLSL разъехался — будет «каша» из матриц/цветов). Сравнить с эталоном до изменения.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Core/Data/MeshProperties.cs \
        Assets/Project/Shaders/GetStructedBuffer.hlsl \
        Assets/Scripts/Tests/EditMode/MeshPropertiesTests.cs \
        Assets/Scripts/Tests/EditMode/MeshPropertiesTests.cs.meta
git commit -m "feat(ground): add neighborTypes channel to MeshProperties (C#+HLSL)"
```

---

### Task 2: Заполнять `neighborTypes` для путевых клеток

**Files:**
- Modify: `Assets/Scripts/Features/Farming/ChunkSystem.cs` (`UpdateGroundNeighborFlags`, `InitializeChunkMeshProps`, +`ComputeNeighborTypes`)
- Test: новый `Assets/Scripts/Tests/EditMode/ChunkSystemNeighborTypesTests.cs`

Замечание: `ComputeNeighborTypes` использует тот же `ResolveCell` (кросс-чанк) и порядок направлений, что и `ComputeNeighborFlags`. Биты flags: 0=N(+Y),1=E(+X),2=S(-Y),3=W(-X). Нибблы neighborTypes ставим в ТОМ ЖЕ порядке: ниббл0=N, ниббл1=E, ниббл2=S, ниббл3=W (первые 4 элемента `DxArr`/`DyArr`).

- [ ] **Step 1: Падающий тест**

Тест на чистом поле (см. паттерн создания `ChunkSystem` в `PlacementServiceTests`/`BrushServiceTests`). Поставить горизонтальную полосу: клетка PathStone, восточный сосед PathWood. После `UpdateGroundNeighborFlags` на клетке PathStone: ниббл E (`(neighborTypes >> 4) & 0xF`) == `(uint)GroundState.PathWood` (5). Соседи-трава → 0, грязь → 1.

- [ ] **Step 2: Прогнать — FAIL** (поле всегда 0).

- [ ] **Step 3: Реализация**

В `UpdateGroundNeighborFlags`, в ветке «Farmed cell» (после `props.uv.w = ComputeNeighborFlags(...)`) добавить для путевых клеток заполнение, иначе обнулять:
```csharp
if (nChunk.Cells[idx].GroundState >= GroundState.PathStone)
    props.neighborTypes = ComputeNeighborTypes(nChunkCoord, nx, ny);
else
    props.neighborTypes = 0u;
```
Новый метод (нибблы N/E/S/W = GroundState соседа; вне сетки/трава → 0):
```csharp
private uint ComputeNeighborTypes(Vector2Int chunkCoord, int cellX, int cellY)
{
    uint packed = 0;
    for (int dir = 0; dir < 4; dir++) // 0=N,1=E,2=S,3=W
    {
        ResolveCell(chunkCoord, cellX + DxArr[dir], cellY + DyArr[dir],
            out var nc, out int nx, out int ny);
        var nChunk = GetChunk(nc);
        uint state = 0;
        if (nChunk != null && nChunk.Unlocked)
            state = (uint)nChunk.Cells[nChunk.CellIndex(nx, ny)].GroundState;
        packed |= (state & 0xF) << (dir * 4);
    }
    return packed;
}
```
В `InitializeChunkMeshProps` добавить `props.neighborTypes = 0u;` рядом с инициализацией `props.cropState`.

- [ ] **Step 4: Прогнать — PASS** (EditMode, новый тест + весь набор).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Features/Farming/ChunkSystem.cs \
        Assets/Scripts/Tests/EditMode/ChunkSystemNeighborTypesTests.cs \
        Assets/Scripts/Tests/EditMode/ChunkSystemNeighborTypesTests.cs.meta
git commit -m "feat(ground): populate neighborTypes nibbles for path cells"
```

---

## Часть 1: данные текстур (B1)

### Task 3: `GroundTextureSet` ScriptableObject

**Files:**
- Create: `Assets/Scripts/Features/Farming/GroundTextureSet.cs`
- Modify: `Assets/Scripts/Features/Farming/FarmRenderConfig.cs`

- [ ] **Step 1: Создать SO** (пары albedo+normal на каждый `GroundState`, порядок = ordinal enum)

```csharp
using UnityEngine;
namespace WheatFarm.Farming
{
    [CreateAssetMenu(menuName = "WheatFarm/GroundTextureSet")]
    public class GroundTextureSet : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public GroundState State;     // для самодокументации (индекс берётся из порядка)
            public Texture2D Albedo;
            public Texture2D Normal;
        }

        [Tooltip("По одной записи на GroundState, в порядке ordinal (Grass..PathBrick = 0..6).")]
        public Entry[] Entries;

        // Собранные билдером массивы (назначаются GroundTextureArrayBuilder).
        public Texture2DArray AlbedoArray;
        public Texture2DArray NormalArray;
    }
}
```

- [ ] **Step 2: Прокинуть ссылку в FarmRenderConfig** (опционально; массивы живут в материале)

```csharp
[Header("Ground Textures")]
[Tooltip("Набор текстур земли (albedo+normal на состояние). Билдер собирает из него Texture2DArray.")]
public GroundTextureSet GroundTextures;
```

- [ ] **Step 3: Компиляция** — Unity MCP `read_console` без ошибок.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Features/Farming/GroundTextureSet.cs \
        Assets/Scripts/Features/Farming/GroundTextureSet.cs.meta \
        Assets/Scripts/Features/Farming/FarmRenderConfig.cs
git commit -m "feat(ground): GroundTextureSet SO (albedo+normal per state)"
```

---

### Task 4: Процедурные плейсхолдеры (камень/доски/кирпич + нормали)

**Files:**
- Create: `Assets/Scripts/Editor/GroundPlaceholderTextures.cs`

Меню `WheatFarm/Build Ground Placeholder Textures`: генерит 512×512 tileable albedo для PathStone/PathWood/PathBrick (плюс простые для Grass/Tilled/Watered/Fertilized, чтобы массив был полон), из высоты считает нормали по Sobel, сохраняет PNG в `Assets/Project/Textures/Ground/Generated/`, импортирует normal-карты с `TextureImporterType.NormalMap`. Алгоритмы: камень — Воронои-ячейки + трещины; доски — вертикальные полосы + волоконный шум; кирпич — кладка со смещением рядов и швами. Все tileable (координаты по модулю).

> **Точка вклада владельца (опц.):** внешний вид плейсхолдеров — вкусовщина, заменяется художественными текстурами без правок кода. Можно принять дефолт из плана.

- [ ] **Step 1:** Скелет генератора с tileable-хелперами (Voronoi/value-noise по модулю размера), функция `HeightToNormal(float[,] height, float strength)` (Sobel → нормаль в tangent space, упаковка в RGB).
- [ ] **Step 2:** Три путевых albedo + height → normal, плюс плоские albedo для 0–3 (почти-сплошные, тинт делает шейдер).
- [ ] **Step 3:** Сохранение через `File.WriteAllBytes(EncodeToPNG)` + `AssetDatabase.ImportAsset`; для нормалей importer `NormalMap`, для albedo sRGB. Заполнить ассет `GroundTextureSet` ссылками.
- [ ] **Step 4:** Запуск меню, `read_console` без ошибок, глазами проверить превью текстур в Project.
- [ ] **Step 5: Commit** (скрипт + сгенерированные ассеты + .meta).

---

### Task 5: `GroundTextureArrayBuilder` + EditMode-тест

**Files:**
- Create: `Assets/Scripts/Editor/GroundTextureArrayBuilder.cs`
- Create: `Assets/Scripts/Tests/EditMode/GroundTextureArrayBuilderTests.cs`
- Modify: `Assets/Scripts/Tests/EditMode/WheatFarm.Tests.EditMode.asmdef` (+`WheatFarm.Editor`)

Чистая логика сборки тестируема без AssetDatabase: статический `Texture2DArray BuildArray(IReadOnlyList<Texture2D> slices, out string error)` — проверяет непустоту, одинаковые размер/формат, создаёт `Texture2DArray(w,h,count,format,mips)` и копирует слайсы (`Graphics.CopyTexture`/`SetPixels`). Меню `WheatFarm/Build Ground Texture Arrays`: читает `GroundTextureSet.Entries`, собирает albedo- и normal-массивы, `AssetDatabase.CreateAsset`, пишет в `set.AlbedoArray/NormalArray`, и `material.SetTexture("_GroundAlbedoArray", ...)`, `material.SetTexture("_GroundNormalArray", ...)` на `GroundMaterial`.

- [ ] **Step 1: Падающий тест**

```csharp
[Test]
public void BuildArray_AssemblesSlices_WithCorrectDimensions()
{
    var slices = new[] {
        new Texture2D(4, 4, TextureFormat.RGBA32, false),
        new Texture2D(4, 4, TextureFormat.RGBA32, false),
    };
    var arr = GroundTextureArrayBuilder.BuildArray(slices, out var error);
    Assert.IsNull(error);
    Assert.IsNotNull(arr);
    Assert.AreEqual(2, arr.depth);
    Assert.AreEqual(4, arr.width);
}

[Test]
public void BuildArray_RejectsMismatchedSizes()
{
    var slices = new[] {
        new Texture2D(4, 4, TextureFormat.RGBA32, false),
        new Texture2D(8, 8, TextureFormat.RGBA32, false),
    };
    var arr = GroundTextureArrayBuilder.BuildArray(slices, out var error);
    Assert.IsNotNull(error);
    Assert.IsNull(arr);
}
```

- [ ] **Step 2:** Добавить `WheatFarm.Editor` в references тестового asmdef. Дождаться доменного релоада (`editor_state.isCompiling`).
- [ ] **Step 3: Прогнать — FAIL** (метода нет).
- [ ] **Step 4:** Реализовать `BuildArray` + меню-обвязку.
- [ ] **Step 5: Прогнать — PASS.**
- [ ] **Step 6:** Запустить меню на реальном `GroundTextureSet`, `read_console` без ошибок, убедиться что массивы созданы и назначены в `Ground.mat`.
- [ ] **Step 7: Commit** (билдер + тест + asmdef + созданные .asset массивов).

---

## Часть 2: шейдер (B2)

Шейдер не покрывается юнит-тестами — проверка визуальная через Unity MCP (`manage_editor` play, `read_console`, скриншоты), per spec B3. Каждый шаг — отдельный коммит после визуального подтверждения. Резать на маленькие шаги, чтобы регрессию ловить рано.

### Task 6: Семплинг Texture2DArray по слайсу + мировой UV дорожек

**Files:** Modify `Assets/Project/Shaders/GroundInstanced.shader`

- [ ] **Step 1:** Объявить `TEXTURE2D_ARRAY(_GroundAlbedoArray)` / `_GroundNormalArray` + сэмплеры; добавить `_PathTileSize` (Float, дефолт 1.0) в Properties и CBUFFER. Старый `_GroundAtlas` оставить временно (удалим в Task 10).
- [ ] **Step 2:** В `frag`: вместо атласного семпла — `SAMPLE_TEXTURE2D_ARRAY(_GroundAlbedoArray, sampler, uvSel, state)`, где для состояний 0–3 `uvSel = input.tileUV`, для дорожек 4–6 `uvSel = input.positionWS.xz / _PathTileSize`. Убрать вычисление `atlasUV` в `vert` (или оставить мёртвым до Task 10).
- [ ] **Step 3:** Visual: Unity MCP play → скриншот. Дорожка из ≥4 клеток бесшовна, грядки/трава на месте. `read_console` чисто.
- [ ] **Step 4: Commit.**

### Task 7: Нормали и освещение дорожек

- [ ] **Step 1:** Семпл `_GroundNormalArray` тем же `uvSel`/слайсом, `UnpackNormal`. TBN фиксированный: T=(1,0,0), B=(0,0,1), N=(0,1,0); нормаль в мир = `normalize(T*n.x + B*n.y + N*n.z)`.
- [ ] **Step 2:** `NdotL` по возмущённой нормали для всех состояний; для дорожек (state≥4) лёгкий Blinn-Phong (`_PathSpecular`, `_PathSmoothness` — новые Float props), для земли/травы без спекуляра.
- [ ] **Step 3:** Visual при дневном освещении (DayNight): рельеф камня/досок/кирпича читается, блик мягкий. Скриншот.
- [ ] **Step 4: Commit.**

### Task 8: Кромки и скругление углов дорожек (`neighborFlags`)

- [ ] **Step 1:** Использовать существующий `input.neighborFlags` (биты N/E/S/W/NE/SE/SW/NW). Для путевых клеток: где нет соседа-дорожки в направлении — смягчать кромку к траве (`_EdgeSoftness`) и скруглять внешние углы (`_CornerRadius`). В «срезанных» местах фрагмент рисует подложку — слайс травы (`SAMPLE_TEXTURE2D_ARRAY(..., tileUV, (int)GroundState.Grass)` с тинтом травы).
- [ ] **Step 2:** Маска кромки по `tileUV` (расстояние до края клетки) в зависимости от наличия соседей; альфа-смешение path↔grass подложки через `smoothstep`.
- [ ] **Step 3:** Visual: одиночная путевая клетка — скруглённый «островок»; прямая дорожка — ровные края; угол/изгиб — скругление снаружи. Скриншоты 3 кейсов.
- [ ] **Step 4: Commit.**

### Task 9: Мягкий бленд на стыке РАЗНЫХ типов дорожек (`neighborTypes`)

- [ ] **Step 1:** Прочитать `neighborTypes` в `vert` (`nointerpolation uint`) из `data.neighborTypes` (только для путевых клеток; иначе 0). Передать во фрагмент.
- [ ] **Step 2:** Во фрагменте: для каждого направления N/E/S/W, если ниббл = путь ДРУГОГО типа (значение ≥ PathStone и ≠ текущему state), в УЗКОЙ полосе у соответствующего края (по `tileUV`) подмешать слайс соседского типа через `smoothstep`. Доминирующее направление при нескольких разных соседях: сосед с наибольшим весом полосы в пикселе — стыки 3+ типов не вылизываем (per spec).

> **РЕШЕНИЕ ВЛАДЕЛЬЦА (2026-06-20): узкая чёткая кромка.** Полоса `_TypeBlendWidth` (Range 0.05..0.5, **дефолт 0.15** клетки), `smoothstep` от края — почти резкий стык, стилизованный/мультяшный вид, минимум «грязи». Разрешение конфликта нескольких разных соседей — по максимальному весу полосы в пикселе.

- [ ] **Step 3:** Visual: стык PathStone↔PathWood и PathWood↔PathBrick — мягкая переходная полоса, без резкой линии. Скриншоты.
- [ ] **Step 4: Commit.**

### Task 10: Чистка — вывести 2×2 атлас, финальная проверка

- [ ] **Step 1:** Удалить из шейдера `_GroundAtlas`, `_GroundAtlas_ST`, мёртвый код `atlasUV`/`atlasOffset`. Тинты `_TintPath*`/`_Tint*` оставить (питают превью кисти — API не трогаем).
- [ ] **Step 2:** Прогнать весь EditMode-набор (Unity MCP `run_tests`) → всё зелёное; `read_console` без ошибок/ворнингов шейдера.
- [ ] **Step 3:** Интеграционный скриншот: ферма с грядками + 3 типами дорожек, дневной и ночной свет (DayNight).
- [ ] **Step 4: Commit.**

```bash
git add Assets/Project/Shaders/GroundInstanced.shader
git commit -m "refactor(ground): retire 2x2 atlas; finalize Texture2DArray ground shader"
```

---

## Проверка завершённости этапа B (per spec B3)

- [ ] `Texture2DArray` ×2 собраны, назначены в `GroundMaterial`, слайсы соответствуют ordinal `GroundState`.
- [ ] Дорожка бесшовна на ≥4 клетках (мировой UV).
- [ ] Скругление кромок/углов на одиночных клетках и изгибах; прямые участки без ложных скруглений.
- [ ] Нормали читаются при дневном освещении; спекуляр только на дорожках.
- [ ] Мягкий бленд на стыке двух разных типов дорог.
- [ ] EditMode: `MeshPropertiesTests`, `ChunkSystemNeighborTypesTests`, `GroundTextureArrayBuilderTests` зелёные; весь существующий набор не сломан.
- [ ] Нет ошибок компиляции шейдеров в `read_console`.

## Вне объёма (per spec)
- Сплайновые дороги; спец-тайлы переходов (делаем бленд); миграция сейвов; этапы A (готов) и C (пикер краски — отдельный план).

## Открытые точки для владельца
1. ~~Бленд-функция стыков (Task 9)~~ — **РЕШЕНО (2026-06-20): узкая чёткая кромка, `_TypeBlendWidth` дефолт 0.15.**
2. **Вид плейсхолдеров (Task 4)** — вкусовщина, заменяется без кода; принят дефолт из плана.
