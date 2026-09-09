using System;
using R3;
using UnityEngine;

namespace Project
{
    /// <summary>사운드/진동/언어 설정. PlayerPrefs 저장.</summary>
    public sealed class GameOptions : IDisposable
    {
        public ReactiveProperty<bool> Music { get; }
        public ReactiveProperty<bool> Sfx { get; }
        public ReactiveProperty<bool> Haptic { get; }
        public ReactiveProperty<string> Language { get; }

        public GameOptions(string defaultLanguage)
        {
            Music = Load("options.music");
            Sfx = Load("options.sfx");
            Haptic = Load("options.haptic");
            Language = new ReactiveProperty<string>(PlayerPrefs.GetString("options.language", defaultLanguage));

            Music.Subscribe(v => PlayerPrefs.SetInt("options.music", v ? 1 : 0));
            Sfx.Subscribe(v => PlayerPrefs.SetInt("options.sfx", v ? 1 : 0));
            Haptic.Subscribe(v => PlayerPrefs.SetInt("options.haptic", v ? 1 : 0));
            Language.Subscribe(v => PlayerPrefs.SetString("options.language", v));
        }

        private static ReactiveProperty<bool> Load(string key)
            => new(PlayerPrefs.GetInt(key, 1) == 1);

        public void Dispose()
        {
            Music.Dispose();
            Sfx.Dispose();
            Haptic.Dispose();
            Language.Dispose();
        }
    }
}
