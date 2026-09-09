using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using BK.Data;
using BK.Localization;
using BK.UI;
using static Project.Editor.UIBuilder;

namespace Project.Editor
{
    /// <summary>
    /// 로비/HUD/상점/팝업 프리팹과 테이블, 로컬라이제이션 키를 코드로 생성합니다.
    /// 기본 실행은 없는 것만 만들고, "Rebuild" 메뉴는 프리팹을 새로 덮어씁니다.
    /// 아트는 전부 플레이스홀더라 실제 프로젝트에서는 프리팹을 열어 교체하면 됩니다.
    /// </summary>
    public static class LobbySetup
    {
        private const string Root = "Assets/_Project";
        private const string LobbyDir = Root + "/UI/Lobby";
        private const string PopupDir = Root + "/UI/Popups";
        private const string CellDir = Root + "/UI/Cells";

        private const string HudPath = LobbyDir + "/HudView.prefab";
        private const string LobbyPanelPath = LobbyDir + "/LobbyPanelView.prefab";
        private const string GoodsItemPath = CellDir + "/GoodsItem.prefab";
        private const string CoinCellPath = CellDir + "/CoinProductCell.prefab";
        private const string BundleCellPath = CellDir + "/BundleProductCell.prefab";
        private const string ProfileCellPath = CellDir + "/ProfileItemCell.prefab";

        private const string ShopTablePath = Root + "/Data/Tables/ShopProductTable.asset";
        private const string DailyTablePath = Root + "/Data/Tables/DailyRewardTable.asset";
        private const string ProfileTablePath = Root + "/Data/Tables/ProfileItemTable.asset";
        private const string CatalogPath = Root + "/Data/TableCatalog.asset";
        private const string LocEnPath = Root + "/Localization/en.asset";
        private const string LocKoPath = Root + "/Localization/ko.asset";

        private static bool _overwrite;

        [MenuItem("BK/Setup/Create Lobby UI")]
        public static void Run() => Run(false);

        [MenuItem("BK/Setup/Rebuild Lobby UI (overwrite prefabs)")]
        public static void Rebuild() => Run(true);

        private static void Run(bool overwrite)
        {
            _overwrite = overwrite;
            EnsureFolders();

            CreateTables();
            CreateLocalization();

            // 셀 프리팹을 먼저 만들어야 뷰 프리팹이 참조할 수 있습니다.
            var goodsItem = Build(GoodsItemPath, BuildGoodsItem);
            var coinCell = Build(CoinCellPath, BuildCoinCell);
            var bundleCell = Build(BundleCellPath, BuildBundleCell);
            var profileCell = Build(ProfileCellPath, BuildProfileCell);

            var popups = new List<(string path, string address)>
            {
                (Build(HudPath, BuildHud), HudView.Address),
                (Build(LobbyPanelPath, () => BuildLobbyPanel(coinCell, bundleCell)), LobbyPanelView.Address),
                (Build(PopupDir + "/MessagePopup.prefab", BuildMessagePopup), MessagePopup.Address),
                (Build(PopupDir + "/ToastView.prefab", BuildToast), ToastView.Address),
                (Build(PopupDir + "/RewardPopup.prefab", () => BuildRewardPopup(goodsItem)), RewardPopup.Address),
                (Build(PopupDir + "/OptionPopup.prefab", BuildOptionPopup), OptionPopup.Address),
                (Build(PopupDir + "/LanguagePopup.prefab", BuildLanguagePopup), LanguagePopup.Address),
                (Build(PopupDir + "/ProfilePopup.prefab", () => BuildProfilePopup(profileCell)), ProfilePopup.Address),
                (Build(PopupDir + "/DailyRewardsPopup.prefab", BuildDailyRewardsPopup), DailyRewardsPopup.Address),
                (Build(PopupDir + "/RefillPopup.prefab", BuildRefillPopup), RefillPopup.Address),
                (ShopTablePath, "Data/ShopProductTable"),
                (DailyTablePath, "Data/DailyRewardTable"),
                (ProfileTablePath, "Data/ProfileItemTable"),
                (LocKoPath, "Localization/ko"),
            };

            SampleProjectSetup.RegisterAddressables(popups.ToArray());
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] lobby UI ready.");
        }

        private static void EnsureFolders()
        {
            foreach (var dir in new[] { "UI", "UI/Lobby", "UI/Popups", "UI/Cells", "Data", "Data/Tables", "Localization" })
            {
                var full = Root + "/" + dir;
                if (AssetDatabase.IsValidFolder(full))
                    continue;
                var slash = full.LastIndexOf('/');
                AssetDatabase.CreateFolder(full.Substring(0, slash), full.Substring(slash + 1));
            }
        }

