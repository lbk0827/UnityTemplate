using System;
using UnityEngine;

namespace BK.Kit
{
    [CreateAssetMenu(menuName = "BK/Bounce/Gimmick Prefabs")]
    public sealed class GimmickPrefabCatalog : ScriptableObject
    {
        public GimmickView tube, cupboard, panel, mine, switchButton, door, combo, armor;
        public GameObject tubeSegment, shard, blast, link;

        public GimmickView Find(string type) => type switch
        {
            "tube" => tube, "cupboard" => cupboard, "panel" => panel, "mine" => mine,
            "switch" => switchButton, "door" => door, "combo" => combo, "armor" => armor,
            _ => throw new ArgumentException("Unknown gimmick: " + type)
        };
    }
}
