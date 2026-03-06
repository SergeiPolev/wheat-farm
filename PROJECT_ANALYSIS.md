# Wheat Farm -- Project Analysis

## 1. Project Identity

| Field | Value |
|-------|-------|
| **Project Name** | Service Template (internal) / wheat-farm (solution) |
| **Unity Version** | 6000.0.48f1 (Unity 6) |
| **Render Pipeline** | Universal Render Pipeline (URP) 17.0.4 |
| **Scripting Backend** | IL2CPP (Android), Mono (other) |
| **Bundle Version** | 0.1.0 |
| **Color Space** | Linear |
| **Template Origin** | `com.unity.template.urp-blank@17.0.11` |

---

## 2. Packages / Dependencies

### Unity Packages (manifest.json)

| Package | Version |
|---------|---------|
| `com.cathei.bakingsheet` | 4.1.3 (Google Sheets data pipeline) |
| `com.unity.render-pipelines.universal` | 17.0.4 |
| `com.unity.cinemachine` | 3.1.3 |
| `com.unity.inputsystem` | 1.14.0 |
| `com.unity.ai.navigation` | 2.0.7 |
| `com.unity.recorder` | 5.1.2 |
| `com.unity.timeline` | 1.8.7 |
| `com.unity.ugui` | 2.0.0 |
| `com.unity.visualscripting` | 1.9.6 |
| `com.unity.test-framework` | 1.5.1 |
| `com.unity.ide.rider` | 3.0.36 |
| `com.unity.ide.visualstudio` | 2.0.23 |
| `com.unity.multiplayer.center` | 1.0.0 |
| `com.unity.collab-proxy` | 2.8.2 |

### Third-Party Libraries (Assets)

| Library | Purpose |
|---------|---------|
| **DOTween** (Demigiant) | Tweening / анимации |
| **NaughtyAttributes** | Расширения инспектора |
| **Graphy** (Tayx) | Runtime FPS/RAM мониторинг |
| **SimpleInput** | Мобильный ввод (joystick, dpad) |
| **LeanPool** (CW) | Object pooling |
| **LeanCommon** (CW) | Общие утилиты CW |

### Scripting Define Symbols

- `DOTWEEN` (все платформы)
- `BAKINGSHEET_RUNTIME_GOOGLECONVERTER` (Android)
- `UNITY_ANY_INSTANCING_ENABLED` (Standalone)

---

## 3. Scenes

| Scene | Path | Назначение |
|-------|------|------------|
| **Main** | `Assets/Project/Scenes/Main.unity` | Основная игровая сцена |
| DemoScene | `Assets/Third Party/NaughtyAttributes/Samples/` | Демо NaughtyAttributes |
| ExampleScene | `Assets/Plugins/SimpleInput/Example/` | Демо SimpleInput |
| Pool examples | `Assets/Plugins/CW/LeanPool/Examples/` | Демо LeanPool |

---

## 4. C# Scripts

### 4.1 Infrastructure / State Machine Framework

**`Scripts/Infrastructure/StateMachine/`**

| File | Description |
|------|-------------|
| `IExitableState.cs` | Базовый интерфейс с `Exit()` для всех состояний |
| `IState.cs` | Расширяет IExitableState, добавляет `Enter()`. Определяет `IPayloadedState<T1,T2>` |
| `IPayloadedState.cs` | Интерфейс для состояний с типизированным payload при входе |
| `ITick.cs` | Маркер для Update-тика |
| `IFixedTick.cs` | Маркер для FixedUpdate-тика |
| `ILateTick.cs` | Маркер для LateUpdate-тика |
| `IStateChanger.cs` | Интерфейс для переключения состояний |
| `StateChanger.cs` | Реализация переключателя |
| `StateMachineBase.cs` | Абстрактная база: `Dictionary<Type, IExitableState>`, управление `Enter`, `ChangeState`, маршрутизация `Tick/FixedTick/LateTick` |

### 4.2 Game Bootstrap & Entry Point

**`Scripts/Infrastructure/`**

| File | Description |
|------|-------------|
| `Game.cs` | **Entry point MonoBehaviour**. Создает `GameStateMachine`, входит в `BootstrapState`. Маршрутизирует Update -> Tick(), FixedUpdate -> FixedTick(). Предотвращает засыпание экрана. |
| `ICoroutineRunner.cs` | Интерфейс-обертка для `StartCoroutine` |
| `IGameStateChanger.cs` | Маркер для game-level переключения состояний |

