#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using VContainer;
using WheatFarm.Buildings;
using WheatFarm.Core.Data;
using WheatFarm.Farming;
using WheatFarm.Inventory;
using WheatFarm.Player;
using WheatFarm.Player.Charges;
using WheatFarm.Player.Tools;

namespace WheatFarm.Infrastructure.Cheats
{
    /// <summary>
    /// Dev-only live run of the core loop in real frames: walks the player between the field,
    /// Well and Warehouse, sows / waters / waits for growth / paints / harvests through the real
    /// ToolService (same path as mouse input), and logs PASS/FAIL per step plus frame-time stats.
    /// Add the component in Play Mode (e.g. via MCP execute_code); read CoreLoopAutoplay.Report.
    /// Screenshots go to Temp/Screens/live_*.png.
    /// </summary>
    public class CoreLoopAutoplay : MonoBehaviour
    {
        public static string Report = "";
        public static bool Done;

        private readonly StringBuilder _log = new();
        private IObjectResolver _c;
        private IToolService _tools;
        private IToolChargeService _charges;
        private IToolStationService _stations;
        private IChunkSystem _chunks;
        private IInventoryService _inventory;
        private IBrushService _brush;
        private IPlacementService _placement;
        private PlacementTool _placementTool;
        private DyeTool _dyeTool;
        private Transform _player;
        private Rigidbody _rb;
        private float _t0;
        private int _frames;
        private float _worstDt, _sumDt;
        private int _depleted;
        private int _fails;

        // Field patch north of the starter Well
        private static readonly Vector3 FieldA = new(0.6f, 0f, 0.4f);
        private static readonly Vector3 FieldB = new(3.4f, 0f, 2.4f);

        private static CoreLoopAutoplay _running;

        private void Start()
        {
            // Tool-call retries can add the component twice; two drivers would fight over the tools.
            if (_running != null && _running != this) { Destroy(gameObject); return; }
            _running = this;
            Done = false;
            Report = "running";
            Run().Forget();
        }

        private void Update()
        {
            if (Done) return;
            _frames++;
            float dt = Time.unscaledDeltaTime;
            _sumDt += dt;
            if (_frames > 5) _worstDt = Mathf.Max(_worstDt, dt);
        }

        private async UniTaskVoid Run()
        {
            try
            {
                await Setup();
                await Sow();
                await Water();
                await Grow();
                await Paint();
                await Harvest();
            }
            catch (Exception e)
            {
                Fail("EXCEPTION " + e.Message);
                Debug.LogException(e);
            }

            Log($"frames={_frames} avgDt={_sumDt / Mathf.Max(1, _frames) * 1000f:F1}ms worstDt={_worstDt * 1000f:F1}ms depletedEvents={_depleted}");
            Log(_fails == 0 ? "RESULT: ALL STEPS PASSED" : $"RESULT: {_fails} FAILURE(S)");
            Report = _log.ToString();
            Done = true;
            _running = null;
            Debug.Log("[Autoplay]\n" + Report);
        }

        // ── Steps ────────────────────────────────────────────

        private async UniTask Setup()
        {
            var scope = FindFirstObjectByType<FarmScope>();
            _c = scope.Container;
            _tools = _c.Resolve<IToolService>();
            _charges = _c.Resolve<IToolChargeService>();
            _stations = _c.Resolve<IToolStationService>();
            _chunks = _c.Resolve<IChunkSystem>();
            _inventory = _c.Resolve<IInventoryService>();
            _brush = _c.Resolve<IBrushService>();
            _placement = _c.Resolve<IPlacementService>();
            _placementTool = _c.Resolve<PlacementTool>();
            _dyeTool = _c.Resolve<DyeTool>();
            var pc = FindFirstObjectByType<PlayerController>();
            _player = pc.transform;
            _rb = pc.GetComponent<Rigidbody>();
            _t0 = Time.time;
            _charges.OnDepleted.Subscribe(_ => _depleted++).AddTo(this);

            AudioListener.volume = 0f; // silent run (it may be running at night next to someone asleep)
            await UniTask.DelayFrame(3);
            _brush.CurrentSize.Value = BrushSize.Large;
            _inventory.TryAdd(new InventoryItem("seed_wheat", ItemType.Seed, 60)); // as if bought at the shop

            // Small containers so one run exercises every station trip (Well x2, Warehouse unload).
            _charges.ReturnSeeds();
            var cfg = _charges.Config;
            cfg.WaterMax = 30; cfg.DyeMax = 30; cfg.BasketMax = 20; cfg.SeedMax = 40;
            _charges.ResetToNewGame();
            _charges.LoadSeeds("wheat", 10); // starter bag
            Log($"setup: bag={_charges.Seeds.CurrentValue} water={_charges.Water.CurrentValue} paint={_charges.Dye.CurrentValue} stored seeds={_inventory.GetAmount("seed_wheat")} (+60 debug)");
            Check(HasStation(ToolChargeConfig.WellId), "starter Well exists");
            Check(HasStation(ToolChargeConfig.WarehouseId), "Warehouse exists");
            Shot("00_start");
        }

