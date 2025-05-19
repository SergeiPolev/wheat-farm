using System;
using Services;

namespace Infrastructure
{
    public class DebugService : IService
    {
        public event Action RestartLevel;
        public event Action OnHub;

        public void ReturnToHub()
        {
            OnHub?.Invoke();
        }


        public void OnRestartLevel()
        {
            RestartLevel?.Invoke();
        }
    }
}