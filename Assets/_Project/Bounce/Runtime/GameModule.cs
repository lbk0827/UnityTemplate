using System;
using UnityEngine;

namespace BK.Kit
{
    // Implement this component in each game's Ingame scene. The kit owns navigation/save.
    public abstract class GameModule : MonoBehaviour
    {
        public abstract void Begin(int level, Action<bool> completed);
        public abstract void End();
    }
}