        private async UniTask Sow()
        {
            _tools.EquipTool(ToolId.Placement);
            var wheat = _placementTool.SelectedPlant;
            Check(wheat != null && wheat.PlantId == "wheat", "wheat auto-selected");

            await WalkTo(FieldA + new Vector3(-0.6f, 0, 0));
            int before = _depleted;
            await SweepField(stopWhen: () => _charges.Seeds.CurrentValue == 0);
            int planted = CountCells(c => c.HasPlant);
            Log($"sow #1: planted={planted} bag={_charges.Seeds.CurrentValue}");
            await SweepField(0.6f); // keep "clicking" with an empty bag
            Check(_depleted > before, "empty seed bag reports depletion");
            Shot("01_bag_empty_hint");

            await WalkToStation(StationKind.Warehouse);
            await WaitUntil(() => _charges.Seeds.CurrentValue >= _charges.Config.SeedMax || _inventory.GetAmount("seed_wheat") == 0, 6f);
            Log($"warehouse: bag={_charges.Seeds.CurrentValue} stored={_inventory.GetAmount("seed_wheat")}");
            Check(_charges.Seeds.CurrentValue > 0, "Warehouse loads the seed bag");
            Shot("02_warehouse_load");

            await WalkTo(FieldA + new Vector3(-0.6f, 0, 0));
            await SweepField(stopWhen: () => _charges.Seeds.CurrentValue == 0 || FieldFull());
            int planted2 = CountCells(c => c.HasPlant);
            Log($"sow #2: planted={planted2}");
            Check(planted2 > planted, "second sowing adds plants");
        }

        private async UniTask Water()
        {
            _tools.EquipTool(ToolId.WateringCan);
            int trips = 0;
            for (int pass = 0; pass < 6 && CountCells(c => c.HasPlant && !c.Watered) > 0; pass++)
            {
                await SweepField(stopWhen: () => !_charges.HasWater || CountCells(c => c.HasPlant && !c.Watered) == 0);
                if (!_charges.HasWater)
                {
                    Shot("03_can_empty_marker");
                    trips++;
                    await WalkToStation(StationKind.Well);
                    await WaitUntil(() => _charges.Water.CurrentValue >= _charges.Config.WaterMax, 5f);
                    Log($"well trip {trips}: water={_charges.Water.CurrentValue}");
                    if (trips == 1) Shot("04_well_refill");
                    Check(_charges.Water.CurrentValue == _charges.Config.WaterMax, "Well refills the can");
                    await WalkTo(FieldA + new Vector3(-0.6f, 0, 0));
                }
            }
            int thirsty = CountCells(c => c.HasPlant && !c.Watered);
            int water = _charges.Water.CurrentValue;
            await SweepField(0.8f); // hold over a fully watered field
            Check(_charges.Water.CurrentValue == water, "no water spent on already-watered plants");
            Log($"water: thirsty left={thirsty} can={water} trips={trips}");
            Check(thirsty == 0, "all plants watered");
        }

        private async UniTask Grow()
        {
            float start = Time.time;
            await WaitUntil(() => CountCells(c => c.HasPlant && !c.IsHarvestable) == 0, 25f);
            Log($"grow: all mature after {Time.time - start:F1}s");
            Check(CountCells(c => c.HasPlant && !c.IsHarvestable) == 0, "plants grew to maturity");
            Shot("05_grown");
        }

