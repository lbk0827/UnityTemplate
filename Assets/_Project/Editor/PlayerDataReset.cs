using UnityEditor;
using UnityEngine;

namespace Project.Editor
{
    /// <summary>로컬 저장(지갑, 프로필, 출석, 옵션, 상점 구매 횟수)을 초기 상태로 되돌립니다.</summary>
    public static class PlayerDataReset
    {
        [MenuItem("BK/Setup/Reset Player Data")]
        public static void Run()
        {
            foreach (var key in new[]
            {
                "wallet.gold", "wallet.heart", "wallet.heart.next",
                "profile.nickname", "profile.avatar", "profile.frame", "profile.stage",
                "options.music", "options.sfx", "options.haptic", "options.language",
                "daily.next", "daily.lastDate", "daily.freeAt",
            })
                PlayerPrefs.DeleteKey(key);

            for (var id = 0; id < 100; id++)
                PlayerPrefs.DeleteKey($"shop.count.{id}");

            PlayerPrefs.Save();
            Debug.Log("[Setup] player data reset.");
        }
    }
}
