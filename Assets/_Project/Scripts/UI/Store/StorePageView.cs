using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using BK.Localization;
using BK.UI;

namespace Project
{
    /// <summary>
    /// 상점 페이지. 테이블을 카테고리별로 묶어 카드를 생성하고, 진입 시 위에서부터 순서대로 등장시킵니다.
    /// 구매 성공 후에는 홈 탭으로 돌아가 보상 팝업을 띄웁니다(원본 로비 상점과 같은 흐름).
    /// </summary>
    public sealed class StorePageView : ViewComponent
    {
        [Serializable]
        private struct CategorySection
        {
            public ShopCategory Category;
            public GameObject Root;
            public Text Title;
            public Transform Container;
        }

        [SerializeField] private Text _storeTitle;
        [SerializeField] private CategorySection[] _sections;
        [SerializeField] private CoinProductCell _coinCellPrefab;
        [SerializeField] private BundleProductCell _bundleCellPrefab;
        [SerializeField] private float _appearStagger = 0.06f;

        private readonly List<ProductCellBase> _cells = new();
        private IObjectResolver _resolver;
        private IUIService _ui;
        private ILocalizationService _loc;
        private ShopService _shop;
        private LobbyState _lobby;
        private CancellationTokenSource _appearCts;
        private bool _hidden;
        private bool _purchasing;

        [Inject]
        public void Construct(IObjectResolver resolver, IUIService ui, ILocalizationService loc,
            ShopService shop, LobbyState lobby)
        {
            _resolver = resolver;
            _ui = ui;
            _loc = loc;
            _shop = shop;
            _lobby = lobby;
        }

        public void Bind(ProjectViewBase owner)
        {
            ClearSubscriptions();
            _loc.CurrentLanguage.Subscribe(_ => Rebuild()).AddTo(Disposables);
            _shop.Purchased
                .Where(p => p.PurchaseLimit > 0)
                .Subscribe(_ => Rebuild())
                .AddTo(Disposables);
        }

        /// <summary>카드 전부를 숨긴 상태로 둡니다. 다음 <see cref="PlayAppear"/> 가 등장 연출을 재생합니다.</summary>
        public void HideAppearItems()
        {
            CancelAppear();
            _hidden = true;
            foreach (var cell in _cells)
                cell.Hide();
        }

        public void PlayAppear()
        {
            CancelAppear();
            _hidden = false;
            _appearCts = new CancellationTokenSource();
            var token = _appearCts.Token;

            var order = 0;
            foreach (var section in _sections)
            {
                if (!section.Root.activeSelf)
                    continue;
                foreach (var cell in _cells.Where(c => c.transform.parent == section.Container))
                {
                    cell.Hide();
                    cell.AppearAsync(_appearStagger * order++, token).Forget();
                }
            }
        }

        private void Rebuild()
        {
            foreach (var cell in _cells)
                Destroy(cell.gameObject);
            _cells.Clear();

            _storeTitle.text = _loc.Get("store.title");

            var groups = _shop.Products.Rows
                .Where(_shop.IsAvailable)
                .GroupBy(p => p.Category)
                .OrderBy(g => g.Min(p => p.CategoryPriority))
                .ThenBy(g => (int)g.Key);

            foreach (var section in _sections)
                section.Root.SetActive(false);

            var rank = 0;
            foreach (var group in groups)
            {
                var index = Array.FindIndex(_sections, s => s.Category == group.Key);
                if (index < 0)
                    continue;

                var section = _sections[index];
                section.Root.SetActive(true);
                section.Root.transform.SetSiblingIndex(rank++);
                section.Title.text = _loc.Get(TitleKey(group.Key));

                foreach (var product in group.OrderBy(p => p.Priority))
                {
                    ProductCellBase cell = group.Key == ShopCategory.Coin
                        ? (ProductCellBase)_resolver.Instantiate(_coinCellPrefab, section.Container)
                        : _resolver.Instantiate(_bundleCellPrefab, section.Container);
                    cell.Bind(product, _loc.Get);
                    cell.BuyClicked += p => PurchaseAsync(p, destroyCancellationToken).Forget();
                    _cells.Add(cell);
                }
            }

            if (_hidden)
                HideAppearItems();
            else
                foreach (var cell in _cells)
                    cell.ShowImmediate();
        }

        private async UniTask PurchaseAsync(ShopProductRow product, CancellationToken cancellationToken)
        {
            if (_purchasing)
                return;
            _purchasing = true;
            try
            {
                if (!await _shop.PurchaseAsync(product, cancellationToken))
                    return;

                _lobby.CurrentTab.Value = LobbyTab.Home;
                await RewardPopup.ShowAsync(_ui, ShopService.RewardsOf(product).ToArray(), cancellationToken);
            }
            finally
            {
                _purchasing = false;
            }
        }

        private static string TitleKey(ShopCategory category) => category switch
        {
            ShopCategory.Coin => "store.category.coin",
            ShopCategory.Bundle => "store.category.bundle",
            _ => "store.category.special",
        };

        private void CancelAppear()
        {
            _appearCts?.Cancel();
            _appearCts?.Dispose();
            _appearCts = null;
        }

        protected override void OnDestroy()
        {
            CancelAppear();
            base.OnDestroy();
        }
    }
}