        private async UniTask Paint()
        {
            _tools.EquipTool(ToolId.Dye);
            var colors = new[] { new Color(0.95f, 0.3f, 0.35f), new Color(1f, 0.78f, 0.2f), new Color(0.35f, 0.5f, 1f) };
            int paint0 = _charges.Dye.CurrentValue;
            for (int i = 0; i < 3; i++)
            {
                _dyeTool.SelectColor(colors[i]);
                float z = Mathf.Lerp(FieldA.z, FieldB.z, (i + 0.5f) / 3f);
                await Sweep(new Vector3(FieldA.x, 0, z), new Vector3(FieldB.x, 0, z), 0.8f);
                if (_charges.Dye.CurrentValue < 10)
                {
                    await WalkToStation(StationKind.Well);
                    await WaitUntil(() => _charges.Dye.CurrentValue >= _charges.Config.DyeMax, 5f);
                    await WalkTo(FieldA + new Vector3(-0.6f, 0, 0));
                }
            }
            int painted = CountCells(c => c.HasPlant && c.Color.a > 0f && (c.Color.r < 0.9f || c.Color.g < 0.9f || c.Color.b < 0.9f));
            Log($"paint: painted={painted} paint {paint0}->{_charges.Dye.CurrentValue}");
            Check(painted > 0, "painting colors plants");

            // Same-color repaint must be free: top up, finish the last stripe, then sweep it again.
            if (_charges.Dye.CurrentValue < _charges.Config.DyeMax)
            {
                await WalkToStation(StationKind.Well);
                await WaitUntil(() => _charges.Dye.CurrentValue >= _charges.Config.DyeMax, 5f);
                await WalkTo(FieldA + new Vector3(-0.6f, 0, 0));
            }
            float lastZ = Mathf.Lerp(FieldA.z, FieldB.z, 2.5f / 3f);
            var la = new Vector3(FieldA.x, 0, lastZ);
            var lb = new Vector3(FieldB.x, 0, lastZ);
            await Sweep(la, lb, 0.8f);
            int p = _charges.Dye.CurrentValue;
            await Sweep(la, lb, 0.8f);
            Log($"repaint same stripe: paint {p}->{_charges.Dye.CurrentValue}");
            Check(_charges.Dye.CurrentValue == p, "repainting the same color costs nothing");
            Shot("06_painted");
        }

        private async UniTask Harvest()
        {
            _placementTool.ClearSelection(); // what the catalog / "4" hotkey do
            _tools.EquipTool(ToolId.Sickle);
            int crops = CountCells(c => c.IsHarvestable);
            int wheat0 = _inventory.GetAmount("wheat");
            int unloads = 0;
            for (int pass = 0; pass < 8 && CountCells(c => c.IsHarvestable) > 0; pass++)
            {
                await SweepField(stopWhen: () => !_charges.BasketHasRoom || CountCells(c => c.IsHarvestable) == 0);
                if (!_charges.BasketHasRoom)
                {
                    if (unloads == 0) Shot("07_basket_full");
                    await WalkToStation(StationKind.Warehouse);
                    await WaitUntil(() => _charges.Basket.CurrentValue == 0, 6f);
                    unloads++;
                    await WalkTo(FieldA + new Vector3(-0.6f, 0, 0));
                }
            }
            Check(_inventory.GetAmount("wheat") >= wheat0 || unloads == 0, "unloading moves crops to inventory");
            await WalkToStation(StationKind.Warehouse);
            await WaitUntil(() => _charges.Basket.CurrentValue == 0, 6f);
            int got = _inventory.GetAmount("wheat") - wheat0;
            Log($"harvest: crops={crops} delivered={got} unloads={unloads} basket={_charges.Basket.CurrentValue}");
            // Sickle in hand, catalog selection cleared: the Warehouse still loads the last crop's seeds.
            await WaitUntil(() => _charges.Seeds.CurrentValue > 0, 3f);
            Log($"bag with sickle in hand: {_charges.Seeds.CurrentValue} ({_charges.SeedPlantId.CurrentValue}), lastCrop={_placementTool.LastCrop?.PlantId}");
            Check(_placementTool.SelectedPlant == null && _charges.Seeds.CurrentValue > 0 && _charges.SeedPlantId.CurrentValue == "wheat",
                  "Warehouse reloads the bag for the last crop while holding another tool");
            Check(got == crops, "every harvested crop reaches inventory");
            Shot("08_delivered");
        }

