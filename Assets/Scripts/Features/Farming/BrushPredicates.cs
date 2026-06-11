namespace WheatFarm.Farming
{
    /// <summary>
    /// Per-tool cell applicability predicates — the single source of truth shared
    /// by brush application (BrushService) and brush preview (BrushPreviewService),
    /// so the preview never lies about which cells a stroke will change.
    /// Exact predicates per spec docs/superpowers/specs/2026-06-11-placement-preview-system-design.md §4.
    /// </summary>
    public static class BrushPredicates
    {
        public static bool Plantable(in SubCellState cell) =>
            !cell.Occupied && !cell.HasPlant;

        public static bool PathPaintable(in SubCellState cell, GroundState targetPath) =>
            !cell.HasPlant
            && (!cell.Occupied || cell.GroundState >= GroundState.PathStone)
            && cell.GroundState != targetPath;

        public static bool Bulldozable(in SubCellState cell) =>
            cell.GroundState >= GroundState.PathStone || cell.HasPlant;

        public static bool Harvestable(in SubCellState cell) => cell.IsHarvestable;

        /// <summary>Uproot, water, fertilize, dye — any cell with a plant.</summary>
        public static bool PlantTargeting(in SubCellState cell) => cell.HasPlant;
    }
}
