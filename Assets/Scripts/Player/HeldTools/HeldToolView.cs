using UnityEngine;

namespace WheatFarm.Player.HeldTools
{
    /// <summary>
    /// Shows one tool prop in the farmer's right hand. Visuals only: HeldToolPresenter decides which.
    /// Props are children of a socket under the RightHand bone, all inactive by default.
    /// </summary>
    public class HeldToolView : MonoBehaviour
    {
        [SerializeField] private GameObject _wateringCan;
        [SerializeField] private GameObject _seedBag;
        [SerializeField] private GameObject _paintBucket;
        [SerializeField] private GameObject _sickle;

        private HeldProp _shown = (HeldProp)(-1);

        public HeldProp Shown => _shown;

        public void Show(HeldProp prop)
        {
            if (prop == _shown) return;
            _shown = prop;
            Set(_wateringCan, prop == HeldProp.WateringCan);
            Set(_seedBag, prop == HeldProp.SeedBag);
            Set(_paintBucket, prop == HeldProp.PaintBucket);
            Set(_sickle, prop == HeldProp.Sickle);
        }

        private static void Set(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }
    }
}
