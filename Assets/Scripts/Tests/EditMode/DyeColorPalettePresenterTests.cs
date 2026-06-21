using System;
using System.Collections.Generic;
using NUnit.Framework;
using R3;
using UnityEngine;
using WheatFarm.Core.Data;
using WheatFarm.Economy;
using WheatFarm.Player.Tools;
using WheatFarm.UI;

namespace WheatFarm.Tests
{
    public class DyeColorPalettePresenterTests
    {
        // --- Fakes ---

        private class FakeToolService : IToolService
        {
            private readonly ReactiveProperty<ITool> _tool = new(null);
            private readonly ReactiveProperty<ToolId> _id = new(ToolId.Planter);
            public ReadOnlyReactiveProperty<ITool> CurrentTool => _tool;
            public ReadOnlyReactiveProperty<ToolId> CurrentToolId => _id;
            public void EquipTool(ToolId id) => _id.Value = id;
            public void UseCurrentTool(Vector3 worldPos) { }
            public void Dispose() { }
        }

        private class FakeView : IDyeColorPaletteView
        {
            public event Action<int> SwatchClicked;
            public bool? Visible;
            public bool Built;
            public readonly Dictionary<int, (bool unlocked, bool selected, int cost)> States = new();
            public void Build(IReadOnlyList<DyeData> dyes) => Built = true;
            public void SetVisible(bool visible) => Visible = visible;
            public void SetSwatchState(int index, bool unlocked, bool selected, int cost)
                => States[index] = (unlocked, selected, cost);
            public void Click(int index) => SwatchClicked?.Invoke(index);
        }

        // --- Helpers ---

        private static DyeData Dye(string id, Color color, int cost)
        {
            var d = ScriptableObject.CreateInstance<DyeData>();
            d.DyeId = id;
            d.Color = color;
            d.Cost = cost;
            return d;
        }

        private static DyeDatabase Db(params DyeData[] dyes)
        {
            var db = ScriptableObject.CreateInstance<DyeDatabase>();
            db.Items = dyes;
            return db;
        }

        private FakeToolService _tools;
        private FakeView _view;
        private WalletService _wallet;
        private DyeUnlockService _unlock;
        private DyeTool _dyeTool;

        [SetUp]
        public void SetUp()
        {
            _tools = new FakeToolService();
            _view = new FakeView();
            _wallet = new WalletService();
            _unlock = new DyeUnlockService(_wallet);
            _dyeTool = new DyeTool(null, null); // SelectColor only touches its own field
        }

        [TearDown]
        public void TearDown() => _wallet.Dispose();

        private DyeColorPalettePresenter Build(DyeDatabase db)
        {
            var p = new DyeColorPalettePresenter(_view, db, _unlock, _tools, _dyeTool);
            p.Initialize();
            return p;
        }

        // --- Tests ---

        [Test]
        public void Palette_Visible_Only_When_Dye_Tool_Active()
        {
            Build(Db(Dye("white", Color.white, 0)));

            _tools.EquipTool(ToolId.Dye);
            Assert.IsTrue(_view.Visible);

            _tools.EquipTool(ToolId.Sickle);
            Assert.IsFalse(_view.Visible);
        }

        [Test]
        public void ClickUnlocked_SelectsColor()
        {
            Build(Db(Dye("white", Color.white, 0)));

            _view.Click(0); // white, Cost 0 -> already unlocked

            Assert.AreEqual(Color.white, _dyeTool.SelectedColor);
        }

        [Test]
        public void ClickLocked_Affordable_Unlocks_ThenSelects()
        {
            _wallet.SetCoins(100);
            var red = Dye("red", Color.red, 20);
            Build(Db(Dye("white", Color.white, 0), red));

            _view.Click(1); // red, locked but affordable

            Assert.IsTrue(_unlock.IsUnlocked(red));
            Assert.AreEqual(Color.red, _dyeTool.SelectedColor);
            Assert.AreEqual(80, _wallet.Coins.CurrentValue);
        }

        [Test]
        public void ClickLocked_Unaffordable_DoesNotSelect()
        {
            _wallet.SetCoins(5);
            var red = Dye("red", Color.red, 20);
            Build(Db(Dye("white", Color.white, 0), red));

            _view.Click(1); // red, cannot afford

            Assert.IsFalse(_unlock.IsUnlocked(red));
            Assert.AreNotEqual(Color.red, _dyeTool.SelectedColor);
        }
    }
}
