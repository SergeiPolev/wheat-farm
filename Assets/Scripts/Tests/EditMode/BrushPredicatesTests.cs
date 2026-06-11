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
