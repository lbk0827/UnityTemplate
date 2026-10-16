using UnityEngine;

namespace BK.Kit
{
    [CreateAssetMenu(menuName = "BK/Bounce/Ingame Config", fileName = "IngameConfig")]
    public sealed class IngameConfig : ScriptableObject
    {
        [Min(.05f), Tooltip("Ball radius in world units. Drives both the collision cast and the ball visual (one block = 1 unit).")]
        public float ballRadius = .25f;
        public GimmickPrefabCatalog gimmickPrefabs;
    }
}
