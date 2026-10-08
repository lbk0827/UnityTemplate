using System.Collections.Generic;

namespace BK.Meta
{
    /// <summary>Carries rewards across a scene boundary so the arrival effect plays where the HUD is. In memory only.</summary>
    public sealed class PendingRewardQueue
    {
        private readonly Queue<PendingReward> _queue = new();

        public bool HasPending => _queue.Count > 0;
        public int Count => _queue.Count;

        public void Enqueue(PendingReward reward) => _queue.Enqueue(reward);

        public bool TryDequeue(out PendingReward reward)
        {
            if (_queue.Count == 0)
            {
                reward = default;
                return false;
            }
            reward = _queue.Dequeue();
            return true;
        }

        public void Clear() => _queue.Clear();
    }
}
