using UnityEngine;

namespace BK.Kit
{
    public sealed class GamePresentation : ScriptableObject
    {
        public GameObject lobby, home, hud, ingame, win, lose, settings, ingameSettings;
        public GameObject cannon, ball;
        public Sprite[] shopSprites;
        public GameObject[] blocks;
        public TextAsset levels;
        public AudioClip lobbyMusic, ingameMusic, startSound, shotSound;
        public Material arenaMaterial, trajectoryMaterial;
    }
}
