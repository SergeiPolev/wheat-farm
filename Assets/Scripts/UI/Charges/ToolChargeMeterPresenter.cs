using System;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer.Unity;
using WheatFarm.Core;
using WheatFarm.Core.Data;
using WheatFarm.Player;
using WheatFarm.Player.Charges;
using WheatFarm.Player.Tools;

namespace WheatFarm.UI
{
    /// <summary>
    /// Floating container meter above the player for the active tool (can / bucket / seed bag / basket),
    /// plus a bobbing marker over the station that refills it when it runs dry.
    /// Built programmatically on the HUD canvas; all motion is eased in Tick (no tween dependency).
    /// </summary>
    public class ToolChargeMeterPresenter : IStartable, ITickable, IDisposable
    {
        private const float BarWidth = 150f;
        private const float BarHeight = 16f;
        private const float HintSeconds = 2.2f;

        private readonly Transform _canvasRoot;
        private readonly IToolChargeService _charges;
        private readonly IToolStationService _stations;
        private readonly IToolService _tools;
        private readonly PlacementTool _placementTool;
        private readonly ISfxService _sfx;
        private readonly CompositeDisposable _subs = new();
        private float _fullTimer;

        private RectTransform _meter, _fill, _flash, _marker, _markerDiamond;
        private Image _fillImage, _flashImage, _bgImage, _markerImage;
        private TextMeshProUGUI _label, _hint, _markerLabel;
        private CanvasGroup _meterGroup, _markerGroup, _hintGroup;

        private Transform _player;
        private Transform _head; // humanoid head bone: meter follows the model's real height
        private Camera _cam;
        private ChargeKind? _kind;
        private float _shownFraction, _alpha, _markerAlpha, _hintTimer, _shake, _pulse, _flashT;
        private float _depletedTimer;
        private ChargeKind _depletedKind;

        public ToolChargeMeterPresenter(
            Transform canvasRoot,
            IToolChargeService charges,
            IToolStationService stations,
            IToolService tools,
            PlacementTool placementTool = null,
            ISfxService sfx = null)
        {
            _sfx = sfx;
            _canvasRoot = canvasRoot;
            _charges = charges;
            _stations = stations;
            _tools = tools;
            _placementTool = placementTool;
        }

        public void Start()
        {
            Build();
            _charges.OnDepleted.Subscribe(OnDepleted).AddTo(_subs);
            _charges.OnTransferred.Subscribe(OnTransferred).AddTo(_subs);
            _tools.CurrentToolId.Skip(1).Subscribe(_ => _sfx?.Play(SfxId.ToolSwitch)).AddTo(_subs);
        }

        // ── Behaviour ─────────────────────────────────────────

        private void OnTransferred((ChargeKind kind, int amount) t)
        {
            if (_kind == t.kind) _pulse = 1f;
            float f = _charges.Fraction(t.kind);
            bool basket = t.kind == ChargeKind.Basket;
            // Glug pitch climbs as a container fills (falls as the basket empties)
            _sfx?.Play(SfxId.RefillTick, basket ? 1.3f - 0.5f * (1f - f) : 0.8f + 0.6f * f);

            bool done = basket ? _charges.Basket.CurrentValue == 0 : _charges.Current(t.kind) >= _charges.Max(t.kind);
            if (done && (_kind == null || _kind == t.kind))
            {
                _sfx?.Play(basket ? SfxId.Unload : SfxId.Full);
                if (_kind == t.kind) _fullTimer = 0.9f;
            }
        }

        private void OnDepleted(ChargeKind kind)
        {
            if (_hintTimer <= 0.2f) _sfx?.Play(SfxId.Empty);
            _depletedKind = kind;
            _depletedTimer = HintSeconds;
            if (_hintTimer <= 0.2f) _shake = 1f;       // don't re-shake every frame while the button is held
            _hintTimer = HintSeconds;
            _flashT = 1f;
            _hint.text = HintFor(kind);
        }

