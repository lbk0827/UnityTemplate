using System;
using UnityEngine;
using UnityEngine.UI;

namespace Project
{
    /// <summary>아바타/프레임 선택 셀. 선택 표시는 부모가 <see cref="SetSelected"/> 로 갱신합니다.</summary>
    public sealed class ProfileItemCell : MonoBehaviour
    {
        [SerializeField] private ProfileBadgeView _badge;
        [SerializeField] private GameObject _check;
        [SerializeField] private Button _button;

        public ProfileItemRow Row { get; private set; }
        public event Action<ProfileItemRow> Clicked;

        public void Bind(ProfileItemRow row)
        {
            Row = row;
            _badge.Show(row.Kind, row.Color, row.Sprite);
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => Clicked?.Invoke(Row));
        }

        public void SetSelected(bool selected) => _check.SetActive(selected);
    }
}
