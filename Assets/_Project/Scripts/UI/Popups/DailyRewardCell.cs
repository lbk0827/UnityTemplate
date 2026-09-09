using System;
using UnityEngine;
using UnityEngine.UI;

namespace Project
{
    public enum DailyRewardCellState
    {
        Locked,
        Claimable,
        Received,
    }

    /// <summary>출석 보상 하루치 셀. 상태에 따라 잠김/받기/완료 표시.</summary>
    public sealed class DailyRewardCell : MonoBehaviour
    {
        [SerializeField] private Text _dayText;
        [SerializeField] private GoodsItemView[] _slots;
        [SerializeField] private Button _claimButton;
        [SerializeField] private Text _claimLabel;
        [SerializeField] private GameObject _lockedOverlay;
        [SerializeField] private GameObject _receivedOverlay;
        [SerializeField] private Image _background;

        public int DayIndex { get; private set; }
        public event Action<int> ClaimClicked;

        public void Bind(int dayIndex, DailyRewardRow row, string dayLabel)
        {
            DayIndex = dayIndex;
            _dayText.text = dayLabel;

            var rewards = row.Rewards;
            for (var i = 0; i < _slots.Length; i++)
            {
                var has = i < rewards.Length && rewards[i].IsValid;
                _slots[i].gameObject.SetActive(has);
                if (has)
                    _slots[i].Bind(rewards[i], "x{0}");
            }

            _claimButton.onClick.RemoveAllListeners();
            _claimButton.onClick.AddListener(() => ClaimClicked?.Invoke(DayIndex));
        }

        public void SetState(DailyRewardCellState state, string claimLabel)
        {
            _claimButton.gameObject.SetActive(state == DailyRewardCellState.Claimable);
            _claimLabel.text = claimLabel;
            _lockedOverlay.SetActive(state == DailyRewardCellState.Locked);
            _receivedOverlay.SetActive(state == DailyRewardCellState.Received);
            _background.color = state == DailyRewardCellState.Claimable
                ? new Color(0.35f, 0.5f, 0.3f)
                : new Color(0.22f, 0.22f, 0.28f);
        }
    }
}