        /// <summary>프리팹이 없거나 덮어쓰기 모드면 build 로 만들어 저장하고, 경로를 돌려줍니다.</summary>
        private static string Build(string path, System.Func<GameObject> build)
        {
            if (!_overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                return path;
            SavePrefab(build(), path);
            return path;
        }

        private static T Prefab<T>(string path) where T : Component
            => AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<T>();

        // ── 테이블 ──────────────────────────────────────────────────────────────

        private static void CreateTables()
        {
            CreateTable<ShopProductTable, int, ShopProductRow>(ShopTablePath, new List<ShopProductRow>
            {
                new(21, ShopCategory.SpecialOffer, 0, 1, ProductLabel.Discount, 80, "product.welcome.name", "product.welcome.desc", 0.99f, 1,
                    new RewardItem(CurrencyId.Gold, 5000), new[] { new RewardItem(CurrencyId.Heart, 5) }),

                new(11, ShopCategory.Bundle, 1, 1, ProductLabel.Popular, 0, "product.starter.name", "product.starter.desc", 2.99f, 0,
                    new RewardItem(CurrencyId.Gold, 3000), new[] { new RewardItem(CurrencyId.Heart, 3) }),
                new(12, ShopCategory.Bundle, 1, 2, ProductLabel.Discount, 30, "product.value.name", "product.value.desc", 9.99f, 0,
                    new RewardItem(CurrencyId.Gold, 12000), new[] { new RewardItem(CurrencyId.Heart, 5) }),

                new(1, ShopCategory.Coin, 2, 1, ProductLabel.None, 0, "product.coin.name", "", 0.99f, 0, new RewardItem(CurrencyId.Gold, 1000), null),
                new(2, ShopCategory.Coin, 2, 2, ProductLabel.Popular, 0, "product.coin.name", "", 4.99f, 0, new RewardItem(CurrencyId.Gold, 5500), null),
                new(3, ShopCategory.Coin, 2, 3, ProductLabel.Best, 0, "product.coin.name", "", 9.99f, 0, new RewardItem(CurrencyId.Gold, 12000), null),
                new(4, ShopCategory.Coin, 2, 4, ProductLabel.None, 0, "product.coin.name", "", 19.99f, 0, new RewardItem(CurrencyId.Gold, 30000), null),
            });

            var free = new RewardItem(CurrencyId.Gold, 100);
            CreateTable<DailyRewardTable, int, DailyRewardRow>(DailyTablePath, new List<DailyRewardRow>
            {
                new(1, new[] { new RewardItem(CurrencyId.Gold, 200) }, free),
                new(2, new[] { new RewardItem(CurrencyId.Gold, 300) }, free),
                new(3, new[] { new RewardItem(CurrencyId.Heart, 1), new RewardItem(CurrencyId.Gold, 300) }, free),
                new(4, new[] { new RewardItem(CurrencyId.Gold, 500) }, free),
                new(5, new[] { new RewardItem(CurrencyId.Heart, 2) }, free),
                new(6, new[] { new RewardItem(CurrencyId.Gold, 800) }, free),
                new(7, new[] { new RewardItem(CurrencyId.Gold, 1500), new RewardItem(CurrencyId.Heart, 3) }, free),
            });

            CreateTable<ProfileItemTable, int, ProfileItemRow>(ProfileTablePath, new List<ProfileItemRow>
            {
                new(1, ProfileItemKind.Avatar, new Color(0.95f, 0.55f, 0.35f), Sprite("IMG_Avatar_0")),
                new(2, ProfileItemKind.Avatar, new Color(0.4f, 0.75f, 0.95f), Sprite("IMG_Avatar_1")),
                new(3, ProfileItemKind.Avatar, new Color(0.55f, 0.85f, 0.45f), Sprite("IMG_Avatar_2")),
                new(4, ProfileItemKind.Avatar, new Color(0.85f, 0.5f, 0.85f), Sprite("IMG_Avatar_3")),
                new(5, ProfileItemKind.Avatar, new Color(0.95f, 0.85f, 0.35f), Sprite("IMG_Avatar_4")),
                new(6, ProfileItemKind.Avatar, new Color(0.6f, 0.6f, 0.65f), Sprite("IMG_Avatar_5")),
                new(101, ProfileItemKind.Frame, new Color(0.9f, 0.9f, 0.9f), Sprite("IMG_ProfileFrame_0")),
                new(102, ProfileItemKind.Frame, new Color(0.95f, 0.8f, 0.3f), Sprite("IMG_ProfileFrame_1")),
                new(103, ProfileItemKind.Frame, new Color(0.4f, 0.8f, 0.9f), Sprite("IMG_ProfileFrame_2")),
                new(104, ProfileItemKind.Frame, new Color(0.9f, 0.4f, 0.4f), Sprite("IMG_ProfileFrame_3")),
            });

            var catalog = AssetDatabase.LoadAssetAtPath<TableCatalog>(CatalogPath);
            if (catalog != null)
            {
                var addresses = (List<string>)GetField(catalog, typeof(TableCatalog), "_addresses");
                foreach (var address in new[] { "Data/ShopProductTable", "Data/DailyRewardTable", "Data/ProfileItemTable" })
                    if (!addresses.Contains(address))
                        addresses.Add(address);
                EditorUtility.SetDirty(catalog);
            }
        }

        private static void CreateTable<TAsset, TKey, TRow>(string path, List<TRow> rows)
            where TAsset : TableAsset<TKey, TRow>
            where TRow : ITableRow<TKey>
        {
            var existing = AssetDatabase.LoadAssetAtPath<TAsset>(path);
            if (existing != null && !_overwrite)
                return;
            if (existing != null)
            {
                SetField(existing, typeof(TableAsset<TKey, TRow>), "_rows", rows);
                EditorUtility.SetDirty(existing);
                return;
            }

            var asset = ScriptableObject.CreateInstance<TAsset>();
            SetField(asset, typeof(TableAsset<TKey, TRow>), "_rows", rows);
            AssetDatabase.CreateAsset(asset, path);
        }

        private static object GetField(object target, System.Type declaring, string name)
            => declaring.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

        private static void SetField(object target, System.Type declaring, string name, object value)
            => declaring.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        // ── 로컬라이제이션 ──────────────────────────────────────────────────────

        private static void CreateLocalization()
        {
            MergeLocalization(LocEnPath, "en", new Dictionary<string, string>
            {
                ["hud.tab.home"] = "Home", ["hud.tab.store"] = "Shop", ["hud.full"] = "FULL",
                ["lobby.level"] = "Level {0}", ["lobby.daily"] = "Daily",
                ["store.title"] = "Shop", ["store.category.coin"] = "Coins", ["store.category.bundle"] = "Bundles",
                ["store.category.special"] = "Special Offer", ["store.label.popular"] = "Popular", ["store.label.best"] = "Best",
                ["product.coin.name"] = "Coins", ["product.welcome.name"] = "Welcome Deal", ["product.welcome.desc"] = "One-time offer for new players",
                ["product.starter.name"] = "Starter Pack", ["product.starter.desc"] = "Coins and hearts to get going",
                ["product.value.name"] = "Value Pack", ["product.value.desc"] = "The best value per coin",
                ["popup.option.title"] = "Settings", ["option.music"] = "Music", ["option.sfx"] = "Sound",
                ["option.haptic"] = "Vibration", ["option.language"] = "Language", ["option.privacy"] = "Privacy Policy",
                ["popup.language.title"] = "Language", ["lang.en"] = "English", ["lang.ko"] = "한국어",
                ["popup.profile.title"] = "Profile", ["popup.profile.avatar"] = "Avatar", ["popup.profile.frame"] = "Frame",
                ["popup.daily.title"] = "Daily Rewards", ["popup.daily.day"] = "Day {0}", ["popup.daily.claim"] = "Claim",
                ["popup.daily.free"] = "Free Coin", ["popup.daily.next"] = "Next in {0}",
                ["popup.refill.title"] = "Refill Hearts", ["popup.refill.desc"] = "Refill all hearts with gold.", ["popup.refill.buy"] = "Refill",
                ["popup.reward.title"] = "Reward", ["popup.reward.tap"] = "Tap to continue",
                ["common.ok"] = "OK", ["common.cancel"] = "Cancel",
                ["msg.stageclear.title"] = "Stage Clear", ["msg.stageclear.body"] = "Stage {0} cleared! (placeholder)",
            });

            MergeLocalization(LocKoPath, "ko", new Dictionary<string, string>
            {
                ["lobby.title"] = "로비", ["lobby.body"] = "{0}: {1}", ["sample.item.1"] = "샘플 아이템",
                ["hud.tab.home"] = "홈", ["hud.tab.store"] = "상점", ["hud.full"] = "가득",
                ["lobby.level"] = "레벨 {0}", ["lobby.daily"] = "출석",
                ["store.title"] = "상점", ["store.category.coin"] = "코인", ["store.category.bundle"] = "패키지",
                ["store.category.special"] = "특별 상품", ["store.label.popular"] = "인기", ["store.label.best"] = "최고",
                ["product.coin.name"] = "코인", ["product.welcome.name"] = "웰컴 딜", ["product.welcome.desc"] = "신규 유저 1회 한정",
                ["product.starter.name"] = "스타터 팩", ["product.starter.desc"] = "시작을 위한 코인과 하트",
                ["product.value.name"] = "밸류 팩", ["product.value.desc"] = "코인당 최고 효율",
                ["popup.option.title"] = "설정", ["option.music"] = "음악", ["option.sfx"] = "효과음",
                ["option.haptic"] = "진동", ["option.language"] = "언어", ["option.privacy"] = "개인정보 처리방침",
                ["popup.language.title"] = "언어", ["lang.en"] = "English", ["lang.ko"] = "한국어",
                ["popup.profile.title"] = "프로필", ["popup.profile.avatar"] = "아바타", ["popup.profile.frame"] = "프레임",
                ["popup.daily.title"] = "출석 보상", ["popup.daily.day"] = "{0}일차", ["popup.daily.claim"] = "받기",
                ["popup.daily.free"] = "무료 코인", ["popup.daily.next"] = "{0} 후",
                ["popup.refill.title"] = "하트 충전", ["popup.refill.desc"] = "골드로 하트를 모두 채웁니다.", ["popup.refill.buy"] = "충전",
                ["popup.reward.title"] = "보상", ["popup.reward.tap"] = "탭하여 계속",
                ["common.ok"] = "확인", ["common.cancel"] = "취소",
                ["msg.stageclear.title"] = "스테이지 클리어", ["msg.stageclear.body"] = "스테이지 {0} 클리어! (임시)",
            });
        }

        /// <summary>테이블이 없으면 만들고, 있으면 없는 키만 추가합니다. 기존 값은 건드리지 않습니다.</summary>
        private static void MergeLocalization(string path, string code, Dictionary<string, string> pairs)
        {
            var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(path);
            var created = table == null;
            if (created)
            {
                table = ScriptableObject.CreateInstance<LocalizationTable>();
                SetField(table, typeof(LocalizationTable), "_languageCode", code);
            }

            var entries = (List<LocalizationTable.Entry>)GetField(table, typeof(LocalizationTable), "_entries");
            var existing = new HashSet<string>();
            foreach (var entry in entries)
                existing.Add(entry.Key);

            foreach (var pair in pairs)
                if (existing.Add(pair.Key))
                    entries.Add(new LocalizationTable.Entry { Key = pair.Key, Value = pair.Value });

            if (created)
                AssetDatabase.CreateAsset(table, path);
            else
                EditorUtility.SetDirty(table);
        }

        // ── 셀 프리팹 ───────────────────────────────────────────────────────────

        private static GameObject BuildGoodsItem()
            => GoodsItem(null, "GoodsItem", new Vector2(120f, 140f)).gameObject;

        private static GameObject BuildProfileCell()
        {
            var bg = MakeImage(null, "ProfileItemCell", Color.white, Sprite("IMG_AvatarBack") ?? RoundedSprite);
            bg.rectTransform.sizeDelta = new Vector2(160f, 160f);
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            var cell = bg.gameObject.AddComponent<ProfileItemCell>();

            var badge = ProfileBadge(bg.transform, "Badge", 120f);
            Place((RectTransform)badge.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));

            var check = MakeImage(bg.transform, "Check", Color.white, Sprite("IMG_Profile_Check") ?? CheckSprite);
            check.type = UnityEngine.UI.Image.Type.Simple;
            Place(check.rectTransform, new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(40f, 40f));
            check.gameObject.SetActive(false);

            Wire(cell, so =>
            {
                so.FindProperty("_badge").objectReferenceValue = badge;
                so.FindProperty("_check").objectReferenceValue = check.gameObject;
                so.FindProperty("_button").objectReferenceValue = button;
            });
            return bg.gameObject;
        }

