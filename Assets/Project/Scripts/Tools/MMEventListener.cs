namespace MoreMountains.Tools
{
    /// <summary>
    /// A public interface you'll need to implement for each type of event you want to listen to.
    /// </summary>
    public interface MMEventListener<T> : MMEventListenerBase
    {
        void OnMMEvent( T eventType );
    }
}