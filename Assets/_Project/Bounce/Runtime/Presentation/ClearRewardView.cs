using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BK.Kit
{
    // The reward has already been saved by KitApp.Complete. This view never grants currency.
    public sealed class ClearRewardView : MonoBehaviour
    {
        public bool HasStarted { get; private set; }
        public bool IsCollecting { get; private set; }
        private VisualBindings binding;
        private TMP_Text balance, rewardText;
        private Transform source, target;
        private Sprite coin;
        private int total, reward;
        private Action finished;

        public void Initialize(VisualBindings references, GamePresentation presentation, int savedTotal, int paidReward, Action onFinished)
        {
            binding = references; total = savedTotal; reward = paidReward; finished = onFinished;
            balance = binding.Get<TMP_Text>("goldAmountText");
            rewardText = binding.Get<TMP_Text>("rewardAmountText");
            source = binding.Get<Transform>("coinFlyStart");
            target = binding.Get<Transform>("coinFlyTarget");
            var currency = balance == null ? null : balance.GetComponentInParent<Button>(true);
            if (currency != null)
            {
                currency.gameObject.SetActive(true); currency.interactable = false;
                // This generic currency prefab originally received its sprites from a private service.
                // Reuse the already-authored Gold HUD skin rather than showing its empty white images.
                var gold = presentation.hud.GetComponentsInChildren<VisualBindings>(true)
                    .Where(b => b.role == "UIHUDSub_Top").Select(b => b.Get<VisualBindings>("gold")).FirstOrDefault(b => b != null);
                if (gold != null)
                {
                    var images = gold.GetComponentsInChildren<Image>(true);
                    foreach (var image in currency.GetComponentsInChildren<Image>(true))
                    {
                        var skin = images.FirstOrDefault(i => i.name == image.name && i.sprite != null);
                        if (skin == null) continue;
                        image.sprite = skin.sprite; image.type = skin.type; image.color = skin.color; image.material = skin.material;
                    }
                }
                var icon = currency.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.name == "sp_icon");
                coin = icon == null ? null : icon.sprite;
                foreach (var image in currency.GetComponentsInChildren<Image>(true))
                    if (image.name == "sp_plus") image.gameObject.SetActive(false);
            }
            if (balance != null) balance.text = (total - reward).ToString();
        }

        public void Collect()
        {
            if (HasStarted) return;
            HasStarted = true; IsCollecting = true;
            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                // The backdrop is also a Button in this prefab; disabling its tint would remove dimming.
                if (button != binding.Get<Button>("basic.claimButton") && button != binding.Get<Button>("closeButton"))
                    button.transition = Selectable.Transition.None;
                button.interactable = false;
            }
            StartCoroutine(Animate());
        }

        private IEnumerator Animate()
        {
            if (reward > 0 && source != null && target != null && coin != null)
            {
                var layer = new GameObject("Reward flight", typeof(RectTransform), typeof(Canvas));
                var rect = (RectTransform)layer.transform; rect.SetParent(transform, false);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                var overlay = layer.GetComponent<Canvas>(); overlay.overrideSorting = true;
                overlay.sortingOrder = GetComponentsInChildren<Canvas>(true).Max(c => c.sortingOrder) + 1;
                const int count = 8;
                var coins = new Image[count];
                for (int i = 0; i < count; i++)
                {
                    var image = new GameObject("Reward coin " + i, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    image.transform.SetParent(rect, false); image.sprite = coin; image.preserveAspect = true; image.raycastTarget = false;
                    image.rectTransform.sizeDelta = Vector2.one * 64; coins[i] = image;
                }
                float elapsed = 0;
                while (elapsed < .9f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    Vector3 start = rect.InverseTransformPoint(source.position), end = rect.InverseTransformPoint(target.position);
                    int arrived = 0;
                    for (int i = 0; i < count; i++)
                    {
                        float t = Mathf.Clamp01((elapsed - i * .045f) / .55f);
                        coins[i].gameObject.SetActive(t > 0 && t < 1);
                        if (t >= 1) arrived++;
                        var control = (start + end) * .5f + new Vector3((i - 3.5f) * 45, -100, 0);
                        float eased = t * t;
                        coins[i].rectTransform.localPosition = (1 - eased) * (1 - eased) * start
                            + 2 * (1 - eased) * eased * control + eased * eased * end;
                        coins[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(1.2f, .65f, t);
                    }
                    int shown = (int)((long)reward * arrived / count);
                    if (balance != null) balance.text = (total - reward + shown).ToString();
                    if (rewardText != null) rewardText.text = (reward - shown).ToString();
                    yield return null;
                }
                Destroy(layer);
            }
            if (balance != null) balance.text = total.ToString();
            if (rewardText != null) rewardText.text = "0";
            yield return new WaitForSecondsRealtime(.12f);
            IsCollecting = false;
            var callback = finished; finished = null; callback?.Invoke();
        }
    }
}
