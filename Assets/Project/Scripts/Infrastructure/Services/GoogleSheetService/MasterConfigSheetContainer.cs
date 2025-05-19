using Cathei.BakingSheet;

public class MasterConfigSheetContainer : SheetContainerBase
{
	public MasterConfigSheetContainer(Microsoft.Extensions.Logging.ILogger logger) : base(logger) { }
	public MasterConfigSheet balance { get; private set; }
}

public class MasterConfigSheet : Sheet<MasterConfigSheet.BalanceConfig>
{
	public class BalanceConfig : SheetRow
	{
		public string config_id { get; private set; }
		public string google_sheet_id { get; private set; }
	}
}