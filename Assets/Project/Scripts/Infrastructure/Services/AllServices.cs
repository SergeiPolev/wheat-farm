using System.Collections.Generic;

namespace Services
{
    public class AllServices
    {
        private static AllServices _instance;
        public static AllServices Container => _instance ??= new AllServices();
        public List<ISavedProgressReader> ProgressReaders { get; } = new List<ISavedProgressReader>();
        public List<ISaveProgress> ProgressWirters { get; } = new List<ISaveProgress>();
        public void RegisterSingle<TService>(TService implementation) where TService : IService
        {
            Implementation<TService>.ServiceInstance = implementation;

            if (Implementation<TService>.ServiceInstance is ISavedProgressReader progressReader)
            {
                Register(progressReader);
            }
        }

        public TService Single<TService>() where TService : IService
        {
            return Implementation<TService>.ServiceInstance;
        }

        private static class Implementation<TService> where TService : IService
        {
            public static TService ServiceInstance;
        }

        private void Register(ISavedProgressReader progressReader)
        {
            if (progressReader is ISaveProgress progressWriter)
            {
                ProgressWirters.Add(progressWriter);
            }

            ProgressReaders.Add(progressReader);
        }
    }
}