### 4.3 Game States

**`Scripts/Infrastructure/States/`**

| File | Description |
|------|-------------|
| `GameStateMachine.cs` | Конкретная стейт-машина, регистрирует все 9 состояний |
| `BootstrapState.cs` | **Регистрирует и инициализирует 20+ сервисов**, затем входит в `LevelState` |
| `SelectGoogleSheetState.cs` | Открывает окно конфигурации Google Sheet, переходит в `LoadGoogleSheetState` |
| `LoadGoogleSheetState.cs` | Загружает конфиг из Google Sheets или JSON, переходит в `LoadProgressState` |
| `LoadProgressState.cs` | Загружает прогресс из PlayerPrefs или создает новый, переходит в `HubState` |
| `HubState.cs` | Открывает Hub UI, по кнопке "Start" -> fade -> `LoadLevelState` |
| `LoadLevelState.cs` | Сбрасывает кошелек и таймер, входит в `LevelState` |
| `LevelState.cs` | **Основной геймплей**. ITick/IFixedTick/ILateTick. Активирует ввод, управляет движением, инструментами, crop points |
| `LevelResultState.cs` | Получает результат (win/lose), переходит в `CleanupGameState` |
| `CleanupGameState.cs` | Очистка, возврат в `HubState` |

### 4.4 Gameplay Services (в States/)

| File | Description |
|------|-------------|
| `PlayerMovementSystem.cs` | Читает input direction, применяет изометрическую матрицу поворота (45 градусов), вызывает `Player.Move()` |
| `FieldToolsService.cs` | **Ядро фарм-инструментов**. Raycast по горизонтальной плоскости, поиск crop fields в радиусе, применение текущего инструмента к ячейкам |
| `ChangeToolsService.cs` | Управление инвентарем инструментов (Watering/Cutting/Planter). Переключение по Numpad 1/2/3 |
| `GetCropPointsService.cs` | Находит все `CropFieldData` на сцене. Пространственный запрос `GetCropRenderersInRadius()` |
| `DebugService.cs` | Events для restart-level и return-to-hub |

### 4.5 Services

**`Scripts/Infrastructure/Services/`**

| File | Description |
|------|-------------|
| `IService.cs` | Маркерный интерфейс для всех сервисов |
| `ITickable.cs` | Интерфейс для тикаемых сервисов |
| `AllServices.cs` | **Service Locator / DI container**. Singleton с `RegisterSingle<T>()` и `Single<T>()`. Generic static class trick для O(1) lookup |

### 4.6 Static Data

**`Services/StaticDataServices/`**

| File | Description |
|------|-------------|
| `StaticDataService.cs` | Загружает ScriptableObject из Resources: окна, настройки, префабы, материалы, VFX, crops |
| `CropsStaticData.cs` | ScriptableObject: массив `CropsData`, используемые типы, ground material/mesh |
| `CropsData.cs` | Сериализуемый класс: тип культуры, mesh, material, название, описание |
| `CropsType.cs` | Enum: `Grass=1, Wheat=2, Corn=3, Tomato=4` |
| `PrefabStaticData.cs` | Placeholder для ссылок на префабы |
| `MaterialsStaticData.cs` | Placeholder для ссылок на материалы |
| `VFXStaticData.cs` | Placeholder для ссылок на VFX |
| `StageStaticData.cs` | Статические данные этапов |
| `Stage.cs` | Структура данных этапа |
| `StageVisualsStaticData.cs` | Визуальные настройки этапов |
| `StageVisual.cs` | Данные визуала этапа |

### 4.7 Progress / Save System

**`Services/ProgressService/`**

| File | Description |
|------|-------------|
| `PersistentProgressService.cs` | Хранит ссылку на текущий `PlayerProgress` |
| `PlayerProgress.cs` | Сериализуемые данные прогресса (содержит `WalletData`) |
| `SaveLoadService.cs` | Оркестрирует save/load через `PlayerPrefsJsonSaveSystem` |
| `PlayerPrefsJsonSaveSystem.cs` | JSON-based сохранение через PlayerPrefs |
| `ISaveSystem.cs` | Интерфейс backend'а сохранения |
| `ISavedProgressReader.cs` | Интерфейс чтения прогресса при загрузке |
| `ISaveProgress.cs` | Интерфейс записи прогресса при сохранении |
| `WalletData.cs` | Сериализуемые данные кошелька |

