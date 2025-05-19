using System;
using System.Collections.Generic;

namespace Services
{
    public class WindowService : IService
    {
        private UIFactory _uiFactory;

        private Dictionary<WindowId, WindowBase> _windows;
        public event Action<WindowId> OnWindowShowed;


        public WindowService()
        {
            _windows = new Dictionary<WindowId, WindowBase>();
        }

        public void Initialize(UIFactory factory)
        {
            _uiFactory = factory;
            //_uiFactory.CreateMainCanvas();
        }

        public bool Open(WindowId windowID)
        {
            return Open<WindowBase>(windowID, out var window);
        }

        public bool Open<T>(WindowId windowId, out T window) where T : WindowBase
        {
            window = null;
            if (IsOpen(windowId))
                return false;

            window = GetOrCreateWindow<T>(windowId);
            window.Open();
            return true;
        }


        public T GetOrCreateWindow<T>(WindowId windowId) where T : WindowBase
        {
            if (_windows.ContainsKey(windowId) == false)
            {
                _windows.Add(windowId, _uiFactory.CreateWindow<T>(windowId));
            }
            return _windows[windowId] as T;
        }

        public void Close(WindowId windowId)
        {
            if (IsOpen(windowId) == false)
            {
                return;
            }

            if (_windows.TryGetValue(windowId, out WindowBase window))
            {
                window.Close();
            }
        }

        public bool IsOpen(WindowId windowId)
        {
            if (_windows.TryGetValue(windowId, out WindowBase window))
            {
                return window.IsOpen;
            }

            return false;
        }


    }
}

