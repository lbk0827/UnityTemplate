using System;
using UnityEngine;
using BK.Data;

namespace Project
{
    /// <summary>Id 는 1 기반 일차(Day 1..7).</summary>
    [Serializable]
    public struct DailyRewardRow : ITableRow<int>
    {
        [SerializeField] private int _id;
        [SerializeField] private RewardItem[] _rewards;
        [SerializeField] private RewardItem _freeReward;

        public int Id => _id;
        public RewardItem[] Rewards => _rewards ?? Array.Empty<RewardItem>();
        public RewardItem FreeReward => _freeReward;

        public DailyRewardRow(int id, RewardItem[] rewards, RewardItem freeReward)
        {
            _id = id;
            _rewards = rewards;
            _freeReward = freeReward;
        }
    }

    [CreateAssetMenu(fileName = "DailyRewardTable", menuName = "Project/Daily Reward Table")]
    public sealed class DailyRewardTable : TableAsset<int, DailyRewardRow> { }
}
