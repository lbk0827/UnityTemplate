using System;
using UnityEngine;

namespace BK.Kit
{
    // Implement this component in each game's Ingame scene. The kit owns navigation/save.
    public abstract class GameModule : MonoBehaviour
    {
        public abstract void Begin(int level, Action<bool> completed);
        public abstract void End();
        /// <summary>True while a lost round can still be resumed (board intact).</summary>
        public abstract bool CanContinue { get; }
        /// <summary>Resumes a lost round with extra moves/balls. Only valid when CanContinue.</summary>
        public abstract void Continue(int extraBalls);
    }
}
