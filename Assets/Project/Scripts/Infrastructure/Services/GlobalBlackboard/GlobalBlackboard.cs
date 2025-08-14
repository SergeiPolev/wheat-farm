using UnityEngine;

namespace Services
{
    public class GlobalBlackboard : IService
    {
        public Player Player => _player;
        
        private Player _player;

        public void Initialize()
        {
            _player = GameObject.FindAnyObjectByType<Player>();
        }
    }
}