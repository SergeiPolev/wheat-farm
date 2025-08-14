using Services;
using UnityEngine;

namespace Infrastructure
{
    public class PlayerMovementSystem : IService
    {
        protected InputService _input;
        protected GlobalBlackboard _blackboard;
        protected Player _player => _blackboard.Player;
        protected Matrix4x4 _rotationMatrix;

        public void OnEnter()
        {
            _input = AllServices.Container.Single<InputService>();
            _blackboard = AllServices.Container.Single<GlobalBlackboard>();
            
            _rotationMatrix = Matrix4x4.Rotate(Quaternion.Euler(0, 45, 0));
        }

        public void OnTick()
        {
            var direction = new Vector3(_input.Direction.x, 0, _input.Direction.y);
            _player.Move(_rotationMatrix * direction);
        }
    }
}