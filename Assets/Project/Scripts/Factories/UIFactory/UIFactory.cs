using UnityEngine;

namespace Services
{
    public class UIFactory : IService
    {
        private const string UIRootPath = "UI/UIRoot";
        private AllServices _services;
        private StaticDataService _staticData;
        private Transform _uiRoot;
        public Transform UIRoot => _uiRoot;

        public UIFactory()
        {
            CreateUIRoot();
        }

        public void Initialize(AllServices services)
        {
            _services = services;
            _staticData = _services.Single<StaticDataService>();
        }

        public T CreateWindow<T>(WindowId windowId) where T : WindowBase
        {
            var winPrefab = _staticData.ForWindow(windowId);
            var window = Object.Instantiate(winPrefab, _uiRoot) as T;
            window.Initialize(_services);
            window.gameObject.SetActive(false);
            return window;
        }
        
        private void CreateUIRoot()
        {
            _uiRoot = Object.Instantiate(Resources.Load<GameObject>(UIRootPath)).transform;

            if (_uiRoot.TryGetComponent(out Canvas canvas))
            {
                canvas.worldCamera = Camera.main;
            }
        }
    }
}