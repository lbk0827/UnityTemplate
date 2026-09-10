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
                ["hud.tab.home"] = "Home", ["hud.tab.lock"] = "LOCK", ["hud.tab.store"] = "Shop", ["hud.full"] = "FULL",
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
                ["hud.tab.home"] = "홈", ["hud.tab.lock"] = "잠금", ["hud.tab.store"] = "상점", ["hud.full"] = "가득",
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
            => GoodsItem(null, "GoodsItem", new Vector2(176f, 192f), 55).gameObject;

        private static GameObject BuildProfileCell()
        {
            var bg = MakeImage(null, "ProfileItemCell", Color.white, Sprite("IMG_AvatarBack") ?? RoundedSprite);
            bg.rectTransform.sizeDelta = new Vector2(180f, 180f);
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            var cell = bg.gameObject.AddComponent<ProfileItemCell>();

            var badge = ProfileBadge(bg.transform, "Badge", 170f);
            Place((RectTransform)badge.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170f, 170f));

            var check = MakeImage(bg.transform, "Check", Color.white, Sprite("IMG_Profile_Check") ?? CheckSprite);
            check.type = UnityEngine.UI.Image.Type.Simple;
            Place(check.rectTransform, new Vector2(1f, 0f), new Vector2(-16f, 14f), new Vector2(86f, 68f));
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
        private static RectTransform CardBody(RectTransform root, out CanvasGroup group, string spriteName)
        {
            var body = MakeRect(root, "Body", typeof(CanvasGroup), typeof(Image));
            Stretch(body);
            var image = body.GetComponent<Image>();
            image.sprite = Sprite(spriteName) ?? Sprite("IMG_Coin_Goods_Bg") ?? Sprite("IMG_ContentsBox") ?? RoundedSprite;
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
            root.sizeDelta = new Vector2(330f, 390f);
            var body = CardBody(root, out var group, "IMG_Coin_Goods_Bg");

            var coinLight = MakeImage(body, "CoinLight", Color.white, Sprite("IMG_GoodsLight"));
            Place(coinLight.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(210f, 210f));
            coinLight.raycastTarget = false;

            var item = GoodsItem(body, "MainItem", new Vector2(176f, 192f), 48);
            Place((RectTransform)item.transform, new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(176f, 192f));

            var buy = MakeButton(body, "BuyButton", "$0.00", Color.white, 34, out var price);
            if (buy.GetComponent<Image>() != null)
                buy.GetComponent<Image>().sprite = Sprite("IMG_CoinBuyFrame") ?? buy.GetComponent<Image>().sprite;
            Place((RectTransform)buy.transform, new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(260f, 112f));

            var cell = root.GetComponent<CoinProductCell>();
            WireCard(cell, group, body, buy, price);
            Wire(cell, so => so.FindProperty("_mainItem").objectReferenceValue = item);
            return root.gameObject;
        }

        private static GameObject BuildBundleCell()
        {
            var root = MakeRect(null, "BundleProductCell", typeof(BundleProductCell));
            Layout(root, height: 360f);
            var body = CardBody(root, out var group, "IMG_Bundle_Bg");

            var main = GoodsItem(body, "MainItem", new Vector2(176f, 192f), 46);
            Place((RectTransform)main.transform, new Vector2(0f, 0.5f), new Vector2(74f, 18f), new Vector2(176f, 192f));

            var name = MakeText(body, "Name", "Bundle", 48, TextAnchor.MiddleLeft);
            Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(270f, -50f), new Vector2(520f, 70f));
            var desc = MakeText(body, "Desc", "Description", 30, TextAnchor.MiddleLeft, new Color(0.2f, 0.12f, 0.45f));
            Place(desc.rectTransform, new Vector2(0f, 1f), new Vector2(270f, -116f), new Vector2(520f, 50f));

            var extrasRoot = MakeRect(body, "Extras");
            Place(extrasRoot, new Vector2(0f, 1f), new Vector2(270f, -178f), new Vector2(400f, 120f));
            HorizontalLayout(extrasRoot, 16f, TextAnchor.MiddleLeft);
            var extras = new GoodsItemView[3];
            for (var i = 0; i < extras.Length; i++)
            {
                extras[i] = GoodsItem(extrasRoot, $"Extra{i}", new Vector2(94f, 104f), 28);
                Layout((RectTransform)extras[i].transform, height: 104f, width: 94f);
            }

            var labelRoot = MakeImage(body, "Label", Color.white, Sprite("IMG_DiscountBg") ?? RoundedSprite);
            Place(labelRoot.rectTransform, new Vector2(1f, 1f), new Vector2(-94f, -36f), new Vector2(188f, 72f));
            var labelText = MakeText(labelRoot.transform, "Text", "LABEL", 30);
            Stretch(labelText.rectTransform);

            var buy = MakeButton(body, "BuyButton", "$0.00", Color.white, 34, out var price);
            if (buy.GetComponent<Image>() != null)
                buy.GetComponent<Image>().sprite = Sprite("IMG_PackageBuyFrame") ?? buy.GetComponent<Image>().sprite;
            Place((RectTransform)buy.transform, new Vector2(1f, 0f), new Vector2(-144f, 34f), new Vector2(260f, 112f));

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
            bg.rectTransform.sizeDelta = recharge ? new Vector2(246f, 74f) : new Vector2(218f, 74f);
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            var view = bg.gameObject.AddComponent<CurrencyHudView>();

            var icon = MakeImage(bg.transform, "Icon", Color.white, id == CurrencyId.Heart ? HeartSprite : CoinSprite);
            icon.type = UnityEngine.UI.Image.Type.Simple;
            icon.preserveAspect = true;
            Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(-38f, id == CurrencyId.Heart ? 3f : 0f), new Vector2(90f, id == CurrencyId.Heart ? 80f : 90f));
            var initial = MakeText(icon.transform, "Initial", "G", 30, TextAnchor.MiddleCenter, new Color(0.1f, 0.1f, 0.1f));
            Stretch(initial.rectTransform);

            var plus = MakeImage(bg.transform, "Plus", Color.white, Sprite("BTN_Plus"));
            Place(plus.rectTransform, new Vector2(0f, 0f), new Vector2(40f, -8f), new Vector2(56f, 56f));
            plus.raycastTarget = false;

            var count = MakeText(bg.transform, "Count", "0", 42, TextAnchor.MiddleCenter);
            Text timer = null;
            if (recharge)
            {
                Place(count.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(37f, 15f), new Vector2(120f, 44f));
                timer = MakeText(bg.transform, "Timer", "00:00", 34, TextAnchor.MiddleCenter);
                Place(timer.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(37f, -16f), new Vector2(150f, 42f));
            }
            else
            {
                Place(count.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(34f, 2f), new Vector2(136f, 60f));
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
            var toggle = MakeToggle(parent, $"Tab_{tab}", new Vector2(298f, 252f), out var background, out var check);
            background.sprite = Sprite("TGL_LobbyBg") ?? background.sprite;
            background.color = new Color(1f, 1f, 1f, 0f);
            background.type = UnityEngine.UI.Image.Type.Sliced;
            Place(background.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -22.5f), new Vector2(298f, 207f));

            var offIcon = MakeImage(background.transform, "IMG_Off", Color.white, Sprite(TabIcon(tab, false)) ?? CircleSprite);
            offIcon.type = UnityEngine.UI.Image.Type.Simple;
            offIcon.preserveAspect = true;
            Place(offIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(104f, 122f));

            check.sprite = Sprite("TGL_LobbyCheck") ?? check.sprite;
            check.color = Color.white;
            Place(check.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(366f, 332f));
            toggle.group = group;
            toggle.isOn = isOn;

            var icon = MakeImage(check.transform, "IMG_On", Color.white, Sprite(TabIcon(tab, true)) ?? CircleSprite);
            icon.type = UnityEngine.UI.Image.Type.Simple;
            icon.preserveAspect = true;
            Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 48f), new Vector2(116f, 136f));

            var label = MakeText(icon.transform, "TXT_On", tab.ToString(), 48);
            Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -95f), new Vector2(200f, 60f));

            var button = toggle.gameObject.AddComponent<LobbyTabButton>();
            Wire(button, so =>
            {
                so.FindProperty("_tab").intValue = (int)tab;
                so.FindProperty("_toggle").objectReferenceValue = toggle;
                so.FindProperty("_icon").objectReferenceValue = icon.rectTransform;
                so.FindProperty("_offIcon").objectReferenceValue = offIcon.rectTransform;
                so.FindProperty("_label").objectReferenceValue = label;
            });
            return button;
        }

        private static string TabIcon(LobbyTab tab, bool selected)
        {
            var suffix = selected ? "On" : "Off";
            return tab switch
            {
                LobbyTab.Home => $"IMG_Home_{suffix}",
                LobbyTab.Lock => $"IMG_Lock_{suffix}",
                _ => $"IMG_Store_{suffix}",
            };
        }

        private static GameObject BuildHud()
        {
            var root = ViewRoot("HudView", typeof(HudView), UILayer.Hud);

            var top = MakeRect(root.transform, "Top");
            Place(top, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(900f, 100f));

            var profileBg = MakeImage(top, "ProfileButton", new Color(1f, 1f, 1f, 0f), Sprite("IMG_Square"));
            Place(profileBg.rectTransform, new Vector2(0f, 0.5f), new Vector2(90f, -6f), new Vector2(180f, 185f));
            var profileButton = profileBg.gameObject.AddComponent<Button>();
            profileButton.targetGraphic = profileBg;
            var badge = ProfileBadge(profileBg.transform, "Badge", 180f);
            Place((RectTransform)badge.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180f, 180f));
            var nickname = MakeText(profileBg.transform, "Nickname", "Player", 28, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0f));
            Place(nickname.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -24f), new Vector2(180f, 50f));

            var gold = Currency(top, CurrencyId.Gold, false);
            Place((RectTransform)gold.transform, new Vector2(0f, 1f), new Vector2(242f, -65f), new Vector2(218f, 74f));
            var heart = Currency(top, CurrencyId.Heart, true);
            Place((RectTransform)heart.transform, new Vector2(0f, 1f), new Vector2(480f, -65f), new Vector2(246f, 74f));

            var optionImage = MakeImage(top, "OptionButton", Color.white, Sprite("BTN_SettingFrame") ?? RoundedSprite);
            var optionIcon = MakeImage(optionImage.transform, "Icon", Color.white, Sprite("BTN_Setting"));
            Place(optionIcon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 93f));
            var option = optionImage.gameObject.AddComponent<Button>();
            option.targetGraphic = optionImage;
            Place((RectTransform)option.transform, new Vector2(1f, 1f), new Vector2(-72f, -63f), new Vector2(118f, 112f));

            var bottom = MakeRect(root.transform, "UI_Bottom");
            Bar(bottom, 0f, 335f);
            var bottomBg = MakeImage(bottom, "IMG_LobbyBg", Color.white, Sprite("TGL_LobbyBg") ?? RoundedSprite);
            Bar(bottomBg.rectTransform, 0f, 207f);
            var lineColor = new Color(0.11f, 0.04f, 0.31f, 1f);
            var line1 = MakeImage(bottomBg.transform, "IMG_Line1", lineColor, Sprite("IMG_Square"));
            Place(line1.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-151f, -5f), new Vector2(4f, 196f));
            var line2 = MakeImage(bottomBg.transform, "IMG_Line2", lineColor, Sprite("IMG_Square"));
            Place(line2.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(151f, -5f), new Vector2(4f, 196f));

            var group = bottom.gameObject.AddComponent<ToggleGroup>();
            var store = TabButton(bottom.transform, LobbyTab.Store, group, false);
            Place((RectTransform)store.transform, new Vector2(1f / 6f, 0f), new Vector2(0f, 22f), new Vector2(298f, 252f));
            var home = TabButton(bottom.transform, LobbyTab.Home, group, true);
            Place((RectTransform)home.transform, new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(298f, 252f));
            var lockTab = TabButton(bottom.transform, LobbyTab.Lock, group, false);
            Place((RectTransform)lockTab.transform, new Vector2(5f / 6f, 0f), new Vector2(0f, 22f), new Vector2(298f, 252f));

            Wire(root.GetComponent<HudView>(), so =>
            {
                so.FindProperty("_optionButton").objectReferenceValue = option;
                so.FindProperty("_profileButton").objectReferenceValue = profileButton;
                so.FindProperty("_profileBadge").objectReferenceValue = badge;
                so.FindProperty("_nicknameText").objectReferenceValue = nickname;
                so.FindProperty("_gold").objectReferenceValue = gold;
                so.FindProperty("_heart").objectReferenceValue = heart;
                SetArray(so.FindProperty("_tabs"), store, home, lockTab);
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
            background.type = UnityEngine.UI.Image.Type.Sliced;

            var pattern = MakeImage(root.transform, "LobbyPattern", new Color(1f, 1f, 1f, 0.16f), Sprite("IMG_LobbyPattern"));
            Stretch(pattern.rectTransform);
            pattern.type = UnityEngine.UI.Image.Type.Tiled;
            pattern.raycastTarget = false;

            var viewport = MakeRect(root.transform, "Viewport", typeof(RectMask2D));
            Stretch(viewport);

            // 페이지 컨테이너는 뷰포트의 3배 폭. 슬라이드는 anchoredPosition 만 움직입니다.
            var pageContent = MakeRect(viewport, "PageContent");
            pageContent.anchorMin = new Vector2(0f, 0f);
            pageContent.anchorMax = new Vector2(3f, 1f);
            pageContent.pivot = new Vector2(0f, 0.5f);
            pageContent.offsetMin = pageContent.offsetMax = Vector2.zero;

            var storePage = BuildStorePage(pageContent, coinCellPath, bundleCellPath);
            var homePage = BuildHomePage(pageContent);
            var lockPage = BuildLockPage(pageContent);

            Wire(root.GetComponent<LobbyPanelView>(), so =>
            {
                so.FindProperty("_pageContent").objectReferenceValue = pageContent;
                so.FindProperty("_home").objectReferenceValue = homePage;
                so.FindProperty("_store").objectReferenceValue = storePage;
                so.FindProperty("_lock").objectReferenceValue = lockPage;
            });
            return root;
        }

        private static RectTransform Page(RectTransform parent, string name, int index, System.Type component)
        {
            var page = MakeRect(parent, name, component);
            page.anchorMin = new Vector2(index / 3f, 0f);
            page.anchorMax = new Vector2((index + 1) / 3f, 1f);
            page.offsetMin = page.offsetMax = Vector2.zero;
            return page;
        }

        private static HomePageView BuildHomePage(RectTransform pageContent)
        {
            var page = Page(pageContent, "HomePage", (int)LobbyTab.Home, typeof(HomePageView));

            var light = MakeImage(page, "LobbyLight", new Color(0.92f, 0.73f, 0.92f, 0.49f), Sprite("IMG_LobbyLight"));
            light.type = UnityEngine.UI.Image.Type.Simple;
            light.preserveAspect = true;
            Place(light.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(704f, 704f));
            light.raycastTarget = false;

            var shadow = MakeImage(page, "TopShadow", Color.white, Sprite("IMG_LobbyShadow"));
            Bar(shadow.rectTransform, 1f, 410f);
            shadow.raycastTarget = false;

            var lineMask = MakeRect(page, "LevelLineRoot", typeof(RectMask2D));
            Place(lineMask, new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(900f, 950f));
            var levelLine = MakeImage(lineMask, "LevelLine", Color.white, Sprite("IMG_BG_Line"));
            Place(levelLine.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72f, 1172f));
            levelLine.raycastTarget = false;

            for (var i = 0; i < 3; i++)
            {
                var next = MakeImage(page, $"NextLevel_{i + 1}", new Color(1f - i * 0.14f, 1f - i * 0.14f, 1f - i * 0.14f), Sprite("IMG_NextLevel"));
                next.type = UnityEngine.UI.Image.Type.Simple;
                next.preserveAspect = true;
                Place(next.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(150f, 120f - 278f * i), new Vector2(226f, 228f));
                next.raycastTarget = false;
            }

            var stageImage = MakeImage(page, "StageButton", Color.white, Sprite("IMG_NextLevel"));
            stageImage.type = UnityEngine.UI.Image.Type.Simple;
            stageImage.preserveAspect = true;
            var stage = stageImage.gameObject.AddComponent<Button>();
            stage.targetGraphic = stageImage;
            var stageLabel = MakeText(stageImage.transform, "Label", "Level 1", 40);
            Place((RectTransform)stage.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(300f, 220f));
            Place(stageLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -86f), new Vector2(240f, 60f));

            var daily = MakeButton(page, "DailyRewardButton", "Daily", Color.white, 30, out var dailyLabel);
            daily.GetComponent<Image>().sprite = Sprite("BTN_CommonGreen") ?? daily.GetComponent<Image>().sprite;
            Place((RectTransform)daily.transform, new Vector2(0.5f, 1f), new Vector2(-334f, -164f), new Vector2(220f, 112f));

            var redDot = MakeImage(daily.transform, "RedDot", Color.white, Sprite("IMG_RedDot") ?? CircleSprite);
            redDot.type = UnityEngine.UI.Image.Type.Simple;
            Place(redDot.rectTransform, new Vector2(1f, 1f), new Vector2(10f, 22f), new Vector2(61f, 63f));
            var redDotCount = MakeText(redDot.transform, "Count", "1", 26);
            Stretch(redDotCount.rectTransform);

            BuildHomeRightMenu(page);

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

        private static void BuildHomeRightMenu(RectTransform page)
        {
            var right = MakeRect(page, "UI_Right");
            Place(right, new Vector2(0.5f, 1f), new Vector2(334f, -164f), new Vector2(200f, 0f));

            LobbyIconButton(right, "BTN_Welcome", "IMG_Welcome_Icon", "IMG_Coin", null, new Vector2(0f, 0f));
            LobbyIconButton(right, "Lobby_EndlessOffer", "IMG_Rocket", null, "4h 36m", new Vector2(0f, -188f));
            LobbyIconButton(right, "Lobby_EndlessGift", "IMG_AirPlane", null, "4h 36m", new Vector2(0f, -376f));
            LobbyIconButton(right, "btn_ad", null, null, null, new Vector2(0f, -564f), "AD");
        }

        private static Button LobbyIconButton(
            Transform parent,
            string name,
            string iconSprite,
            string subSprite,
            string timerText,
            Vector2 position,
            string fallbackText = null)
        {
            var root = MakeRect(parent, name);
            Place(root, new Vector2(0.5f, 1f), position, new Vector2(184f, 184f));

            var box = MakeImage(root, "IMG_Box", Color.white, Sprite("BTN_Lobby") ?? RoundedSprite);
            Place(box.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(184f, 184f));
            var button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = box;

            var circleSprite = name.Contains("Gift") ? "IMG_Circle" : "IMG_CircleBg";
            var circle = MakeImage(box.transform, "IMG_Circle", Color.white, Sprite(circleSprite));
            circle.type = UnityEngine.UI.Image.Type.Simple;
            circle.preserveAspect = true;
            circle.raycastTarget = false;
            Place(circle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-1f, 2f), new Vector2(138f, 138f));

            if (!string.IsNullOrEmpty(iconSprite))
            {
                var icon = MakeImage(box.transform, "IMG_Icon", Color.white, Sprite(iconSprite) ?? CircleSprite);
                icon.type = UnityEngine.UI.Image.Type.Simple;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                var iconSize = iconSprite == "IMG_Welcome_Icon" ? new Vector2(150f, 149f) : new Vector2(150f, 150f);
                Place(icon.rectTransform, new Vector2(0.5f, 0.5f), iconSprite == "IMG_Welcome_Icon" ? new Vector2(2f, 2f) : new Vector2(22f, -22f), iconSize);
            }

            if (!string.IsNullOrEmpty(subSprite))
            {
                var sub = MakeImage(box.transform, "IMG_Coin", Color.white, Sprite(subSprite));
                sub.type = UnityEngine.UI.Image.Type.Simple;
                sub.preserveAspect = true;
                sub.raycastTarget = false;
                Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1f, -38f), new Vector2(134f, 58f));
            }

            if (!string.IsNullOrEmpty(timerText))
            {
                var timer = MakeImage(root, "UI_Timer", Color.white, Sprite("IMG_Icon_BottomBar") ?? RoundedSprite);
                Place(timer.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -67f), new Vector2(158f, 56f));
                var text = MakeText(timer.transform, "TXT_Remain", timerText, 32);
                Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(120f, 38f));
            }

            var redDot = MakeImage(root, "IMG_RedDot", Color.white, Sprite("IMG_RedDot") ?? CircleSprite);
            redDot.type = UnityEngine.UI.Image.Type.Simple;
            redDot.raycastTarget = false;
            Place(redDot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(60f, 60f), new Vector2(56f, 60f));

            if (!string.IsNullOrEmpty(fallbackText))
            {
                var text = MakeText(box.transform, "TXT_Label", fallbackText, 48, TextAnchor.MiddleCenter);
                Stretch(text.rectTransform);
            }

            return button;
        }

        private static LockPageView BuildLockPage(RectTransform pageContent)
        {
            var page = Page(pageContent, "LockPage", (int)LobbyTab.Lock, typeof(LockPageView));

            var shadow = MakeImage(page, "IMG_Shadow", Color.white, Sprite("IMG_LobbyShadow"));
            Bar(shadow.rectTransform, 1f, 410f);
            shadow.raycastTarget = false;

            var dimShadow = MakeImage(shadow.transform, "IMG_Shadow", new Color(0f, 0f, 0f, 0.53f), Sprite("IMG_LobbyShadow"));
            Bar(dimShadow.rectTransform, 1f, 410f);
            dimShadow.raycastTarget = false;

            var comingSoon = MakeImage(page, "IMG_ComingSoon", Color.white, Sprite("IMG_ComingSoon"));
            comingSoon.type = UnityEngine.UI.Image.Type.Simple;
            comingSoon.preserveAspect = true;
            comingSoon.raycastTarget = false;
            Place(comingSoon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 115f), new Vector2(636f, 492f));

            var label = MakeText(page, "txt_comingsoon", "Coming Soon!", 80);
            Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -165f), new Vector2(800f, 100f));

            return page.GetComponent<LockPageView>();
        }

        private static StorePageView BuildStorePage(RectTransform pageContent, string coinCellPath, string bundleCellPath)
        {
            var page = Page(pageContent, "StorePage", (int)LobbyTab.Store, typeof(StorePageView));

            var shopBox = MakeImage(page, "ShopBox", Color.white, Sprite("IMG_ShopBox") ?? Sprite("IMG_ContentsBox"));
            Stretch(shopBox.rectTransform, 28f, 28f, 250f, 195f);
            shopBox.raycastTarget = false;

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

                var sectionArt = order[i] switch
                {
                    ShopCategory.SpecialOffer => "IMG_ShopTitle_Special",
                    ShopCategory.Bundle => "IMG_ShopTitle_Package",
                    _ => "IMG_ShopTitle_Coin",
                };
                var sectionTitleBg = MakeImage(sectionRoot, "TitleBg", Color.white, Sprite(sectionArt) ?? Sprite("IMG_Shop_Tag") ?? RoundedSprite);
                Layout(sectionTitleBg.rectTransform, height: 76f);
                var sectionTitle = MakeText(sectionTitleBg.transform, "Title", order[i].ToString(), 36, TextAnchor.MiddleCenter);
                Stretch(sectionTitle.rectTransform);

                var container = MakeRect(sectionRoot, "Container");
                if (order[i] == ShopCategory.Coin)
                    MakeGrid(container, new Vector2(330f, 390f), new Vector2(15f, 15f), 3);
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
            var panel = PopupShell(root, new Vector2(844f, 604f), out var dim, out var close, out var title);

            var contents = MakeImage(panel, "ContentsBox", Color.white, Sprite("IMG_ContentsBox") ?? RoundedSprite);
            Place(contents.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -38f), new Vector2(716f, 328f));
            var body = MakeText(contents.transform, "Body", "Body", 50, TextAnchor.MiddleCenter, new Color(0.2f, 0.12f, 0.45f));
            Stretch(body.rectTransform, 38f, 38f, 38f, 38f);

            var primary = MakeButton(panel, "PrimaryButton", "OK", Color.white, 56, out var primaryLabel);
            Place((RectTransform)primary.transform, new Vector2(1f, 0f), new Vector2(-218f, -6f), new Vector2(346f, 176f));
            var secondary = MakeButton(panel, "SecondaryButton", "Cancel", Color.white, 56, out var secondaryLabel);
            secondary.GetComponent<Image>().sprite = Sprite("BTN_CommonBlue") ?? secondary.GetComponent<Image>().sprite;
            Place((RectTransform)secondary.transform, new Vector2(0f, 0f), new Vector2(218f, -6f), new Vector2(346f, 176f));

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
            var panel = MakeRect(root.transform, "Panel");
            Stretch((RectTransform)root.transform);
            var dim = InvisibleButton(root.transform, "Dim", Dim);
            panel.SetAsLastSibling();
            Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 1500f));

            var itemRoot = MakeRect(panel, "Items");
            Place(itemRoot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440f, 700f));
            HorizontalLayout(itemRoot, 44f);

            var title = MakeText(panel, "Title", "Reward", 120, TextAnchor.MiddleCenter);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 644f), new Vector2(800f, 240f));

            var hint = MakeText(panel, "TapHint", "Tap to continue", 60, TextAnchor.MiddleCenter);
            Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 243f), new Vector2(900f, 100f));

            // 마지막 자식이라 패널 위에 깔립니다. 어디를 탭해도 닫힙니다.
            var tapAnywhere = InvisibleButton(root.transform, "TapAnywhere", new Color(0f, 0f, 0f, 0f));

            var popup = root.GetComponent<RewardPopup>();
            WirePopup(popup, panel, dim);
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
            Place(row, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(186f, 240f));
            label = MakeText(row, "Label", name, 48, TextAnchor.MiddleCenter);
            Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 28f), new Vector2(180f, 70f));
            var toggle = MakeToggle(row, "Toggle", new Vector2(186f, 186f), out var bg, out var check);
            bg.sprite = Sprite("IMG_SettingBGM") ?? bg.sprite;
            bg.color = Color.white;
            bg.type = UnityEngine.UI.Image.Type.Simple;
            check.sprite = Sprite("IMG_SettingOff") ?? check.sprite;
            check.color = Color.white;
            check.type = UnityEngine.UI.Image.Type.Simple;
            var frame = MakeImage(toggle.transform, "Frame", Color.white, Sprite("IMG_SettingFrame"));
            Stretch(frame.rectTransform);
            frame.transform.SetAsFirstSibling();
            Place((RectTransform)toggle.transform, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(186f, 186f));
            return toggle;
        }

        private static GameObject BuildOptionPopup()
        {
            var root = ViewRoot("OptionPopup", typeof(OptionPopup), UILayer.Popup);
            Stretch((RectTransform)root.transform);
            var dim = InvisibleButton(root.transform, "Dim", Dim);
            var bg = MakeImage(root.transform, "Background", new Color(0.2f, 0.12f, 0.45f), Sprite("IMG_Square"));
            Stretch(bg.rectTransform);
            bg.raycastTarget = false;
            var titlePanel = MakeImage(root.transform, "TitlePanel", Color.white, Sprite("IMG_TitlePanel"));
            Bar(titlePanel.rectTransform, 1f, 216f);
            var titleBox = MakeImage(titlePanel.transform, "TitleBox", Color.white, Sprite("IMG_TitleBox"));
            Place(titleBox.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(508f, 262f));
            var title = MakeText(titleBox.transform, "Title", "Settings", 88);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -158f), new Vector2(400f, 120f));
            var panel = MakeRect(root.transform, "Panel");
            Place(panel, new Vector2(0.5f, 1f), new Vector2(0f, -1016f), new Vector2(900f, 1600f));
            var closeImage = MakeImage(root.transform, "CloseButton", Color.white, Sprite("BTN_Close_Red") ?? RoundedSprite);
            var close = closeImage.gameObject.AddComponent<Button>();
            close.targetGraphic = closeImage;
            Place(closeImage.rectTransform, new Vector2(1f, 1f), new Vector2(-72f, -72f), new Vector2(120f, 120f));

            var settingBox = MakeImage(panel, "SettingBox", Color.white, Sprite("IMG_SettingBox_Lobby") ?? RoundedSprite);
            Place(settingBox.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 540f), new Vector2(752f, 340f));
            var sfx = OptionRow(settingBox.rectTransform, "Sound", -42f, out var sfxLabel);
            Place((RectTransform)sfx.transform.parent, new Vector2(0.5f, 0.5f), new Vector2(-228f, -42f), new Vector2(186f, 240f));
            var music = OptionRow(settingBox.rectTransform, "Music", -42f, out var musicLabel);
            Place((RectTransform)music.transform.parent, new Vector2(0.5f, 0.5f), new Vector2(0f, -42f), new Vector2(186f, 240f));
            var haptic = OptionRow(settingBox.rectTransform, "Vibration", -42f, out var hapticLabel);
            Place((RectTransform)haptic.transform.parent, new Vector2(0.5f, 0.5f), new Vector2(228f, -42f), new Vector2(186f, 240f));

            var language = MakeButton(panel, "LanguageButton", "Language", Color.white, 44, out var languageLabel);
            Place((RectTransform)language.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 32f), new Vector2(346f, 176f));
            var privacy = MakeButton(panel, "PrivacyButton", "Privacy Policy", Color.white, 38, out var privacyLabel);
            privacy.GetComponent<Image>().sprite = Sprite("BTN_Privacy") ?? privacy.GetComponent<Image>().sprite;
            Place((RectTransform)privacy.transform, new Vector2(0.5f, 0.5f), new Vector2(174f, -400f), new Vector2(322f, 152f));

            var version = MakeText(panel, "Version", "v0.0", 30, TextAnchor.MiddleCenter);
            Place(version.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -238f), new Vector2(300f, 60f));

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
            var panel = PopupShell(root, new Vector2(832f, 878f), out var dim, out var close, out var title);
            var group = panel.gameObject.AddComponent<ToggleGroup>();
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(420f, 120f));

            var codes = new[] { "en", "ko" };
            var toggles = new Toggle[codes.Length];
            var labels = new Text[codes.Length];
            for (var i = 0; i < codes.Length; i++)
            {
                toggles[i] = MakeToggle(panel, $"Toggle_{codes[i]}", new Vector2(350f, 160f));
                var toggleImage = toggles[i].targetGraphic as Image;
                if (toggleImage != null)
                {
                    toggleImage.sprite = Sprite("IMG_LanguageBox") ?? toggleImage.sprite;
                    toggleImage.color = Color.white;
                }
                if (toggles[i].graphic is Image check)
                {
                    check.sprite = Sprite("IMG_Check") ?? check.sprite;
                    check.color = Color.white;
                    Place(check.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-100f, 15f), new Vector2(100f, 84f));
                }
                var box = MakeImage(toggles[i].transform, "CheckBox", Color.white, Sprite("IMG_CheckBox"));
                Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-106f, 0f), new Vector2(88f, 88f));
                box.transform.SetAsFirstSibling();
                toggles[i].group = group;
                Place((RectTransform)toggles[i].transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f - 180f * i), new Vector2(350f, 160f));
                labels[i] = MakeText(toggles[i].transform, "Label", codes[i], 52, TextAnchor.MiddleCenter, new Color(0.2f, 0.12f, 0.45f));
                Place(labels[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(42f, 1f), new Vector2(200f, 80f));
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
            var panel = PopupShell(root, new Vector2(832f, 1326f), out var dim, out var close, out var title);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(420f, 130f));

            var badge = ProfileBadge(panel, "Badge", 160f);
            Place((RectTransform)badge.transform, new Vector2(0.5f, 0.5f), new Vector2(-258f, 410f), new Vector2(180f, 180f));

            var profileBox = MakeImage(panel, "ProfileBox", Color.white, Sprite("IMG_PopupBoxProfile") ?? RoundedSprite);
            Place(profileBox.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 410f), new Vector2(715f, 228f));
            profileBox.transform.SetAsFirstSibling();
            ((RectTransform)badge.transform).SetAsLastSibling();

            // 닉네임 입력
            var inputBg = MakeImage(profileBox.transform, "Nickname", Color.white, Sprite("IMG_ProfileNameBox") ?? RoundedSprite);
            Place(inputBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(84f, 0f), new Vector2(478f, 88f));
            var input = inputBg.gameObject.AddComponent<InputField>();
            var editBack = MakeImage(inputBg.transform, "EditBack", Color.white, Sprite("BTN_ProfileEditBack"));
            Place(editBack.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(189f, 1f), new Vector2(56f, 57f));
            var editIcon = MakeImage(editBack.transform, "EditIcon", Color.white, Sprite("BTN_ProfileEdit"));
            Place(editIcon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(50f, 50f));
            var inputText = MakeText(inputBg.transform, "Text", "", 54, TextAnchor.MiddleCenter, new Color(0.2f, 0.12f, 0.45f));
            Stretch(inputText.rectTransform, 36f, 88f, 8f, 8f);
            inputText.supportRichText = false;
            var placeholder = MakeText(inputBg.transform, "Placeholder", "Nickname", 54, TextAnchor.MiddleCenter, new Color(0.2f, 0.12f, 0.45f, 0.45f));
            Stretch(placeholder.rectTransform, 36f, 88f, 8f, 8f);
            input.textComponent = inputText;
            input.placeholder = placeholder;
            input.characterLimit = 12;

            var contents = MakeImage(panel, "ContentsBox", Color.white, Sprite("IMG_ProfileBox") ?? RoundedSprite);
            Place(contents.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -165f), new Vector2(715f, 878f));
            var line = MakeImage(contents.transform, "Line", Color.white, Sprite("IMG_PopupBoxBar_1"));
            Place(line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 238f), new Vector2(642f, 10f));

            // 탭
            var tabGroup = contents.gameObject.AddComponent<ToggleGroup>();
            var tabBack = MakeImage(contents.transform, "TabBack", Color.white, Sprite("BTN_Profileback") ?? RoundedSprite);
            Place(tabBack.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 326f), new Vector2(644f, 132f));
            var avatarTab = MakeToggle(tabBack.transform, "AvatarTab", new Vector2(314f, 118f));
            if (avatarTab.targetGraphic is Image avatarBg)
                avatarBg.sprite = Sprite("BTN_AvatarDown") ?? avatarBg.sprite;
            if (avatarTab.graphic is Image avatarCheck)
                avatarCheck.sprite = Sprite("BTN_Avatar") ?? avatarCheck.sprite;
            avatarTab.group = tabGroup;
            Place((RectTransform)avatarTab.transform, new Vector2(0.5f, 0.5f), new Vector2(-158f, 0f), new Vector2(314f, 118f));
            var avatarLabel = MakeText(avatarTab.transform, "Label", "Avatar", 58);
            Place(avatarLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 7f), new Vector2(296f, 98f));
            var frameTab = MakeToggle(tabBack.transform, "FrameTab", new Vector2(314f, 118f));
            if (frameTab.targetGraphic is Image frameBg)
                frameBg.sprite = Sprite("BTN_FrameDown") ?? frameBg.sprite;
            if (frameTab.graphic is Image frameCheck)
                frameCheck.sprite = Sprite("BTN_Frame") ?? frameCheck.sprite;
            frameTab.group = tabGroup;
            frameTab.isOn = false;
            Place((RectTransform)frameTab.transform, new Vector2(0.5f, 0.5f), new Vector2(158f, 0f), new Vector2(314f, 118f));
            var frameLabel = MakeText(frameTab.transform, "Label", "Frame", 58);
            Place(frameLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 7f), new Vector2(296f, 98f));

            // 선택 그리드
            var scroll = ScrollView(contents.transform, "ScrollView", out var content, 20f, new RectOffset(26, 26, 26, 26));
            Stretch((RectTransform)scroll.transform, 22f, 22f, 224f, 18f);
            Object.DestroyImmediate(content.GetComponent<VerticalLayoutGroup>());
            MakeGrid(content, new Vector2(180f, 180f), new Vector2(18f, 18f), 3).padding = new RectOffset(20, 20, 20, 20);

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

            var day = MakeText(bg.transform, "Day", $"Day {dayIndex + 1}", 26, TextAnchor.MiddleCenter, new Color(0.2f, 0.12f, 0.45f));
            Bar(day.rectTransform, 1f, 44f, 8f);

            var slotsRoot = MakeRect(bg.transform, "Slots");
            Place(slotsRoot, new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(210f, 112f));
            HorizontalLayout(slotsRoot, 8f);
            var slots = new GoodsItemView[2];
            for (var i = 0; i < slots.Length; i++)
            {
                slots[i] = GoodsItem(slotsRoot, $"Slot{i}", new Vector2(96f, 108f), 24);
                Layout((RectTransform)slots[i].transform, height: 108f, width: 96f);
            }

            var claim = MakeButton(bg.transform, "ClaimButton", "Claim", Color.white, 28, out var claimLabel);
            Place((RectTransform)claim.transform, new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(180f, 74f));

            var locked = MakeImage(bg.transform, "Locked", new Color(0f, 0f, 0f, 0.5f), RoundedSprite);
            Stretch(locked.rectTransform);
            var lockText = MakeText(locked.transform, "Text", "LOCKED", 24, TextAnchor.MiddleCenter, Color.white);
            Stretch(lockText.rectTransform);

            var received = MakeImage(bg.transform, "Received", new Color(0f, 0f, 0f, 0.5f), RoundedSprite);
            Stretch(received.rectTransform);
            var check = MakeImage(received.transform, "Check", Color.white, Sprite("IMG_Check") ?? CheckSprite);
            check.type = UnityEngine.UI.Image.Type.Simple;
            Place(check.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90f, 84f));

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
            var panel = PopupShell(root, new Vector2(832f, 1040f), out var dim, out var close, out var title);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(520f, 120f));

            var grid = MakeRect(panel, "Days");
            Place(grid, new Vector2(0.5f, 0.5f), new Vector2(0f, 82f), new Vector2(690f, 560f));
            MakeGrid(grid, new Vector2(210f, 170f), new Vector2(20f, 18f), 3);
            var cells = new DailyRewardCell[DailyRewardState.CycleDays];
            for (var i = 0; i < cells.Length; i++)
                cells[i] = DailyCell(grid, i);

            var free = MakeImage(panel, "FreeCoin", Color.white, Sprite("IMG_ContentsBox") ?? RoundedSprite);
            Place(free.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(716f, 178f));
            var freeTitle = MakeText(free.transform, "Title", "Free Coin", 30, TextAnchor.MiddleCenter, new Color(0.2f, 0.12f, 0.45f));
            Place(freeTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(240f, 50f));
            var freeItem = GoodsItem(free.transform, "Item", new Vector2(96f, 108f), 24);
            Place((RectTransform)freeItem.transform, new Vector2(0f, 0.5f), new Vector2(76f, -20f), new Vector2(96f, 108f));
            var freeClaim = MakeButton(free.transform, "ClaimButton", "Claim", Color.white, 30, out var freeClaimLabel);
            Place((RectTransform)freeClaim.transform, new Vector2(1f, 0.5f), new Vector2(-120f, -18f), new Vector2(200f, 82f));
            var freeWait = MakeText(free.transform, "Wait", "Next in 00:00", 28, TextAnchor.MiddleCenter, new Color(0.2f, 0.12f, 0.45f));
            Place(freeWait.rectTransform, new Vector2(1f, 0.5f), new Vector2(-146f, -18f), new Vector2(300f, 80f));

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
            var panel = PopupShell(root, new Vector2(844f, 724f), out var dim, out var close, out var title);

            var contents = MakeImage(panel, "ContentsBox", Color.white, Sprite("IMG_ContentsBox") ?? RoundedSprite);
            Place(contents.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -42f), new Vector2(716f, 448f));
            var light = MakeImage(contents.transform, "Light", Color.white, Sprite("IMG_PurchaseCompleteLight"));
            Place(light.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 39f), new Vector2(688f, 420f));
            light.raycastTarget = false;

            var heartMain = MakeImage(contents.transform, "Heart", Color.white, Sprite("IMG_MoreLive_Heart") ?? HeartSprite);
            heartMain.preserveAspect = true;
            Place(heartMain.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 85f), new Vector2(196f, 174f));

            var desc = MakeText(contents.transform, "Desc", "Refill all hearts with gold.", 54, TextAnchor.MiddleCenter, new Color(0.2f, 0.12f, 0.45f));
            Place(desc.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -28f), new Vector2(652f, 84f));

            var heartsRoot = MakeRect(contents.transform, "Hearts");
            Place(heartsRoot, new Vector2(0.5f, 0.5f), new Vector2(0f, -124f), new Vector2(560f, 90f));
            HorizontalLayout(heartsRoot, 16f);
            var hearts = new Image[PlayerWallet.HeartMax];
            for (var i = 0; i < hearts.Length; i++)
            {
                hearts[i] = MakeImage(heartsRoot, $"Heart{i}", Color.white, Sprite("IMG_MoreLive_Heart") ?? HeartSprite ?? CircleSprite);
                hearts[i].type = UnityEngine.UI.Image.Type.Simple;
                Layout(hearts[i].rectTransform, height: 80f, width: 80f);
            }

            var refillCount = MakeText(heartMain.transform, "RefillCount", "+0", 86);
            Stretch(refillCount.rectTransform);

            var buy = MakeButton(panel, "BuyButton", "Refill", Color.white, 56, out var buyLabel);
            Place((RectTransform)buy.transform, new Vector2(0.5f, 0f), new Vector2(0f, -8f), new Vector2(346f, 176f));
            var price = MakeText(panel, "Price", "0", 44, TextAnchor.MiddleCenter, new Color(1f, 0.82f, 0.2f));
            Place(price.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 118f), new Vector2(300f, 60f));

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
