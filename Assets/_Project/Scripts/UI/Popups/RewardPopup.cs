using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using BK.UI;

namespace Project
{
    /// <summary>
    /// 보상 팝업. 아이템을 나열하고 아무 곳이나 탭하면 닫힙니다.
    /// 지급은 호출자가 이미 끝낸 상태여야 합니다. 이 팝업은 보여주기만 합니다.
    /// </summary>
    public sealed class RewardPopup : PopupViewBase, IUIView<RewardItem[]>
    {
        public const string Address = "UI/Popups/Reward";

        [SerializeField] private Text _title;
        [SerializeField] private Text _tapHint;
        [SerializeField] private Transform _itemRoot;
        [SerializeField] private GoodsItemView _itemPrefab;
        [SerializeField] private Button _tapAnywhere;

        private RewardItem[] _items;
        private readonly UniTaskCompletionSource _closed = new();

        public void SetArgs(RewardItem[] args) => _items = args;

        /// <summary>열고 닫힐 때까지 기다립니다.</summary>
        public static async UniTask ShowAsync(IUIService ui, RewardItem[] items, CancellationToken cancellationToken)
        {
            if (items == null || items.Length == 0)
                return;
            var popup = await ui.OpenAsync<RewardPopup, RewardItem[]>(Address, items, cancellationToken);
            await popup._closed.Task;
        }

        public override UniTask OnInitializeAsync(CancellationToken cancellationToken)
        {
            foreach (var item in _items)
                Instantiate(_itemPrefab, _itemRoot).Bind(item, "+{0}");

            _tapAnywhere.onClick.AddListener(RequestClose);
            return base.OnInitializeAsync(cancellationToken);
        }

        protected override void ApplyTexts()
        {
            _title.text = Loc.Get("popup.reward.title");
            _tapHint.text = Loc.Get("popup.reward.tap");
        }

        protected override async UniTask PlayCloseAsync(CancellationToken cancellationToken)
        {
            await base.PlayCloseAsync(cancellationToken);
            _closed.TrySetResult();
        }

        protected override void OnDestroy()
        {
            // 씬 전환 등으로 연출 없이 파괴돼도 기다리는 쪽이 풀리게 합니다.
            _closed.TrySetResult();
            base.OnDestroy();
        }
    }
}