        /// <summary>카드 공통 골격: 레이아웃이 잡는 루트 + 연출 대상 Body. Body 가 CanvasGroup/배경을 가집니다.</summary>
        private static RectTransform CardBody(RectTransform root, out CanvasGroup group)
        {
            var body = MakeRect(root, "Body", typeof(CanvasGroup), typeof(Image));
            Stretch(body);
            var image = body.GetComponent<Image>();
            image.sprite = Sprite("IMG_Coin_Goods_Bg") ?? Sprite("IMG_ContentsBox") ?? RoundedSprite;
            image.type = image.sprite != null && image.sprite.border.sqrMagnitude > 0f ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.color = Color.white;
            group = body.GetComponent<CanvasGroup>();
            return body;
        }

        private static void WireCard(ProductCellBase cell, CanvasGroup group, RectTransform body, Button buy, Text price)
        {
            Wire(cell, so =>
            {
                so.FindProperty("_canvasGroup").objectReferenceValue = group;
                so.FindProperty("_root").objectReferenceValue = body;
                so.FindProperty("_buyButton").objectReferenceValue = buy;
                so.FindProperty("_priceText").objectReferenceValue = price;
            });
        }

        private static GameObject BuildCoinCell()
        {
            var root = MakeRect(null, "CoinProductCell", typeof(CoinProductCell));
            root.sizeDelta = new Vector2(330f, 360f);
            var body = CardBody(root, out var group);

            var item = GoodsItem(body, "MainItem", new Vector2(220f, 200f), 30);
            Place((RectTransform)item.transform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(220f, 200f));

            var buy = MakeButton(body, "BuyButton", "$0.00", Positive, 30, out var price);
            Place((RectTransform)buy.transform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(270f, 76f));

            var cell = root.GetComponent<CoinProductCell>();
            WireCard(cell, group, body, buy, price);
            Wire(cell, so => so.FindProperty("_mainItem").objectReferenceValue = item);
            return root.gameObject;
        }

        private static GameObject BuildBundleCell()
        {
            var root = MakeRect(null, "BundleProductCell", typeof(BundleProductCell));
            Layout(root, height: 300f);
            var body = CardBody(root, out var group);

            var main = GoodsItem(body, "MainItem", new Vector2(200f, 200f), 30);
            Place((RectTransform)main.transform, new Vector2(0f, 0.5f), new Vector2(30f, 10f), new Vector2(200f, 200f));

            var name = MakeText(body, "Name", "Bundle", 40, TextAnchor.MiddleLeft);
            Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(260f, -24f), new Vector2(560f, 50f));
            var desc = MakeText(body, "Desc", "Description", 26, TextAnchor.MiddleLeft, new Color(0.8f, 0.8f, 0.85f));
            Place(desc.rectTransform, new Vector2(0f, 1f), new Vector2(260f, -80f), new Vector2(560f, 40f));

            var extrasRoot = MakeRect(body, "Extras");
            Place(extrasRoot, new Vector2(0f, 1f), new Vector2(260f, -130f), new Vector2(400f, 120f));
            HorizontalLayout(extrasRoot, 16f, TextAnchor.MiddleLeft);
            var extras = new GoodsItemView[3];
            for (var i = 0; i < extras.Length; i++)
            {
                extras[i] = GoodsItem(extrasRoot, $"Extra{i}", new Vector2(100f, 116f), 24);
                Layout((RectTransform)extras[i].transform, height: 116f, width: 100f);
            }

