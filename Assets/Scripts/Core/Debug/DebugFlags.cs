namespace WheatFarm.Core
{
    /// <summary>
    /// Central debug/cheat state. GodMode is a master switch that implies the resource cheats.
    /// Read by WalletService, InventoryService and PlantSystem.
    /// </summary>
    public interface IDebugFlags
    {
        bool GodMode { get; set; }
        bool InfiniteSeeds { get; set; }
        bool InfiniteCoins { get; set; }
        bool InfiniteResources { get; set; }
        bool InstantGrowth { get; set; }

        /// <summary>Seeds never deplete.</summary>
        bool SeedsAreFree { get; }
        /// <summary>Any non-seed inventory item never depletes.</summary>
        bool ResourcesAreFree { get; }
        /// <summary>Coins never deplete.</summary>
        bool CoinsAreFree { get; }
    }

    public class DebugFlags : IDebugFlags
    {
        public bool GodMode { get; set; }
        public bool InfiniteSeeds { get; set; }
        public bool InfiniteCoins { get; set; }
        public bool InfiniteResources { get; set; }
        public bool InstantGrowth { get; set; }

        public bool SeedsAreFree => GodMode || InfiniteResources || InfiniteSeeds;
        public bool ResourcesAreFree => GodMode || InfiniteResources;
        public bool CoinsAreFree => GodMode || InfiniteCoins;
    }
}