### 4.8 Wallet Services

**`Services/WalletService/`**

| File | Description |
|------|-------------|
| `WalletService.cs` | Постоянный (мета) кошелек: Crystals, Coins. ISaveProgress. Add/Spend/Get с событиями |
| `GameWalletService.cs` | Кошелек уровня: Gold, Chest. Сбрасывается каждый уровень |
| `CurrencyId.cs` | Enum: `None=0, Crystal=5, Coins=10` |
| `GameCurrencyID.cs` | Enum: `None=0, Gold=1, Chest=10` |
| `CurrencyData.cs` | Пара CurrencyId + float value |

### 4.9 Google Sheet Services

**`Services/GoogleSheetService/`**

| File | Description |
|------|-------------|
| `GoogleSheetService.cs` | Управляет BakingSheet. Поддерживает online fetch и baked JSON |
| `MasterConfigSheetContainer.cs` | Контейнер master config sheet |
| `MasterConfigSheet.cs` | Определение листа master config |
| `MiscellaneousSheet.cs` | Лист конфигурации игры (длина этапа, HP и т.д.) |
| `ToolsSheet.cs` | Лист конфигурации инструментов |
| `SheetContainer.cs` | BakingSheet контейнер |
| `JsonBakedConverter.cs` | JSON конвертер для baked данных |
| `DebugSheetLogger.cs` | Логгер для отладки |

### 4.10 Other Services

| File | Description |
|------|-------------|
| `InputService.cs` | Обертка SimpleInput. Enable/Disable. Direction из Horizontal/Vertical |
| `WindowService.cs` | Менеджер окон. Open/Close/IsOpen по `WindowId`. Lazy-создание через `UIFactory` |
| `PauseService.cs` | Мульти-source система паузы. Задержанная пауза через DOTween timeScale |
| `CameraService.cs` | Управление Cinemachine. Follow target, ортографическая ширина, stage-transition движение |
| `CameraGroup.cs` | MonoBehaviour: Cinemachine camera + spline dolly. Отслеживание viewport bounds |
| `BoundsBorder.cs` | Enum направлений границ камеры |
| `GlobalBlackboard.cs` | Runtime-хранилище глобальных ссылок (Player) |

### 4.11 Player

| File | Description |
|------|-------------|
| `Player.cs` | MonoBehaviour. `ToolHandler` + Rigidbody. `Move()` устанавливает velocity и обновляет глобальный шейдер-вектор `_Interaction_Position` |

### 4.12 Crop System

**`Scripts/Crops/`**

| File | Description |
|------|-------------|
| `CropFieldData.cs` | Определяет grid поля. Генерирует `MeshProperties[]` с рандомизированными трансформами и UV для GPU instancing |
| `CropRenderer.cs` | **GPU instanced indirect рендеринг** через `Graphics.DrawMeshInstancedIndirect`. ComputeBuffers для per-instance данных |
| `MeshProperties.cs` | Struct (160 bytes): две 4x4 матрицы, цвет, UV, crop state |

### 4.13 Crop Tools

**`Scripts/Crop Tools/`**

| File | Description |
|------|-------------|
| `Tool.cs` | Абстрактный базовый класс. `ToolID` enum (Watering=1, Cutting=2, Planter=3). Методы: OnEquip, OnUnequip, UseAt, ChargeUp |
| `ToolHandler.cs` | MonoBehaviour на Player. Хранит текущий инструмент, переключение. Содержит реализации: |
| -- `WateringTool` | Полив: растит `cropState.y` если культура посажена |
| -- `CuttingTool` | Срезка/сбор: сброс cropState, цвет -> white |
| -- `PlanterTool` | Посадка: устанавливает `cropState.x` = тип культуры |

### 4.14 Grass / Painting System

**`Scripts/Grass Paint/`**

