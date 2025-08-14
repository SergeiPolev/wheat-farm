namespace Cathei.BakingSheet.Examples.Google
{
    public class SheetContainer : SheetContainerBase
    {
        public SheetContainer(Microsoft.Extensions.Logging.ILogger logger) : base(logger)
        { }

        public ItemSheet Simple { get; set; }
        public CharacterSheet Complex { get; set; }
    }
}