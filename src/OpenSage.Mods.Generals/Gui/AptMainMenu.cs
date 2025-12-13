using OpenSage.Gui.Apt;
using OpenSage.Gui.Apt.ActionScript;

namespace OpenSage.Mods.Generals.Gui;

/// <summary>
/// APT callbacks for the main menu and shell UI in Command & Conquer Generals.
/// These provide fallback implementations for APT UI elements that may not be fully implemented.
/// </summary>
[AptCallbacks(SageGame.CncGenerals, SageGame.CncGeneralsZeroHour)]
public static class Global
{
    /// <summary>
    /// Called when the main menu APT is updated.
    /// </summary>
    public static void MainMenuUpdate(string param, ActionContext context, AptWindow window, IGame game)
    {
        // No-op implementation for shell menu compatibility
    }

    /// <summary>
    /// Called when the main menu APT is shutting down.
    /// </summary>
    public static void MainMenuShutdown(string param, ActionContext context, AptWindow window, IGame game)
    {
        // No-op implementation for shell menu compatibility
    }

    /// <summary>
    /// Called when the shell menu scheme should be drawn.
    /// </summary>
    public static void W3DShellMenuSchemeDraw(string param, ActionContext context, AptWindow window, IGame game)
    {
        // No-op implementation for shell menu compatibility
    }

    /// <summary>
    /// Called when the clock widget should be drawn.
    /// </summary>
    public static void W3DClockDraw(string param, ActionContext context, AptWindow window, IGame game)
    {
        // No-op implementation for shell menu compatibility
    }
}
