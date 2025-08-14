using System;

namespace MoreMountains.Tools
{
    public class MMEventListenerWrapper<TOwner, TTarget, TEvent> : MMEventListener<TEvent>, IDisposable
        where TEvent : struct
    {
        private Action<TTarget> _callback;

        private TOwner _owner;
        public MMEventListenerWrapper(TOwner owner, Action<TTarget> callback)
        {
            _owner = owner;
            _callback = callback;
            RegisterCallbacks(true);
        }

        public void Dispose()
        {
            RegisterCallbacks(false);
            _callback = null;
        }

        protected virtual TTarget OnEvent(TEvent eventType) => default;
        public void OnMMEvent(TEvent eventType)
        {
            var item = OnEvent(eventType);
            _callback?.Invoke(item);
        }

        private void RegisterCallbacks(bool b)
        {
            if (b)
            {
                this.MMEventStartListening<TEvent>();
            }
            else
            {
                this.MMEventStopListening<TEvent>();
            }
        }
    }
}