using System;
using R3;
using UnityEngine;

namespace Project
{
    /// <summary>닉네임, 아바타, 프레임, 현재 스테이지. PlayerPrefs 저장.</summary>
    public sealed class PlayerProfile : IDisposable
    {
        private readonly ReactiveProperty<string> _nickname;
        private readonly ReactiveProperty<int> _avatarId;
        private readonly ReactiveProperty<int> _frameId;
        private readonly ReactiveProperty<int> _currentStage;

        public PlayerProfile()
        {
            _nickname = new ReactiveProperty<string>(PlayerPrefs.GetString("profile.nickname", "Player"));
            _avatarId = new ReactiveProperty<int>(PlayerPrefs.GetInt("profile.avatar", 1));
            _frameId = new ReactiveProperty<int>(PlayerPrefs.GetInt("profile.frame", 101));
            _currentStage = new ReactiveProperty<int>(PlayerPrefs.GetInt("profile.stage", 1));
        }

        public ReadOnlyReactiveProperty<string> Nickname => _nickname;
        public ReadOnlyReactiveProperty<int> AvatarId => _avatarId;
        public ReadOnlyReactiveProperty<int> FrameId => _frameId;
        public ReadOnlyReactiveProperty<int> CurrentStage => _currentStage;

        /// <summary>공백 제거 후 비어 있거나 내부 공백이 있으면 거부합니다.</summary>
        public bool TrySetNickname(string value)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.Contains(' '))
                return false;

            _nickname.Value = trimmed;
            PlayerPrefs.SetString("profile.nickname", trimmed);
            return true;
        }

        public void SetAvatar(int id)
        {
            _avatarId.Value = id;
            PlayerPrefs.SetInt("profile.avatar", id);
        }

        public void SetFrame(int id)
        {
            _frameId.Value = id;
            PlayerPrefs.SetInt("profile.frame", id);
        }

        public void AdvanceStage()
        {
            _currentStage.Value += 1;
            PlayerPrefs.SetInt("profile.stage", _currentStage.Value);
        }

        public void Dispose()
        {
            _nickname.Dispose();
            _avatarId.Dispose();
            _frameId.Dispose();
            _currentStage.Dispose();
        }
    }
}
