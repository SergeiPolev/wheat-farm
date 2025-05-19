namespace Services
{
    public class GameFactory : IService
    {
        private GlobalBlackboard _globalBlackboard;
        private StaticDataService _staticData;

        public void Initialize(GlobalBlackboard globalBlackboard, StaticDataService staticData)
        {
            _globalBlackboard = globalBlackboard;
            _staticData = staticData;
        }
    }
}