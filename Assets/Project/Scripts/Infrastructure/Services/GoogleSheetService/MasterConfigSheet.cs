using Cathei.BakingSheet;

public class MasterConfigSheet : Sheet<MasterConfigSheet.BalanceConfig>
{
    public class BalanceConfig : SheetRow
    {
        public string config_id { get; private set; }
        public string google_sheet_id { get; private set; }
    }
}