using VContainer;
using VContainer.Unity;
using UnityEngine;
using WheatFarm.Infrastructure.Save;

namespace WheatFarm.Infrastructure
{
    /// <summary>
    /// Root DI scope — lives for entire application lifetime (DontDestroyOnLoad).
    /// Registers core services: save, config databases.
    /// </summary>
    public class RootScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            Debug.Log("[RootScope] Configure");

            // Phase 10: Save/Load
            builder.Register<FarmSaveService>(Lifetime.Singleton)
                .As<IFarmSaveService>();
        }
    }
}