        private string HintFor(ChargeKind kind)
        {
            var station = _stations.StationFor(kind) == StationKind.Well ? "Well" : "Warehouse";
            return kind switch
            {
                ChargeKind.Water => $"Watering can empty — refill at the {station}",
                ChargeKind.Dye => $"Paint bucket empty — refill at the {station}",
                ChargeKind.Seeds => SeedHint(),
                ChargeKind.Basket => "Basket full — unload at the Warehouse",
                _ => ""
            };
        }

        private string SeedHint()
        {
            var plant = _placementTool?.SelectedPlant;
            var name = plant != null ? plant.DisplayName : "seed";
            if (_charges.Seeds.CurrentValue > 0 && plant != null && _charges.SeedPlantId.CurrentValue != plant.PlantId)
                return $"Bag holds other seeds — swap at the Warehouse";
            return $"Seed bag empty — load {name} at the Warehouse";
        }

        private ChargeKind? ActiveKind()
        {
            switch (_tools.CurrentToolId.CurrentValue)
            {
                case ToolId.WateringCan: return ChargeKind.Water;
                case ToolId.Dye: return ChargeKind.Dye;
                case ToolId.Sickle: return ChargeKind.Basket;
                case ToolId.Placement:
                case ToolId.Planter:
                    var p = _placementTool?.SelectedPlant;
                    return p != null && p.Category != PlantCategory.Tree ? ChargeKind.Seeds : null;
                default: return null;
            }
        }

        private bool NeedsStation(ChargeKind k) =>
            k == ChargeKind.Basket ? !_charges.BasketHasRoom : _charges.Current(k) <= 0
            || (k == ChargeKind.Seeds && _placementTool?.SelectedPlant != null
                && !_charges.CanSow(_placementTool.SelectedPlant.PlantId));

        public void Tick()
        {
            if (_meter == null) return;
            float dt = Time.unscaledDeltaTime;

            if (_player == null)
            {
                var pc = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                if (pc != null)
                {
                    _player = pc.transform;
                    var anim = pc.GetComponentInChildren<Animator>();
                    if (anim != null && anim.isHuman) _head = anim.GetBoneTransform(HumanBodyBones.Head);
                }
            }
            if (_cam == null) _cam = Camera.main;

            var kind = ActiveKind();
            if (kind != _kind)
            {
                _kind = kind;
                if (kind.HasValue) _shownFraction = _charges.Fraction(kind.Value);
                // A hint about another container is stale once the tool changes
                if (!kind.HasValue || kind.Value != _depletedKind) { _hintTimer = 0f; _depletedTimer = 0f; _flashT = 0f; }
            }

            _hintTimer = Mathf.Max(0f, _hintTimer - dt);
            _depletedTimer = Mathf.Max(0f, _depletedTimer - dt);
            _shake = Mathf.Max(0f, _shake - dt * 3.5f);
            _pulse = Mathf.Max(0f, _pulse - dt * 4f);
            _flashT = Mathf.Max(0f, _flashT - dt * 2.5f);
            _fullTimer = Mathf.Max(0f, _fullTimer - dt);

            var atStation = _stations.CurrentStation.CurrentValue;

            // Meter
            float targetAlpha = kind.HasValue && _player != null ? 1f : 0f;
            _alpha = Mathf.MoveTowards(_alpha, targetAlpha, dt * 6f);
            _meterGroup.alpha = _alpha;

            if (kind.HasValue && _player != null && _cam != null)
            {
                var k = kind.Value;
                float target = _charges.Fraction(k);
                _shownFraction = Mathf.Lerp(_shownFraction, target, 1f - Mathf.Exp(-12f * dt));
                _fill.sizeDelta = new Vector2(BarWidth * _shownFraction, BarHeight);

                var baseColor = ColorFor(k);
                bool low = k == ChargeKind.Basket ? target >= 0.85f : target <= 0.2f;
                if (low) baseColor = Color.Lerp(baseColor, new Color(1f, 0.55f, 0.15f), 0.35f + 0.35f * Mathf.Sin(Time.unscaledTime * 8f));
                _fillImage.color = baseColor;
                _flashImage.color = new Color(1f, 0.25f, 0.2f, 0.75f * _flashT);

                _label.text = _fullTimer > 0f
                    ? (k == ChargeKind.Basket ? "<color=#FFE070>Unloaded!</color>" : "<color=#B8FF9A>Full!</color>")
                    : LabelFor(k, atStation);

                var anchorPos = _head != null ? _head.position + Vector3.up * 0.55f : _player.position + Vector3.up * 2.3f;
                var screen = _cam.WorldToScreenPoint(anchorPos);
                float shakeX = Mathf.Sin(Time.unscaledTime * 60f) * 9f * _shake;
                SetScreenPos(_meter, screen, new Vector2(shakeX, 0f));
                float s = 1f + 0.12f * _pulse;
                _meter.localScale = new Vector3(s, s, 1f);
            }

            // Hint under the meter
            _hintGroup.alpha = Mathf.Clamp01(_hintTimer / 0.35f) * _alpha;

            // Station marker: show when the active container needs its station (or just failed)
            bool wantMarker = kind.HasValue && atStation == StationKind.None
                              && (NeedsStation(kind.Value) || _depletedTimer > 0f);
            Vector3 anchor = default;
            if (wantMarker)
            {
                var st = _stations.StationFor(kind.Value);
                wantMarker = _player != null && _stations.TryGetStationAnchor(st, _player.position, out anchor);
                _markerLabel.text = st == StationKind.Well ? "Well" : "Warehouse";
                _markerImage.color = ColorFor(kind.Value);
            }
            _markerAlpha = Mathf.MoveTowards(_markerAlpha, wantMarker ? 1f : 0f, dt * 5f);
            _markerGroup.alpha = _markerAlpha;
            if (_markerAlpha > 0f && _cam != null && anchor != default)
            {
                var sp = _cam.WorldToScreenPoint(anchor);
                float bob = Mathf.Sin(Time.unscaledTime * 4f) * 8f;
                SetScreenPos(_marker, sp, new Vector2(0f, 18f + bob));
                _markerDiamond.localRotation = Quaternion.Euler(0, 0, 45f);
            }
        }