| File | Description |
|------|-------------|
| `CropPainter.cs` | Legacy/editor инструмент покраски. Raycast по crop bounds, конвертация позиции в grid |
| `Grass.cs` | Vertex-color покраска травы. Per-vertex color arrays, прогресс покраски, шейдер-текстуры |
| `Ground.cs` | Покраска земли. Lerp vertex colors к целевому цвету |

### 4.15 UI System

**`Scripts/UI/`**

| File | Description |
|------|-------------|
| `WindowId.cs` | Enum: 33 ID окон (Hub, HUD, TransitionFade, Shop, Inventory, LoadingScreen и т.д.) |
| `WindowBase.cs` | Абстрактный MonoBehaviour для окон. Open/Close с событиями, Initialize |
| `WindowModel.cs` | Модель данных окна с `OnCloseAction` |
| `WindowConfig.cs` | Связка `WindowId` + `WindowBase` префаб |
| `WindowStaticData.cs` | ScriptableObject со списком конфигов окон |
| `UIStaticData.cs` | Placeholder для UI-ссылок |
| `HUDWindow.cs` | HUD: таймер, здоровье, номер уровня, цели сбора |
| `HubWindow.cs` | Главное меню с кнопкой Start -> `OnGameStartEvent` |
| `CharacterHubAnimation.cs` | Анимации персонажа на Hub-экране |
| `TransitionFadeWindow.cs` | Fade-переход через DOTween (fade-in, callback, fade-out) |
| `FadeWindowModel.cs` | Модель fade-перехода с настраиваемыми задержками и callback'ами |
| `LoadingScreen.cs` | Экран загрузки |
| `GoogleSheetConfigWindow.cs` | Окно конфигурации Google Sheet |
| `GoogleSheetStatusWindow.cs` | Статус загрузки Google Sheet |
| `UIGameCurrencyValue.cs` | Отображение игровой валюты |
| `UILose.cs` | Экран проигрыша |
| `UIOutOfTime.cs` | Уведомление "время вышло" |

### 4.16 Factories

**`Scripts/Factories/`**

| File | Description |
|------|-------------|
| `UIFactory.cs` | Создает UI-окна из префабов через `StaticDataService`. Инстанциирует UIRoot из Resources |
| `GameFactory.cs` | Фабрика игровых объектов (placeholder) |
| `CombatTextFactory.cs` | Создает floating текст через LeanPool (Damage, Crit, Dodge) |
| `CombatText.cs` | Анимированный floating text. DOTween show/hide. Пулинг через LeanPool |

### 4.17 Extensions

**`Scripts/Extensions/`**

| File | Description |
|------|-------------|
| `ArrayExtensions.cs` | Хелперы массивов |
| `ColorExtensions.cs` | Манипуляции с цветом |
| `EnumExtensions.cs` | Утилиты enum |
| `EnumGenerator.cs` | Editor-инструмент генерации enum |
| `GameObjectExtensions.cs` | Расширения GameObject |
| `GizmosExtension.cs` | Хелперы для Gizmo |
| `JsonExtension.cs` | Хелперы JSON-сериализации |
| `MathExtension.cs` | Математические утилиты |
| `Preconditions.cs` | Validation/assertion |
| `RandomExtensions.cs` | Генерация случайных чисел |
| `StringExtensions.cs` | Строковые манипуляции |
| `TransformExtension.cs` | Утилиты Transform |
| `TweenExtension.cs` | Расширения DOTween |
| `VectorExtensions.cs` | Векторная математика |
| `Rarity.cs` | Enum: Common -> Legendary +3 |
| `RarityManager.cs` | Утилиты сравнения и отображения rarities |

### 4.18 Tools / Utilities

**`Scripts/Tools/`**

| File | Description |
|------|-------------|
| `MMEventManager.cs` | MoreMountains-style глобальная event-система. Type-safe broadcasting через struct events |
| `MMEventListener.cs` | Интерфейс слушателей |
| `MMEventListenerBase.cs` | Базовый класс слушателей |
| `MMEventListenerWrapper.cs` | Обертка слушателя |
| `EventRegister.cs` | Extension-методы для подписки на события |
| `MMGameEvent.cs` | Строковое игровое событие |
| `Coroutines.cs` | Утилиты корутин |
| `FPSCounter.cs` | Счетчик FPS |
| `UIFPSCounterNotAlloc.cs` | Non-allocating FPS counter |
| `FpsLock.cs` | Блокировка target frame rate |
| `Helpers.cs` | Общие утилиты |
| `RotateTweenAnimation.cs` | DOTween-анимация вращения |
| `UpdatePolygonCollider.cs` | Обновление polygon collider |
| `Editor/Tools.cs` | Кастомные editor-инструменты |

