using LitMotion;
using LitMotion.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace Project
{
    /// <summary>
    /// 하단 탭 버튼. Toggle 위에 선택/해제 스케일 연출을 얹습니다.
    /// 탭 전환 자체는 HUD 가 LobbyState 로 처리하고, 여기서는 보이는 것만 담당합니다.
    /// </summary>
    public sealed class LobbyTabButton : MonoBehaviour
    {
        [SerializeField] private LobbyTab _tab;
        [SerializeField] private Toggle _toggle;
        [SerializeField] private RectTransform _icon;
        [SerializeField] private Text _label;
        [SerializeField] private float _selectedScale = 1.15f;

        private MotionHandle _motion;

        public LobbyTab Tab => _tab;
        public Toggle Toggle => _toggle;

        private void OnEnable()
        {
            _toggle.onValueChanged.AddListener(OnValueChanged);
            ApplyImmediate(_toggle.isOn);
        }

        private void OnDisable()
        {
            _toggle.onValueChanged.RemoveListener(OnValueChanged);
            if (_motion.IsActive())
                _motion.Cancel();
        }

        public void SetLabel(string text) => _label.text = text;

        private void OnValueChanged(bool isOn)
        {
            if (_motion.IsActive())
                _motion.Cancel();

            var target = Vector3.one * (isOn ? _selectedScale : 1f);
            _motion = LMotion.Create(_icon.localScale, target, 0.18f)
                .WithEase(isOn ? Ease.OutBack : Ease.OutQuad)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .BindToLocalScale(_icon);
        }

        private void ApplyImmediate(bool isOn)
            => _icon.localScale = Vector3.one * (isOn ? _selectedScale : 1f);
    }
}
