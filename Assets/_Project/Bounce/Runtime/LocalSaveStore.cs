using System;
using System.IO;
using UnityEngine;

namespace BK.Kit
{
    [Serializable]
    public sealed class PlayerProgress
    {
        public int version = 2;
        public int unlockedLevel = 1;
        public int gold;
        public string playerName = "BK Player";
        public int missiles, extraBalls, bombs, lasers;
        public int Count(BoosterKind kind)
        {
            switch(kind) {case BoosterKind.Missile:return missiles;case BoosterKind.ExtraBall:return extraBalls;case BoosterKind.Bomb:return bombs;case BoosterKind.Laser:return lasers;default:return 0;}
        }
        public void SetCount(BoosterKind kind,int count)
        {
            switch(kind) {case BoosterKind.Missile:missiles=count;break;case BoosterKind.ExtraBall:extraBalls=count;break;case BoosterKind.Bomb:bombs=count;break;case BoosterKind.Laser:lasers=count;break;default:throw new ArgumentOutOfRangeException(nameof(kind));}
        }
        public bool soundEnabled = true;
        public bool musicEnabled = true;
        public bool effectsEnabled = true;
        public bool hapticsEnabled = true;
    }

    // This store owns only this template's progress.json, never another game's save.
    public sealed class LocalSaveStore
    {
        private readonly string path;
        public LocalSaveStore(string directory) => path = Path.Combine(directory, "progress.json");

        public PlayerProgress Load()
        {
            if (!File.Exists(path)) return new PlayerProgress();
            try { return Read(path); }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is InvalidDataException)
            {
                Debug.LogWarning("BK_Kit: invalid save; trying backup. " + error.Message);
                try { if (File.Exists(path + ".bak")) return Read(path + ".bak"); }
                catch (Exception backupError) when (backupError is IOException || backupError is ArgumentException || backupError is InvalidDataException) { }
                return new PlayerProgress();
            }
        }

        private static PlayerProgress Read(string file)
        {
            var data = JsonUtility.FromJson<PlayerProgress>(File.ReadAllText(file));
            if (data == null || data.version < 1 || data.version > 2 || data.unlockedLevel < 1 || data.gold < 0)
                throw new InvalidDataException("Unsupported or invalid progress data.");
            if(data.missiles<0 || data.extraBalls<0 || data.bombs<0 || data.lasers<0)
                throw new InvalidDataException("Invalid booster inventory.");
            if(data.version==1)
            {
                data.musicEnabled=data.effectsEnabled=data.soundEnabled;
                data.soundEnabled=true;data.hapticsEnabled=true;data.version=2;
            }
            if(string.IsNullOrWhiteSpace(data.playerName))data.playerName="BK Player";
            if(data.playerName.Length>24)data.playerName=data.playerName.Substring(0,24);
            return data;
        }

        public void Save(PlayerProgress data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(data, true));
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
    }
}