            var labelRoot = MakeImage(body, "Label", Color.white, Sprite("IMG_DiscountBg") ?? RoundedSprite);
            Place(labelRoot.rectTransform, new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(170f, 48f));
            var labelText = MakeText(labelRoot.transform, "Text", "LABEL", 26);
            Stretch(labelText.rectTransform);

            var buy = MakeButton(body, "BuyButton", "$0.00", Positive, 30, out var price);
            Place((RectTransform)buy.transform, new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(240f, 76f));

            var cell = root.GetComponent<BundleProductCell>();
            WireCard(cell, group, body, buy, price);
            Wire(cell, so =>
            {
                so.FindProperty("_nameText").objectReferenceValue = name;
                so.FindProperty("_descText").objectReferenceValue = desc;
                so.FindProperty("_mainItem").objectReferenceValue = main;
                SetArray(so.FindProperty("_extraSlots"), extras);
                so.FindProperty("_labelRoot").objectReferenceValue = labelRoot.gameObject;
                so.FindProperty("_labelText").objectReferenceValue = labelText;
            });
            return root.gameObject;
        }

        // ── HUD ─────────────────────────────────────────────────────────────────

        private static GameObject ViewRoot(string name, System.Type viewType, UILayer layer)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), viewType);
            Stretch((RectTransform)root.transform);
            Wire(root.GetComponent(viewType), so => so.FindProperty("_layer").intValue = (int)layer);
            return root;
        }

        private static CurrencyHudView Currency(Transform parent, string id, bool recharge)
        {
            var bg = MakeImage(parent, $"Currency_{id}", Color.white, Sprite("IMG_GoodsBox") ?? RoundedSprite);
            bg.rectTransform.sizeDelta = new Vector2(260f, recharge ? 110f : 90f);
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            var view = bg.gameObject.AddComponent<CurrencyHudView>();

            var icon = MakeImage(bg.transform, "Icon", Color.white, id == CurrencyId.Heart ? HeartSprite : CoinSprite);
            icon.type = UnityEngine.UI.Image.Type.Simple;
            icon.preserveAspect = true;
            Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(64f, 64f));
            var initial = MakeText(icon.transform, "Initial", "G", 30, TextAnchor.MiddleCenter, new Color(0.1f, 0.1f, 0.1f));
            Stretch(initial.rectTransform);

            var count = MakeText(bg.transform, "Count", "0", 32, TextAnchor.MiddleRight);
            Text timer = null;
            if (recharge)
            {
                Place(count.rectTransform, new Vector2(1f, 1f), new Vector2(-16f, -6f), new Vector2(160f, 50f));
                timer = MakeText(bg.transform, "Timer", "00:00", 22, TextAnchor.MiddleRight, new Color(0.8f, 0.85f, 0.9f));
                Place(timer.rectTransform, new Vector2(1f, 0f), new Vector2(-16f, 6f), new Vector2(160f, 40f));
            }
            else
            {
                Place(count.rectTransform, new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(160f, 60f));
            }

            Wire(view, so =>
            {
                so.FindProperty("_currencyId").stringValue = id;
                so.FindProperty("_icon").objectReferenceValue = icon;
                so.FindProperty("_iconLabel").objectReferenceValue = initial;
                so.FindProperty("_countText").objectReferenceValue = count;
                so.FindProperty("_timerText").objectReferenceValue = timer;
                so.FindProperty("_button").objectReferenceValue = button;
                so.FindProperty("_goldSprite").objectReferenceValue = CoinSprite;
                so.FindProperty("_heartSprite").objectReferenceValue = HeartSprite;
            });
            return view;
        }

        private static LobbyTabButton TabButton(Transform parent, LobbyTab tab, ToggleGroup group, bool isOn)
        {
            var toggle = MakeToggle(parent, $"Tab_{tab}", new Vector2(480f, 140f), out var background, out var check);
            background.sprite = Sprite("TGL_LobbyBg") ?? background.sprite;
            background.color = Color.white;
            background.type = UnityEngine.UI.Image.Type.Sliced;
            check.sprite = Sprite("TGL_LobbyCheck") ?? check.sprite;
            check.color = new Color(Accent.r, Accent.g, Accent.b, 0.35f);
            toggle.group = group;
            toggle.isOn = isOn;

            var icon = MakeImage(toggle.transform, "Icon", Color.white, Sprite(tab == LobbyTab.Home ? "IMG_Home_On" : "IMG_Store_On") ?? CircleSprite);
            icon.type = UnityEngine.UI.Image.Type.Simple;
            icon.preserveAspect = true;
            Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(64f, 64f));

            var label = MakeText(toggle.transform, "Label", tab.ToString(), 30);
            Bar(label.rectTransform, 0f, 50f, 6f);

            var button = toggle.gameObject.AddComponent<LobbyTabButton>();
            Wire(button, so =>
            {
                so.FindProperty("_tab").intValue = (int)tab;
                so.FindProperty("_toggle").objectReferenceValue = toggle;
                so.FindProperty("_icon").objectReferenceValue = icon.rectTransform;
                so.FindProperty("_label").objectReferenceValue = label;
            });
            return button;
        }

        private static GameObject BuildHud()
        {
            var root = ViewRoot("HudView", typeof(HudView), UILayer.Hud);

            // 상단
            var top = MakeRect(root.transform, "Top");
            Bar(top, 1f, 150f, 20f);

            var profileBg = MakeImage(top, "ProfileButton", Color.white, Sprite("BTN_Profileback") ?? RoundedSprite);
            Place(profileBg.rectTransform, new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(280f, 110f));
            var profileButton = profileBg.gameObject.AddComponent<Button>();
            profileButton.targetGraphic = profileBg;
            var badge = ProfileBadge(profileBg.transform, "Badge", 90f);
            Place((RectTransform)badge.transform, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(90f, 90f));
            var nickname = MakeText(profileBg.transform, "Nickname", "Player", 28, TextAnchor.MiddleLeft);
            Place(nickname.rectTransform, new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(160f, 60f));

            var gold = Currency(top, CurrencyId.Gold, false);
            Place((RectTransform)gold.transform, new Vector2(0.5f, 0.5f), new Vector2(-20f, 0f), new Vector2(260f, 90f));
            var heart = Currency(top, CurrencyId.Heart, true);
            Place((RectTransform)heart.transform, new Vector2(1f, 0.5f), new Vector2(-120f, 0f), new Vector2(260f, 110f));

            var optionImage = MakeImage(top, "OptionButton", Color.white, Sprite("BTN_Setting") ?? RoundedSprite);
            var option = optionImage.gameObject.AddComponent<Button>();
            option.targetGraphic = optionImage;
            Place((RectTransform)option.transform, new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(90f, 90f));

            // 하단 탭
            var bottom = MakeImage(root.transform, "Bottom", Color.white, Sprite("TGL_LobbyBg") ?? RoundedSprite);
            Bar(bottom.rectTransform, 0f, 170f);
            var group = bottom.gameObject.AddComponent<ToggleGroup>();
            var store = TabButton(bottom.transform, LobbyTab.Store, group, false);
            Place((RectTransform)store.transform, new Vector2(0.25f, 0.5f), Vector2.zero, new Vector2(480f, 140f));
            var home = TabButton(bottom.transform, LobbyTab.Home, group, true);
            Place((RectTransform)home.transform, new Vector2(0.75f, 0.5f), Vector2.zero, new Vector2(480f, 140f));

            Wire(root.GetComponent<HudView>(), so =>
            {
                so.FindProperty("_optionButton").objectReferenceValue = option;
                so.FindProperty("_profileButton").objectReferenceValue = profileButton;
                so.FindProperty("_profileBadge").objectReferenceValue = badge;
                so.FindProperty("_nicknameText").objectReferenceValue = nickname;
                so.FindProperty("_gold").objectReferenceValue = gold;
                so.FindProperty("_heart").objectReferenceValue = heart;
                SetArray(so.FindProperty("_tabs"), store, home);
            });
            return root;
        }

        // ── 로비 패널 ───────────────────────────────────────────────────────────

        private static GameObject BuildLobbyPanel(string coinCellPath, string bundleCellPath)
        {
            var root = ViewRoot("LobbyPanelView", typeof(LobbyPanelView), UILayer.Content);

            var background = MakeImage(root.transform, "Background", Color.white, Sprite("IMG_LobbyBg"));
            Stretch(background.rectTransform);
            background.raycastTarget = false;

            var viewport = MakeRect(root.transform, "Viewport", typeof(RectMask2D));
            Stretch(viewport);

            // 페이지 컨테이너는 뷰포트의 2배 폭. 슬라이드는 anchoredPosition 만 움직입니다.
            var pageContent = MakeRect(viewport, "PageContent");
            pageContent.anchorMin = new Vector2(0f, 0f);
            pageContent.anchorMax = new Vector2(2f, 1f);
            pageContent.pivot = new Vector2(0f, 0.5f);
            pageContent.offsetMin = pageContent.offsetMax = Vector2.zero;

            var storePage = BuildStorePage(pageContent, coinCellPath, bundleCellPath);
            var homePage = BuildHomePage(pageContent);

            Wire(root.GetComponent<LobbyPanelView>(), so =>
            {
                so.FindProperty("_pageContent").objectReferenceValue = pageContent;
                so.FindProperty("_home").objectReferenceValue = homePage;
                so.FindProperty("_store").objectReferenceValue = storePage;
            });
            return root;
        }

        private static RectTransform Page(RectTransform parent, string name, int index, System.Type component)
        {
            var page = MakeRect(parent, name, component);
            page.anchorMin = new Vector2(0.5f * index, 0f);
            page.anchorMax = new Vector2(0.5f * (index + 1), 1f);
            page.offsetMin = page.offsetMax = Vector2.zero;
            return page;
        }

        private static HomePageView BuildHomePage(RectTransform pageContent)
        {
            var page = Page(pageContent, "HomePage", (int)LobbyTab.Home, typeof(HomePageView));

            var pattern = MakeImage(page, "Pattern", new Color(1f, 1f, 1f, 0.18f), Sprite("IMG_LobbyPattern") ?? RoundedSprite);
            Stretch(pattern.rectTransform, 40f, 40f, 200f, 220f);
            pattern.raycastTarget = false;

            var stage = MakeButton(page, "StageButton", "Level 1", Color.white, 40, out var stageLabel);
            stage.GetComponent<Image>().sprite = Sprite("IMG_NextLevel") ?? stage.GetComponent<Image>().sprite;
            stage.GetComponent<Image>().preserveAspect = true;
            Place((RectTransform)stage.transform, new Vector2(0.5f, 0f), new Vector2(0f, 270f), new Vector2(440f, 130f));

            var daily = MakeButton(page, "DailyRewardButton", "Daily", Color.white, 30, out var dailyLabel);
            Place((RectTransform)daily.transform, new Vector2(0f, 0f), new Vector2(60f, 440f), new Vector2(200f, 110f));

            var redDot = MakeImage(daily.transform, "RedDot", Color.white, Sprite("IMG_RedDot") ?? CircleSprite);
            redDot.type = UnityEngine.UI.Image.Type.Simple;
            Place(redDot.rectTransform, new Vector2(1f, 1f), new Vector2(14f, 14f), new Vector2(48f, 48f));
            var redDotCount = MakeText(redDot.transform, "Count", "1", 26);
            Stretch(redDotCount.rectTransform);

            Wire(page.GetComponent<HomePageView>(), so =>
            {
                so.FindProperty("_stageButton").objectReferenceValue = stage;
                so.FindProperty("_stageButtonText").objectReferenceValue = stageLabel;
                so.FindProperty("_dailyRewardButton").objectReferenceValue = daily;
                so.FindProperty("_dailyRewardLabel").objectReferenceValue = dailyLabel;
                so.FindProperty("_dailyRedDot").objectReferenceValue = redDot.gameObject;
                so.FindProperty("_dailyRedDotCount").objectReferenceValue = redDotCount;
            });
            return page.GetComponent<HomePageView>();
        }

        private static StorePageView BuildStorePage(RectTransform pageContent, string coinCellPath, string bundleCellPath)
        {
            var page = Page(pageContent, "StorePage", (int)LobbyTab.Store, typeof(StorePageView));

            var titlePanel = MakeImage(page, "TitlePanel", Color.white, Sprite("IMG_TitlePanel") ?? RoundedSprite);
            Bar(titlePanel.rectTransform, 1f, 130f, 145f);
            titlePanel.raycastTarget = false;
            var title = MakeText(titlePanel.transform, "Title", "Shop", 48);
            Stretch(title.rectTransform);

            var scroll = ScrollView(page, "ScrollView", out var content, 30f, new RectOffset(30, 30, 20, 40));
            Stretch((RectTransform)scroll.transform, 0f, 0f, 270f, 190f);
            content.GetComponent<VerticalLayoutGroup>().childControlHeight = true;

            var sections = new (ShopCategory category, GameObject root, Text title, Transform container)[3];
            var order = new[] { ShopCategory.SpecialOffer, ShopCategory.Bundle, ShopCategory.Coin };
            for (var i = 0; i < order.Length; i++)
            {
                var sectionRoot = MakeRect(content, $"Section_{order[i]}");
                VerticalLayout(sectionRoot, 12f, null, controlChildHeight: true);

                var sectionTitle = MakeText(sectionRoot, "Title", order[i].ToString(), 36, TextAnchor.MiddleLeft);
                Layout(sectionTitle.rectTransform, height: 56f);

                var container = MakeRect(sectionRoot, "Container");
                if (order[i] == ShopCategory.Coin)
                    MakeGrid(container, new Vector2(330f, 360f), new Vector2(15f, 15f), 3);
                else
                    VerticalLayout(container, 15f, null, controlChildHeight: true);

                sections[i] = (order[i], sectionRoot.gameObject, sectionTitle, container);
            }

            Wire(page.GetComponent<StorePageView>(), so =>
            {
                so.FindProperty("_storeTitle").objectReferenceValue = title;
                var array = so.FindProperty("_sections");
                array.arraySize = sections.Length;
                for (var i = 0; i < sections.Length; i++)
                {
                    var element = array.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("Category").intValue = (int)sections[i].category;
                    element.FindPropertyRelative("Root").objectReferenceValue = sections[i].root;
                    element.FindPropertyRelative("Title").objectReferenceValue = sections[i].title;
                    element.FindPropertyRelative("Container").objectReferenceValue = sections[i].container;
                }
                so.FindProperty("_coinCellPrefab").objectReferenceValue = Prefab<CoinProductCell>(coinCellPath);
                so.FindProperty("_bundleCellPrefab").objectReferenceValue = Prefab<BundleProductCell>(bundleCellPath);
            });
            return page.GetComponent<StorePageView>();
        }

        // ── 팝업 ────────────────────────────────────────────────────────────────

        private static void WirePopup(PopupViewBase popup, RectTransform panel, params Button[] closeButtons)
        {
            Wire(popup, so =>
            {
                SetArray(so.FindProperty("_closeButtons"), closeButtons);
                so.FindProperty("_panel").objectReferenceValue = panel;
            });
        }

        private static GameObject BuildMessagePopup()
        {
            var root = ViewRoot("MessagePopup", typeof(MessagePopup), UILayer.System);
            var panel = PopupShell(root, new Vector2(820f, 520f), out var dim, out var close, out var title);

            var body = MakeText(panel, "Body", "Body", 32);
            Stretch(body.rectTransform, 50f, 50f, 110f, 140f);

            var primary = MakeButton(panel, "PrimaryButton", "OK", Color.white, 32, out var primaryLabel);
            Place((RectTransform)primary.transform, new Vector2(1f, 0f), new Vector2(-40f, 36f), new Vector2(300f, 84f));
            var secondary = MakeButton(panel, "SecondaryButton", "Cancel", Color.white, 32, out var secondaryLabel);
            secondary.GetComponent<Image>().sprite = Sprite("BTN_CommonBlue") ?? secondary.GetComponent<Image>().sprite;
            Place((RectTransform)secondary.transform, new Vector2(0f, 0f), new Vector2(40f, 36f), new Vector2(300f, 84f));

            var popup = root.GetComponent<MessagePopup>();
            WirePopup(popup, panel, dim, close);
            Wire(popup, so =>
            {
                so.FindProperty("_title").objectReferenceValue = title;
                so.FindProperty("_body").objectReferenceValue = body;
                so.FindProperty("_primaryButton").objectReferenceValue = primary;
                so.FindProperty("_primaryLabel").objectReferenceValue = primaryLabel;
                so.FindProperty("_secondaryButton").objectReferenceValue = secondary;
                so.FindProperty("_secondaryLabel").objectReferenceValue = secondaryLabel;
            });
            return root;
        }

        private static GameObject BuildToast()
        {
            var root = ViewRoot("ToastView", typeof(ToastView), UILayer.System);

            var bubble = MakeImage(root.transform, "Bubble", Color.white, Sprite("IMG_ToastInfoBox") ?? RoundedSprite);
            bubble.raycastTarget = false;
            Place(bubble.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 280f), new Vector2(760f, 90f));
            var group = bubble.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var text = MakeText(bubble.transform, "Text", "Toast", 30);
            Stretch(text.rectTransform);

            Wire(root.GetComponent<ToastView>(), so =>
            {
                so.FindProperty("_bubble").objectReferenceValue = group;
                so.FindProperty("_text").objectReferenceValue = text;
            });
            return root;
        }

        private static GameObject BuildRewardPopup(string goodsItemPath)
        {
            var root = ViewRoot("RewardPopup", typeof(RewardPopup), UILayer.Popup);
            var panel = PopupShell(root, new Vector2(900f, 720f), out _, out var close, out var title);

            var itemRoot = MakeRect(panel, "Items");
            Stretch(itemRoot, 40f, 40f, 120f, 140f);
            HorizontalLayout(itemRoot, 40f);

            var hint = MakeText(panel, "TapHint", "Tap to continue", 28, TextAnchor.MiddleCenter, new Color(0.2f, 0.12f, 0.45f));
            Bar(hint.rectTransform, 0f, 80f, 30f);

            // 마지막 자식이라 패널 위에 깔립니다. 어디를 탭해도 닫힙니다.
            var tapAnywhere = InvisibleButton(root.transform, "TapAnywhere", new Color(0f, 0f, 0f, 0f));

            var popup = root.GetComponent<RewardPopup>();
            WirePopup(popup, panel, close);
            Wire(popup, so =>
            {
                so.FindProperty("_title").objectReferenceValue = title;
                so.FindProperty("_tapHint").objectReferenceValue = hint;
                so.FindProperty("_itemRoot").objectReferenceValue = itemRoot;
                so.FindProperty("_itemPrefab").objectReferenceValue = Prefab<GoodsItemView>(goodsItemPath);
                so.FindProperty("_tapAnywhere").objectReferenceValue = tapAnywhere;
            });
            return root;
        }

        private static Toggle OptionRow(RectTransform panel, string name, float y, out Text label)
        {
            var row = MakeRect(panel, name);
            Place(row, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(680f, 90f));
            label = MakeText(row, "Label", name, 32, TextAnchor.MiddleLeft);
            Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(400f, 60f));
            var toggle = MakeToggle(row, "Toggle", new Vector2(120f, 64f));
            Place((RectTransform)toggle.transform, new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(120f, 64f));
            return toggle;
        }

        private static GameObject BuildOptionPopup()
        {
            var root = ViewRoot("OptionPopup", typeof(OptionPopup), UILayer.Popup);
            var panel = PopupShell(root, new Vector2(800f, 900f), out var dim, out var close, out var title);

            var music = OptionRow(panel, "Music", -120f, out var musicLabel);
            var sfx = OptionRow(panel, "Sound", -220f, out var sfxLabel);
            var haptic = OptionRow(panel, "Vibration", -320f, out var hapticLabel);

            var language = MakeButton(panel, "LanguageButton", "Language", Color.white, 32, out var languageLabel);
            Place((RectTransform)language.transform, new Vector2(0.5f, 1f), new Vector2(0f, -450f), new Vector2(680f, 90f));
            var privacy = MakeButton(panel, "PrivacyButton", "Privacy Policy", Color.white, 32, out var privacyLabel);
            privacy.GetComponent<Image>().sprite = Sprite("BTN_Privacy") ?? privacy.GetComponent<Image>().sprite;
            Place((RectTransform)privacy.transform, new Vector2(0.5f, 1f), new Vector2(0f, -560f), new Vector2(680f, 90f));

            var version = MakeText(panel, "Version", "v0.0", 24, TextAnchor.MiddleCenter, new Color(0.6f, 0.6f, 0.65f));
            Bar(version.rectTransform, 0f, 60f, 20f);

            var popup = root.GetComponent<OptionPopup>();
            WirePopup(popup, panel, dim, close);
            Wire(popup, so =>
            {
                so.FindProperty("_title").objectReferenceValue = title;
                so.FindProperty("_music").objectReferenceValue = music;
                so.FindProperty("_musicLabel").objectReferenceValue = musicLabel;
                so.FindProperty("_sfx").objectReferenceValue = sfx;
                so.FindProperty("_sfxLabel").objectReferenceValue = sfxLabel;
                so.FindProperty("_haptic").objectReferenceValue = haptic;
                so.FindProperty("_hapticLabel").objectReferenceValue = hapticLabel;
                so.FindProperty("_languageButton").objectReferenceValue = language;
                so.FindProperty("_languageLabel").objectReferenceValue = languageLabel;
                so.FindProperty("_privacyButton").objectReferenceValue = privacy;
                so.FindProperty("_privacyLabel").objectReferenceValue = privacyLabel;
                so.FindProperty("_versionText").objectReferenceValue = version;
            });
            return root;
        }

        private static GameObject BuildLanguagePopup()
        {
            var root = ViewRoot("LanguagePopup", typeof(LanguagePopup), UILayer.Popup);
            var panel = PopupShell(root, new Vector2(700f, 520f), out var dim, out var close, out var title);
            var group = panel.gameObject.AddComponent<ToggleGroup>();

            var codes = new[] { "en", "ko" };
            var toggles = new Toggle[codes.Length];
            var labels = new Text[codes.Length];
            for (var i = 0; i < codes.Length; i++)
            {
                toggles[i] = MakeToggle(panel, $"Toggle_{codes[i]}", new Vector2(560f, 90f));
                var toggleImage = toggles[i].targetGraphic as Image;
                if (toggleImage != null)
                {
                    toggleImage.sprite = Sprite("IMG_LanguageBox") ?? toggleImage.sprite;
                    toggleImage.color = Color.white;
                }
                toggles[i].group = group;
                Place((RectTransform)toggles[i].transform, new Vector2(0.5f, 1f), new Vector2(0f, -130f - 110f * i), new Vector2(560f, 90f));
                labels[i] = MakeText(toggles[i].transform, "Label", codes[i], 32);
                Stretch(labels[i].rectTransform);
            }

            var popup = root.GetComponent<LanguagePopup>();
            WirePopup(popup, panel, dim, close);
            Wire(popup, so =>
            {
                so.FindProperty("_title").objectReferenceValue = title;
                var array = so.FindProperty("_toggles");
                array.arraySize = codes.Length;
                for (var i = 0; i < codes.Length; i++)
                {
                    var element = array.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("Code").stringValue = codes[i];
                    element.FindPropertyRelative("Toggle").objectReferenceValue = toggles[i];
                    element.FindPropertyRelative("Label").objectReferenceValue = labels[i];
                }
            });
            return root;
        }

        private static GameObject BuildProfilePopup(string profileCellPath)
        {
            var root = ViewRoot("ProfilePopup", typeof(ProfilePopup), UILayer.Popup);
            var panel = PopupShell(root, new Vector2(900f, 1200f), out var dim, out var close, out var title);

            var badge = ProfileBadge(panel, "Badge", 160f);
            Place((RectTransform)badge.transform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(160f, 160f));

            // 닉네임 입력
            var inputBg = MakeImage(panel, "Nickname", Color.white, Sprite("IMG_ProfileNameBox") ?? RoundedSprite);
            Place(inputBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(520f, 80f));
            var input = inputBg.gameObject.AddComponent<InputField>();
            var inputText = MakeText(inputBg.transform, "Text", "", 32, TextAnchor.MiddleCenter);
            Stretch(inputText.rectTransform, 20f, 20f);
            inputText.supportRichText = false;
            var placeholder = MakeText(inputBg.transform, "Placeholder", "Nickname", 32, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.4f));
            Stretch(placeholder.rectTransform, 20f, 20f);
            input.textComponent = inputText;
            input.placeholder = placeholder;
            input.characterLimit = 12;

            // 탭
            var tabGroup = panel.gameObject.AddComponent<ToggleGroup>();
            var avatarTab = MakeToggle(panel, "AvatarTab", new Vector2(300f, 80f));
            if (avatarTab.targetGraphic is Image avatarBg)
                avatarBg.sprite = Sprite("BTN_AvatarDown") ?? avatarBg.sprite;
            if (avatarTab.graphic is Image avatarCheck)
                avatarCheck.sprite = Sprite("BTN_Avatar") ?? avatarCheck.sprite;
            avatarTab.group = tabGroup;
            Place((RectTransform)avatarTab.transform, new Vector2(0.5f, 1f), new Vector2(-160f, -410f), new Vector2(300f, 80f));
            var avatarLabel = MakeText(avatarTab.transform, "Label", "Avatar", 30);
            Stretch(avatarLabel.rectTransform);
            var frameTab = MakeToggle(panel, "FrameTab", new Vector2(300f, 80f));
            if (frameTab.targetGraphic is Image frameBg)
                frameBg.sprite = Sprite("BTN_FrameDown") ?? frameBg.sprite;
            if (frameTab.graphic is Image frameCheck)
                frameCheck.sprite = Sprite("BTN_Frame") ?? frameCheck.sprite;
            frameTab.group = tabGroup;
            frameTab.isOn = false;
            Place((RectTransform)frameTab.transform, new Vector2(0.5f, 1f), new Vector2(160f, -410f), new Vector2(300f, 80f));
            var frameLabel = MakeText(frameTab.transform, "Label", "Frame", 30);
            Stretch(frameLabel.rectTransform);

            // 선택 그리드
            var scroll = ScrollView(panel, "ScrollView", out var content, 16f, new RectOffset(20, 20, 20, 20));
            Stretch((RectTransform)scroll.transform, 40f, 40f, 480f, 40f);
            Object.DestroyImmediate(content.GetComponent<VerticalLayoutGroup>());
            MakeGrid(content, new Vector2(160f, 160f), new Vector2(16f, 16f), 4).padding = new RectOffset(20, 20, 20, 20);

            var popup = root.GetComponent<ProfilePopup>();
            WirePopup(popup, panel, dim, close);
            Wire(popup, so =>
            {
                so.FindProperty("_title").objectReferenceValue = title;
                so.FindProperty("_badge").objectReferenceValue = badge;
                so.FindProperty("_nickname").objectReferenceValue = input;
                so.FindProperty("_avatarTab").objectReferenceValue = avatarTab;
                so.FindProperty("_avatarTabLabel").objectReferenceValue = avatarLabel;
                so.FindProperty("_frameTab").objectReferenceValue = frameTab;
                so.FindProperty("_frameTabLabel").objectReferenceValue = frameLabel;
                so.FindProperty("_cellRoot").objectReferenceValue = content;
                so.FindProperty("_cellPrefab").objectReferenceValue = Prefab<ProfileItemCell>(profileCellPath);
            });
            return root;
        }

        private static DailyRewardCell DailyCell(Transform parent, int dayIndex)
        {
            var bg = MakeImage(parent, $"Day{dayIndex + 1}", Color.white, Sprite("IMG_RewardBox") ?? RoundedSprite);
            var cell = bg.gameObject.AddComponent<DailyRewardCell>();

            var day = MakeText(bg.transform, "Day", $"Day {dayIndex + 1}", 30);
            Bar(day.rectTransform, 1f, 50f, 8f);

            var slotsRoot = MakeRect(bg.transform, "Slots");
            Place(slotsRoot, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(260f, 130f));
            HorizontalLayout(slotsRoot, 12f);
            var slots = new GoodsItemView[2];
            for (var i = 0; i < slots.Length; i++)
            {
                slots[i] = GoodsItem(slotsRoot, $"Slot{i}", new Vector2(110f, 126f), 24);
                Layout((RectTransform)slots[i].transform, height: 126f, width: 110f);
            }

            var claim = MakeButton(bg.transform, "ClaimButton", "Claim", Positive, 28, out var claimLabel);
            Place((RectTransform)claim.transform, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(220f, 64f));

            var locked = MakeImage(bg.transform, "Locked", new Color(0f, 0f, 0f, 0.5f), RoundedSprite);
            Stretch(locked.rectTransform);
            var lockText = MakeText(locked.transform, "Text", "LOCKED", 28, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.85f));
            Stretch(lockText.rectTransform);

            var received = MakeImage(bg.transform, "Received", new Color(0f, 0f, 0f, 0.5f), RoundedSprite);
            Stretch(received.rectTransform);
            var check = MakeImage(received.transform, "Check", Positive, CheckSprite);
            check.type = UnityEngine.UI.Image.Type.Simple;
            Place(check.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90f, 90f));

            Wire(cell, so =>
            {
                so.FindProperty("_dayText").objectReferenceValue = day;
                SetArray(so.FindProperty("_slots"), slots);
                so.FindProperty("_claimButton").objectReferenceValue = claim;
                so.FindProperty("_claimLabel").objectReferenceValue = claimLabel;
                so.FindProperty("_lockedOverlay").objectReferenceValue = locked.gameObject;
                so.FindProperty("_receivedOverlay").objectReferenceValue = received.gameObject;
                so.FindProperty("_background").objectReferenceValue = bg;
            });
            return cell;
        }

        private static GameObject BuildDailyRewardsPopup()
        {
            var root = ViewRoot("DailyRewardsPopup", typeof(DailyRewardsPopup), UILayer.Popup);
            var panel = PopupShell(root, new Vector2(1000f, 1320f), out var dim, out var close, out var title);

            var grid = MakeRect(panel, "Days");
            Place(grid, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(940f, 860f));
            MakeGrid(grid, new Vector2(300f, 270f), new Vector2(20f, 20f), 3);
            var cells = new DailyRewardCell[DailyRewardState.CycleDays];
            for (var i = 0; i < cells.Length; i++)
                cells[i] = DailyCell(grid, i);

            // 무료 코인
            var free = MakeImage(panel, "FreeCoin", Color.white, Sprite("IMG_ContentsBox") ?? RoundedSprite);
            Place(free.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(940f, 260f));
            var freeTitle = MakeText(free.transform, "Title", "Free Coin", 32);
            Bar(freeTitle.rectTransform, 1f, 60f, 10f);
            var freeItem = GoodsItem(free.transform, "Item", new Vector2(120f, 140f), 26);
            Place((RectTransform)freeItem.transform, new Vector2(0f, 0.5f), new Vector2(60f, -20f), new Vector2(120f, 140f));
            var freeClaim = MakeButton(free.transform, "ClaimButton", "Claim", Positive, 30, out var freeClaimLabel);
            Place((RectTransform)freeClaim.transform, new Vector2(1f, 0.5f), new Vector2(-60f, -20f), new Vector2(260f, 80f));
            var freeWait = MakeText(free.transform, "Wait", "Next in 00:00", 28, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.85f));
            Place(freeWait.rectTransform, new Vector2(1f, 0.5f), new Vector2(-60f, -20f), new Vector2(360f, 80f));

            var popup = root.GetComponent<DailyRewardsPopup>();
            WirePopup(popup, panel, dim, close);
            Wire(popup, so =>
            {
                so.FindProperty("_title").objectReferenceValue = title;
                SetArray(so.FindProperty("_dayCells"), cells);
                so.FindProperty("_freeItem").objectReferenceValue = freeItem;
                so.FindProperty("_freeClaimButton").objectReferenceValue = freeClaim;
                so.FindProperty("_freeClaimLabel").objectReferenceValue = freeClaimLabel;
                so.FindProperty("_freeTitle").objectReferenceValue = freeTitle;
                so.FindProperty("_freeWaitText").objectReferenceValue = freeWait;
            });
            return root;
        }

        private static GameObject BuildRefillPopup()
        {
            var root = ViewRoot("RefillPopup", typeof(RefillPopup), UILayer.Popup);
            var panel = PopupShell(root, new Vector2(800f, 620f), out var dim, out var close, out var title);

            var desc = MakeText(panel, "Desc", "Refill all hearts with gold.", 28, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.85f));
            Place(desc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(700f, 50f));

            var heartsRoot = MakeRect(panel, "Hearts");
            Place(heartsRoot, new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(600f, 90f));
            HorizontalLayout(heartsRoot, 16f);
            var hearts = new Image[PlayerWallet.HeartMax];
            for (var i = 0; i < hearts.Length; i++)
            {
                hearts[i] = MakeImage(heartsRoot, $"Heart{i}", Color.white, Sprite("IMG_MoreLive_Heart") ?? HeartSprite ?? CircleSprite);
                hearts[i].type = UnityEngine.UI.Image.Type.Simple;
                Layout(hearts[i].rectTransform, height: 80f, width: 80f);
            }

            var refillCount = MakeText(panel, "RefillCount", "+0", 40);
            Place(refillCount.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -320f), new Vector2(300f, 60f));

            var buy = MakeButton(panel, "BuyButton", "Refill", Positive, 32, out var buyLabel);
            Place((RectTransform)buy.transform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(360f, 90f));
            var price = MakeText(panel, "Price", "0", 30, TextAnchor.MiddleCenter, new Color(1f, 0.82f, 0.2f));
            Place(price.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(300f, 50f));

            var popup = root.GetComponent<RefillPopup>();
            WirePopup(popup, panel, dim, close);
            Wire(popup, so =>
            {
                so.FindProperty("_title").objectReferenceValue = title;
                so.FindProperty("_desc").objectReferenceValue = desc;
                SetArray(so.FindProperty("_heartIcons"), hearts);
                so.FindProperty("_refillCountText").objectReferenceValue = refillCount;
                so.FindProperty("_priceText").objectReferenceValue = price;
                so.FindProperty("_buyButton").objectReferenceValue = buy;
                so.FindProperty("_buyLabel").objectReferenceValue = buyLabel;
            });
            return root;
        }
    }
}