        // ── Helpers ──────────────────────────────────────────

        private async UniTask SweepField(float maxSeconds = 12f, Func<bool> stopWhen = null)
        {
            // Serpentine rows across the patch, like dragging the mouse while holding the button.
            float end = Time.time + maxSeconds;
            const int rows = 4;
            for (int r = 0; r < rows && Time.time < end; r++)
            {
                float z = Mathf.Lerp(FieldA.z, FieldB.z, (r + 0.5f) / rows);
                var a = new Vector3(r % 2 == 0 ? FieldA.x : FieldB.x, 0, z);
                var b = new Vector3(r % 2 == 0 ? FieldB.x : FieldA.x, 0, z);
                await Sweep(a, b, Mathf.Min(1.2f, end - Time.time), stopWhen);
                if (stopWhen != null && stopWhen()) return;
            }
        }

        private async UniTask Sweep(Vector3 a, Vector3 b, float seconds, Func<bool> stopWhen = null)
        {
            float t = 0f;
            while (t < seconds)
            {
                if (stopWhen != null && stopWhen()) return;
                _tools.UseCurrentTool(Vector3.Lerp(a, b, t / seconds));
                t += Time.deltaTime;
                await UniTask.Yield();
            }
        }

        private async UniTask WalkToStation(StationKind kind)
        {
            if (!_stations.TryGetStationAnchor(kind, _player.position, out var anchor))
            {
                Fail($"no {kind} anchor");
                return;
            }
            var dir = _player.position - anchor;
            dir.y = 0;
            var target = anchor + dir.normalized * 1.5f;
            await WalkTo(target, () => _stations.CurrentStation.CurrentValue == kind);
            Check(_stations.CurrentStation.CurrentValue == kind, $"arrived at {kind}");
        }

        private async UniTask WalkTo(Vector3 target, Func<bool> arrived = null)
        {
            target.y = _player.position.y;
            float timeout = Time.time + 12f;
            const float speed = 4f;
            while (Time.time < timeout)
            {
                if (arrived != null && arrived()) return;
                var pos = _player.position;
                var to = target - pos;
                to.y = 0;
                if (to.magnitude < 0.08f) { if (arrived == null) return; else { await UniTask.Yield(); continue; } }
                var next = pos + to.normalized * Mathf.Min(to.magnitude, speed * Time.deltaTime);
                if (_rb != null) { _rb.position = next; _rb.linearVelocity = Vector3.zero; }
                _player.position = next;
                _player.rotation = Quaternion.Slerp(_player.rotation, Quaternion.LookRotation(to.normalized), 0.3f);
                await UniTask.Yield();
            }
        }

        private static async UniTask WaitUntil(Func<bool> cond, float timeout)
        {
            float end = Time.time + timeout;
            while (!cond() && Time.time < end) await UniTask.Yield();
        }

        private bool FieldFull() => CountCells(c => !c.HasPlant && !c.Occupied) == 0;

        private int CountCells(Func<SubCellState, bool> pred)
        {
            int n = 0;
            foreach (var ch in _chunks.GetAllUnlockedChunks())
            {
                for (int y = 0; y < ch.Resolution; y++)
                for (int x = 0; x < ch.Resolution; x++)
                {
                    var w = _chunks.CellToWorld(ch.ChunkCoord, x, y);
                    if (w.x < FieldA.x - 0.8f || w.x > FieldB.x + 0.8f || w.z < FieldA.z - 0.8f || w.z > FieldB.z + 0.8f) continue;
                    if (pred(ch.Cells[ch.CellIndex(x, y)])) n++;
                }
            }
            return n;
        }

        private bool HasStation(string id) => _placement.PlacedObjects.Any(p => p.Data != null && p.Data.PlaceableId == id);

        private void Shot(string name)
        {
            System.IO.Directory.CreateDirectory("Temp/Screens");
            ScreenCapture.CaptureScreenshot($"Temp/Screens/live_{name}.png");
        }

        private void Check(bool ok, string what)
        {
            if (ok) Log("PASS " + what);
            else Fail(what);
        }

        private void Fail(string what)
        {
            _fails++;
            Log("FAIL " + what);
        }

        private void Log(string s) => _log.AppendLine($"[{Time.time - _t0,6:F1}s] {s}");
    }
}
#endif