### 4.19 Constants

**`Scripts/Constants/`**

| File | Description |
|------|-------------|
| `PathConstants.cs` | Пути JSON конфигов: baked (Resources), runtime (persistentDataPath), master configs |
| `StringConstants.cs` | Строковые ключи PlayerPrefs |

### 4.20 Layer Manager

| File | Description |
|------|-------------|
| `LayerManager.cs` | Статический класс: индексы и маски слоев (Digger, Grass, GrassChunk, Collectibles, Player, PlayerProjectile, Enemy, EnemyProjectile, Block) |

---

## 5. Shaders / Materials

### Project Shaders (`Assets/Project/Shaders/`)

| File | Type | Description |
|------|------|-------------|
| `Grass Instanced.shadergraph` | ShaderGraph | Основной шейдер рендеринга культур. GPU instancing через StructuredBuffer |
| `Ground.shadergraph` | ShaderGraph | Шейдер поверхности земли |
| `GroundPainter.shadergraph` | ShaderGraph | Шейдер визуализации покраски земли |
| `GetStructedBuffer.hlsl` | HLSL | **Ядро instancing инфраструктуры**. `MeshProperties` struct, `StructuredBuffer<MeshProperties>`, функции чтения instance ID/UV/cropState/color |
| `GetUV.hlsl` | HLSL | Legacy UV lookup из массива float4 |

### Materials (`Assets/Project/Materials/`)

