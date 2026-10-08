using System;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;
using BK.Scene;

namespace BK.UI
{
    /// <summary>
    /// The only place that reads the back key. Escape on desktop and the Android back
    /// button (which Unity reports as Escape) both route to
    /// <see cref="IUIService.HandleBackRequest"/>, topmost layer first. Nothing fires
    /// while a scene transition is in flight or while <see cref="Suspended"/> is set.
    /// </summary>
    public sealed class BackInputDriver : ITickable
    {
        private readonly IUIService _ui;
        private readonly ISceneService _scenes;
        private readonly Func<bool> _wasPressedThisFrame;

        /// <summary>Game code sets this during its own modal flows (e.g. a transition it drives itself).</summary>
        public bool Suspended { get; set; }

        [Inject]
        public BackInputDriver(IUIService ui, ISceneService scenes)
            : this(ui, scenes, ReadKeyboard) { }

        public BackInputDriver(IUIService ui, ISceneService scenes, Func<bool> wasPressedThisFrame)
        {
            _ui = ui;
            _scenes = scenes;
            _wasPressedThisFrame = wasPressedThisFrame;
        }

        public void Tick()
        {
            if (Suspended || _scenes.IsTransitioning || !_wasPressedThisFrame())
                return;
            _ui.HandleBackRequest();
        }

        private static bool ReadKeyboard()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
        }
    }
}
