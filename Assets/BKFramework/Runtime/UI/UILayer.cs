namespace BK.UI
{
    /// <summary>
    /// Draw/interaction bands. Each layer owns its own canvas and its own view
    /// stack, so a popup opening never disturbs HUD state.
    /// Values are spaced to leave room for project-specific layers in between.
    /// </summary>
    public enum UILayer
    {
        Background = 0,
        Content = 100,
        Hud = 200,
        Popup = 300,
        Overlay = 400,
        System = 500,
    }
}
