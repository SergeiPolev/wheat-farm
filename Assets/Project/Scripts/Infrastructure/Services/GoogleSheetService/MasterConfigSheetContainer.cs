using Cathei.BakingSheet;

public class MasterConfigSheetContainer : SheetContainerBase
{
	public MasterConfigSheetContainer(Microsoft.Extensions.Logging.ILogger logger) : base(logger) { }
	public MasterConfigSheet balance { get; private set; }
}