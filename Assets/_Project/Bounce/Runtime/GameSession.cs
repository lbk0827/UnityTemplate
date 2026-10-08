using System;

namespace BK.Kit
{
    public enum SessionState { Lobby, Playing, Won, Lost }

    // No scene, UI or SDK dependencies. A game reports its result exactly once.
    public sealed class GameSession
    {
        public SessionState State { get; private set; } = SessionState.Lobby;
        public int Level { get; private set; } = 1;
        public event Action Changed;

        public void Start(int level)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
            Level = level;
            State = SessionState.Playing;
            Changed?.Invoke();
        }

        public bool Finish(bool won)
        {
            if (State != SessionState.Playing) return false;
            State = won ? SessionState.Won : SessionState.Lost;
            Changed?.Invoke();
            return true;
        }

        /// <summary>A bought continue brings a lost round back to play.</summary>
        public bool Resume()
        {
            if (State != SessionState.Lost) return false;
            State = SessionState.Playing;
            Changed?.Invoke();
            return true;
        }

        public void ReturnToLobby()
        {
            State = SessionState.Lobby;
            Changed?.Invoke();
        }
    }
}
