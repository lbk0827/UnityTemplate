using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using BK.Data;

namespace Project
{
    /// <summary>프로필 팝업. 닉네임 편집, 아바타/프레임 탭 전환과 선택.</summary>
    public sealed class ProfilePopup : PopupViewBase
    {
        public const string Address = "UI/Popups/Profile";

        [SerializeField] private Text _title;
        [SerializeField] private ProfileBadgeView _badge;
        [SerializeField] private InputField _nickname;
        [SerializeField] private Toggle _avatarTab;
        [SerializeField] private Text _avatarTabLabel;
        [SerializeField] private Toggle _frameTab;
        [SerializeField] private Text _frameTabLabel;
        [SerializeField] private Transform _cellRoot;
        [SerializeField] private ProfileItemCell _cellPrefab;

        private readonly List<ProfileItemCell> _cells = new();
        private PlayerProfile _profile;
        private ITableService _tables;

        [Inject]
        public void Construct(PlayerProfile profile, ITableService tables)
        {
            _profile = profile;
            _tables = tables;
        }

        public override UniTask OnInitializeAsync(CancellationToken cancellationToken)
        {
            _badge.Bind(_profile, _tables);

            _nickname.text = _profile.Nickname.CurrentValue;
            _nickname.onEndEdit.AddListener(value =>
            {
                if (!_profile.TrySetNickname(value))
                    _nickname.text = _profile.Nickname.CurrentValue;
            });

            _avatarTab.onValueChanged.AddListener(isOn => { if (isOn) BuildCells(ProfileItemKind.Avatar); });
            _frameTab.onValueChanged.AddListener(isOn => { if (isOn) BuildCells(ProfileItemKind.Frame); });

            _profile.AvatarId.Subscribe(_ => RefreshSelection()).AddTo(Disposables);
            _profile.FrameId.Subscribe(_ => RefreshSelection()).AddTo(Disposables);

            _avatarTab.SetIsOnWithoutNotify(true);
            _frameTab.SetIsOnWithoutNotify(false);
            BuildCells(ProfileItemKind.Avatar);
            return base.OnInitializeAsync(cancellationToken);
        }

        private void BuildCells(ProfileItemKind kind)
        {
            foreach (var cell in _cells)
                Destroy(cell.gameObject);
            _cells.Clear();

            foreach (var row in _tables.Get<int, ProfileItemRow>().Rows)
            {
                if (row.Kind != kind)
                    continue;
                var cell = Instantiate(_cellPrefab, _cellRoot);
                cell.Bind(row);
                cell.Clicked += OnCellClicked;
                _cells.Add(cell);
            }
            RefreshSelection();
        }

        private void OnCellClicked(ProfileItemRow row)
        {
            if (row.Kind == ProfileItemKind.Avatar)
                _profile.SetAvatar(row.Id);
            else
                _profile.SetFrame(row.Id);
        }

        private void RefreshSelection()
        {
            foreach (var cell in _cells)
            {
                var selectedId = cell.Row.Kind == ProfileItemKind.Avatar
                    ? _profile.AvatarId.CurrentValue
                    : _profile.FrameId.CurrentValue;
                cell.SetSelected(cell.Row.Id == selectedId);
            }
        }

        protected override void ApplyTexts()
        {
            _title.text = Loc.Get("popup.profile.title");
            _avatarTabLabel.text = Loc.Get("popup.profile.avatar");
            _frameTabLabel.text = Loc.Get("popup.profile.frame");
        }
    }
}