        private string LabelFor(ChargeKind k, StationKind atStation)
        {
            int cur = _charges.Current(k), max = _charges.Max(k);
            bool transferring = (k is ChargeKind.Water or ChargeKind.Dye && atStation == _stations.StationFor(k) && cur < max)
                                || (k == ChargeKind.Basket && atStation == StationKind.Warehouse && cur > 0)
                                || (k == ChargeKind.Seeds && atStation == StationKind.Warehouse && cur < max);
            string verb = transferring ? (k == ChargeKind.Basket ? "Unloading… " : "Refilling… ") : "";
            string name = k switch
            {
                ChargeKind.Water => "Water",
                ChargeKind.Dye => "Paint",
                ChargeKind.Basket => "Basket",
                ChargeKind.Seeds => SeedName(),
                _ => ""
            };
            return $"{verb}{name}  {cur}/{max}";
        }

        private string SeedName()
        {
            var id = _charges.SeedPlantId.CurrentValue;
            var sel = _placementTool?.SelectedPlant;
            if (sel != null && (id == null || id == sel.PlantId)) return $"{sel.DisplayName} seeds";
            return id != null ? $"{id} seeds" : "Seeds";
        }

        private static Color ColorFor(ChargeKind k) => k switch
        {
            ChargeKind.Water => new Color(0.30f, 0.66f, 1.00f),
            ChargeKind.Dye => new Color(0.88f, 0.38f, 0.80f),
            ChargeKind.Seeds => new Color(0.46f, 0.82f, 0.36f),
            ChargeKind.Basket => new Color(1.00f, 0.80f, 0.28f),
            _ => Color.white
        };

        private void SetScreenPos(RectTransform rt, Vector3 screen, Vector2 offset)
        {
            if (screen.z < 0f) { rt.anchoredPosition = new Vector2(-9999, -9999); return; }
            var canvasRt = (RectTransform)_canvasRoot;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, null, out var local);
            rt.anchoredPosition = local + offset;
        }

        // ── Build ─────────────────────────────────────────────

