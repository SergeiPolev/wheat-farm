using UnityEngine;

namespace Services
{
    public class InputService : IService
    {
        private WindowService _windowService;
        protected bool _isActive;
        protected const string Horizontal = "Horizontal";
        protected const string Vertical = "Vertical";
        public Vector2 Direction
        {
            get
            {
                Vector2 axis;

                if (_isActive)
                {
                    axis = MovementInputAxis();
                }
                else
                {
                    axis = Vector2.zero;
                }

                return axis;
            }
        } 

        public void Initialize(WindowService windowService)
        {
            _windowService = windowService;
        }

        protected Vector2 MovementInputAxis()
        {
            return new Vector2(SimpleInput.GetAxis(Horizontal), SimpleInput.GetAxis(Vertical));
        }

        public void SetActive(bool isActive)
        {
            _isActive = isActive;

            if (_isActive)
            {
                _windowService.Open(WindowId.Joystick);
            }
            else
            {
                _windowService.Close(WindowId.Joystick);
            }
        }
    }
}
