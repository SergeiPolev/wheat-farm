using NUnit.Framework;
using WheatFarm.Core.Data;
using WheatFarm.Player.HeldTools;
using WheatFarm.Player.Tools;

namespace WheatFarm.Tests
{
    public class HeldPropRulesTests
    {
        [TestCase(ToolId.WateringCan, HeldProp.WateringCan)]
        [TestCase(ToolId.Dye, HeldProp.PaintBucket)]
        [TestCase(ToolId.Sickle, HeldProp.Sickle)]
        [TestCase(ToolId.Planter, HeldProp.SeedBag)]
        [TestCase(ToolId.Fertilizer, HeldProp.SeedBag)]
        [TestCase(ToolId.Uproot, HeldProp.None)]
        [TestCase(ToolId.Bulldoze, HeldProp.None)]
        [TestCase(ToolId.Build, HeldProp.None)]
        public void ToolWithoutSelection_MapsToProp(ToolId tool, HeldProp expected)
        {
            Assert.AreEqual(expected, HeldPropRules.For(tool, null));
        }

        [TestCase(PlantCategory.Crop, HeldProp.SeedBag)]
        [TestCase(PlantCategory.Bush, HeldProp.SeedBag)]
        [TestCase(PlantCategory.Tree, HeldProp.None)]
        public void Placement_DependsOnSelectedPlant(PlantCategory category, HeldProp expected)
        {
            Assert.AreEqual(expected, HeldPropRules.For(ToolId.Placement, category));
        }

        [Test]
        public void Placement_WithBuildingOrNothingSelected_HoldsNothing()
        {
            Assert.AreEqual(HeldProp.None, HeldPropRules.For(ToolId.Placement, null));
        }
    }
}