        private void Build()
        {
            var root = new GameObject("ToolChargeMeter", typeof(RectTransform), typeof(CanvasGroup));
            _meter = (RectTransform)root.transform;
            _meter.SetParent(_canvasRoot, false);
            Center(_meter, new Vector2(BarWidth + 8f, BarHeight + 8f));
            _meterGroup = root.GetComponent<CanvasGroup>();
            _meterGroup.blocksRaycasts = false;
            _meterGroup.interactable = false;
            _meterGroup.alpha = 0f;
            root.transform.SetAsFirstSibling(); // under panels

            _bgImage = MakeImage(_meter, "Bg", new Color(0.08f, 0.09f, 0.10f, 0.78f), new Vector2(BarWidth + 8f, BarHeight + 8f));
            _flash = MakeImage(_meter, "Flash", new Color(1, 0.2f, 0.2f, 0), new Vector2(BarWidth + 8f, BarHeight + 8f)).rectTransform;
            _flashImage = _flash.GetComponent<Image>();

            var track = MakeImage(_meter, "Track", new Color(1f, 1f, 1f, 0.12f), new Vector2(BarWidth, BarHeight));
            _fillImage = MakeImage(track.rectTransform, "Fill", Color.white, new Vector2(0f, BarHeight));
            _fill = _fillImage.rectTransform;
            _fill.anchorMin = _fill.anchorMax = new Vector2(0f, 0.5f);
            _fill.pivot = new Vector2(0f, 0.5f);
            _fill.anchoredPosition = Vector2.zero;

            _label = MakeText(_meter, "Label", 21f, new Vector2(0f, BarHeight + 4f), new Vector2(260f, 24f));
            _label.fontStyle = FontStyles.Bold;
            _label.outlineWidth = 0.22f;
            _label.outlineColor = new Color32(0, 0, 0, 190);

            var hintGo = new GameObject("Hint", typeof(RectTransform), typeof(CanvasGroup));
            var hintRt = (RectTransform)hintGo.transform;
            hintRt.SetParent(_meter, false);
            Center(hintRt, new Vector2(420f, 26f));
            hintRt.anchoredPosition = new Vector2(0f, -(BarHeight + 12f));
            _hintGroup = hintGo.GetComponent<CanvasGroup>();
            _hintGroup.alpha = 0f;
            var hintBg = MakeImage(hintRt, "HintBg", new Color(0.55f, 0.12f, 0.08f, 0.85f), new Vector2(380f, 26f));
            _hint = MakeText(hintRt, "HintText", 17f, Vector2.zero, new Vector2(380f, 26f));

            // Station marker
            var mGo = new GameObject("StationMarker", typeof(RectTransform), typeof(CanvasGroup));
            _marker = (RectTransform)mGo.transform;
            _marker.SetParent(_canvasRoot, false);
            Center(_marker, new Vector2(120f, 60f));
            _markerGroup = mGo.GetComponent<CanvasGroup>();
            _markerGroup.alpha = 0f;
            _markerGroup.blocksRaycasts = false;
            mGo.transform.SetAsFirstSibling();
            _markerImage = MakeImage(_marker, "Diamond", Color.white, new Vector2(20f, 20f));
            _markerDiamond = _markerImage.rectTransform;
            _markerDiamond.anchoredPosition = new Vector2(0f, -6f);
            var outline = _markerImage.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.6f);
            outline.effectDistance = new Vector2(2f, -2f);
            _markerLabel = MakeText(_marker, "MarkerLabel", 18f, new Vector2(0f, 18f), new Vector2(160f, 26f));
            _markerLabel.fontStyle = FontStyles.Bold;
            _markerLabel.outlineWidth = 0.25f;
            _markerLabel.outlineColor = new Color32(0, 0, 0, 200);
        }

        private static void Center(RectTransform rt, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
        }

        private static Image MakeImage(RectTransform parent, string name, Color c, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Center(rt, size);
            var img = go.GetComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        private static TextMeshProUGUI MakeText(RectTransform parent, string name, float size, Vector2 pos, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Center(rt, box);
            rt.anchoredPosition = pos;
            var t = go.GetComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        public void Dispose()
        {
            _subs.Dispose();
            if (_meter != null) UnityEngine.Object.Destroy(_meter.gameObject);
            if (_marker != null) UnityEngine.Object.Destroy(_marker.gameObject);
        }
    }
}
