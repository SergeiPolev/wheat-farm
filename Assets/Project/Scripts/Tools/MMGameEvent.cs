namespace MoreMountains.Tools
{
    /// <summary>
    /// MMGameEvents are used throughout the game for general game events (game started, game ended, life lost, etc.)
    /// </summary>
    public struct MMGameEvent
    {
        public string EventName;
        public MMGameEvent(string newName)
        {
            EventName = newName;
        }
        static MMGameEvent e;
        public static void Trigger(string newName)
        {
            e.EventName = newName;
            MMEventManager.TriggerEvent(e);
        }
    }
}