- **Crops/**: `Grass.mat`, `Wheat.mat`, `Corn.mat`, `Tomato.mat`
- **Ground/**: `Ground.mat`

---

## 6. Prefabs

### Project Prefabs (`Assets/Project/Prefabs/`)

```
Player/
  Player.prefab                     -- Игрок с Rigidbody и ToolHandler
Grass/
  Grass.prefab                      -- Объект травяного поля
UI/
  Base/
    WindowBase.prefab               -- Шаблон UI-окна
    UIButton.prefab                 -- Переиспользуемая кнопка
    Text Outline (TMP).prefab       -- Текст с обводкой
    Dim.prefab                      -- Затемняющий overlay
  HUD/
    [Window] HUD.prefab             -- Игровой HUD
  HubWindow/
    [Window] Hub.prefab             -- Главное меню
  FadeWindow/
    [Window] TransitionFade.prefab  -- Fade-переход
  Wallet/
    [W] Wallet Window.prefab        -- Отображение кошелька
  GoogleSheetWindow/
    [Window] GoogleSheetConfig.prefab
    [Window] GoogleSheetStatus.prefab
  Debug/
    DEBUG_HUD.prefab                -- Debug HUD
    DEBUG_HUB.prefab                -- Debug Hub
  [Window] LoadingScreen.prefab     -- Экран загрузки
  World/
    Health Bar.prefab               -- World-space полоска здоровья
```

### Resources

- `UI/UIRoot.prefab` -- Root canvas, инстанциируемый UIFactory

---

## 7. ScriptableObject Assets

| Asset | Path | Purpose |
|-------|------|---------|
| `CropsStaticData.asset` | `Resources/StaticData/Crops/` | Определения культур (meshes, materials, types) |
| `WindowStaticData.asset` | `Resources/StaticData/Window/` | Привязка WindowId к префабам окон |
| `SettingsStaticData.asset` | `Resources/StaticData/Settings/` | Глобальные настройки (UseJSONAsConfig) |
| `PrefabStaticData.asset` | `Resources/StaticData/Prefabs/` | Ссылки на префабы (пусто) |
| `MaterialsStaticData.asset` | `Resources/StaticData/Materials/` | Ссылки на материалы (пусто) |
| `VFXStaticData.asset` | `Resources/StaticData/VFX/` | Ссылки на VFX (пусто) |
| `PC_RPAsset.asset` | `Project/Settings/` | URP pipeline (PC) |
| `Mobile_RPAsset.asset` | `Project/Settings/` | URP pipeline (Mobile) |
| `PC_Renderer.asset` | `Project/Settings/` | URP renderer (PC) |
| `Mobile_Renderer.asset` | `Project/Settings/` | URP renderer (Mobile) |

---

## 8. 3D Models

`Assets/Project/Models/Veggies/`:
- `Carrot1_P.fbx`
- `Corn1_P.fbx`
- `Sunflower1_P.fbx`
- `Tomatoes1_P.fbx`
- `pyramid.fbx` (placeholder)

---

## 9. Architecture Overview

### Game Type

**Farming / crop simulation** с изометрической top-down камерой. Игрок перемещается по полю и использует инструменты для взаимодействия с культурами.

### Core Loop

1. **Посадка** культур на grid-based полях (Grass, Wheat, Corn, Tomato)
2. **Полив** посаженных культур (рост: `cropState.y` от 0 до 1)
3. **Срезка/сбор** выросших культур

Таргет: **PC и мобильные** (раздельные URP pipeline, SimpleInput для виртуальных джойстиков).

### Game State Flow

```
Game.cs (MonoBehaviour, entry point)
  |
  v
GameStateMachine
  |-- BootstrapState ---------> регистрация 20+ сервисов
  |-- SelectGoogleSheetState -> (optional) выбор Google Sheet
  |-- LoadGoogleSheetState ---> загрузка конфига из Google Sheets
  |-- LoadProgressState ------> загрузка/создание PlayerProgress
  |-- HubState ---------------> главное меню, кнопка Start
  |-- LoadLevelState ---------> сброс кошелька/таймера
  |-- LevelState -------------> ОСНОВНОЙ ГЕЙМПЛЕЙ (tick: движение, инструменты, ввод)
  |-- LevelResultState -------> обработка win/lose
  |-- CleanupGameState -------> очистка, возврат в hub
```

*Текущее состояние: `BootstrapState` пропускает Google Sheets и сразу входит в `LevelState`.*

### System Interaction Diagram

```
                    AllServices (Service Locator)
                           |
         +-----------------+-----------------+
         |                 |                 |
   InputService    StaticDataService   WindowService
         |                 |                 |
         v                 v                 v
  PlayerMovementSystem  CropsStaticData   UIFactory
         |                 |                 |
         v                 v                 v
      Player.cs     CropFieldData      WindowBase (UI)
         |                 |
         v                 v
   Rigidbody +      CropRenderer
   Shader Global    (GPU Instanced Indirect)
   (_Interaction_       |
    Position)      ComputeBuffer
                        |
                   GetStructedBuffer.hlsl
                        |
                   Grass Instanced.shadergraph
```

---

## 10. Key Architectural Patterns

### Service Locator (Generic Static Class Trick)

`AllServices` использует `Implementation<T>.ServiceInstance` для O(1) type-based разрешения сервисов без аллокаций.

### State Machine with Tick Routing

Состояния опционально реализуют `ITick`/`IFixedTick`/`ILateTick`. Машина маршрутизирует Unity lifecycle-вызовы в активное состояние.

### GPU Instanced Indirect Rendering

Культуры рендерятся полностью на GPU через `DrawMeshInstancedIndirect` со structured buffers. Тысячи инстансов с минимальной CPU нагрузкой. Каждый инстанс: матрица позиции, матрица земли, цвет, UV, cropState вектор.

### Data-Driven Configuration

Google Sheets как live config backend через BakingSheet. Baked JSON fallback для offline сборок.

### Event System

MoreMountains-style type-safe глобальный event broadcasting через struct events.

### Object Pooling

LeanPool для combat text и spawnable объектов.

---

## 11. Current Project State

Проект находится на **ранней стадии разработки** (v0.1.0). Многие системы **scaffolded, но не заполнены**:

- Пустые ScriptableObject placeholders (Prefabs, Materials, VFX)
- Закомментированные пути кода (Google Sheets flow)
- Системы из шаблона, не специфичные для фарминга (combat text, rarity system, enemy layers, health bars)
- Название "Service Template" указывает на происхождение из переиспользуемого шаблона

**Работающие системы:**
- GPU instanced crop rendering
- Tool system (plant/water/cut)
- Player movement (isometric)
- State machine game flow
- UI window system
- Save/load infrastructure
