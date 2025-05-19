using Data;

namespace Services
{
    public interface ISaveProgress: ISavedProgressReader
    {
        void UpdateProgress(PlayerProgress progress);
    }
}

