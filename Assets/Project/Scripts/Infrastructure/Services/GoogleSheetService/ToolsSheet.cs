using Cathei.BakingSheet;

public class ToolsSheet : Sheet<ToolsSheet.ToolsConfig>
{
    public class ToolsConfig : SheetRow<string>
    {
        public string next_id { get; private set; }
        public int spawn_shop { get; private set; }
        public string spawn_velocity { get; private set; }
        public string spawn_rotate { get; private set; }
        public float spawn_interval { get; private set; }
        public float first_spawn_accelerate { get; private set; }
        public float size { get; private set; }
        public float lifetime { get; private set; }
        public float damage_from { get; private set; }
        public float damage_to { get; private set; }
        public float damage_rate { get; private set; }
    }
}