using R3;
using UnityEngine;
using UnityEngine.UI;
using BK.Data;

namespace Project
{
    /// <summary>아바타 + 프레임 한 벌. HUD 와 프로필 팝업 상단이 같은 컴포넌트를 씁니다.</summary>
    public sealed class ProfileBadgeView : ViewComponent
    {
        [SerializeField] private Image _frame;
        [SerializeField] private Image _avatar;

        public void Bind(PlayerProfile profile, ITableService tables)
        {
            ClearSubscriptions();
            var items = tables.Get<int, ProfileItemRow>();

            profile.AvatarId.Subscribe(id => Apply(_avatar, items, id)).AddTo(Disposables);
            profile.FrameId.Subscribe(id => Apply(_frame, items, id)).AddTo(Disposables);
        }

        /// <summary>테이블과 무관하게 색을 직접 지정. 선택 셀에서 사용.</summary>
        public void Show(ProfileItemKind kind, Color color, Sprite sprite)
        {
            _avatar.gameObject.SetActive(kind == ProfileItemKind.Avatar);
            _frame.gameObject.SetActive(kind == ProfileItemKind.Frame);
            Apply(kind == ProfileItemKind.Avatar ? _avatar : _frame, color, sprite);
        }

        private static void Apply(Image image, ITable<int, ProfileItemRow> items, int id)
        {
            if (items.TryGet(id, out var row))
            {
                Apply(image, row.Color, row.Sprite);
                return;
            }

            Apply(image, Color.gray, null);
        }

        private static void Apply(Image image, Color color, Sprite sprite)
        {
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : color;
            image.preserveAspect = sprite != null;
        }
    }
}
