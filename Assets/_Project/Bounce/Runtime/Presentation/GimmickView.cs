using UnityEngine;

namespace BK.Kit
{
    // Explicit prefab bindings. Artists can replace meshes/materials without changing game rules.
    public sealed class GimmickView : MonoBehaviour
    {
        public BoxCollider solid;
        public GameObject[] pieces;
        public Transform furnace, movingBody, rail;
        public Transform[] railStops;
        public Renderer[] furnaceGlow;
        public GameObject activeVisual, inactiveVisual;

        public void ShowActive(bool active)
        {
            if (activeVisual != null) activeVisual.SetActive(active);
            if (inactiveVisual != null) inactiveVisual.SetActive(!active);
        }
    }
}
