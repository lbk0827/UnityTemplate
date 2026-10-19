using UnityEngine;

namespace BK.Kit
{
    public sealed class GamePresentation : ScriptableObject
    {
        public GameObject lobby, home, hud, ingame, win, lose, settings, ingameSettings;
        public GameObject welcomeDeal, endlessOffer, endlessGifts;
        public GameObject quit, retry, heart, unlockGimmick, unlockBooster, useBooster, celebration, reward, profile;
        public GameObject cannon, ball;
        public Sprite[] shopSprites;
        public Sprite[] refinedSprites;
        public TMPro.TMP_FontAsset bodyFont;
        [Tooltip("One-cell box prefabs by colour. Colour does not determine HP.")]
        public GameObject[] blocks;
        public TextAsset levels;
        public AudioClip lobbyMusic, ingameMusic, startSound, shotSound;
        public Material arenaMaterial, trajectoryMaterial;
        public IngameConfig ingameConfig;
        public GameObject[] brokenBlocks;
        public GameObject blockBreakParticle, ballHitParticle;
        public Material ballTrailMaterial;
        public AudioClip blockHitSound, blockBreakSound;
    }
}
