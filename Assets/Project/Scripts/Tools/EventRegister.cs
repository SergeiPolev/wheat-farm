namespace MoreMountains.Tools
{
    /// <summary>
    /// Static class that allows any class to start or stop listening to events
    /// </summary>
    public static class EventRegister
    {
        public delegate void Delegate<T>( T eventType );

        public static void MMEventStartListening<EventType>( this MMEventListener<EventType> caller ) where EventType : struct
        {
            MMEventManager.AddListener<EventType>( caller );
        }

        public static void MMEventStopListening<EventType>( this MMEventListener<EventType> caller ) where EventType : struct
        {
            MMEventManager.RemoveListener<EventType>( caller );
        }
    }
}