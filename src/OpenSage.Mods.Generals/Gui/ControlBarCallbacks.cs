using System.IO;
using System.Linq;
using OpenSage.Gui;
using OpenSage.Gui.Wnd;
using OpenSage.Gui.Wnd.Controls;
using OpenSage.Logic.Orders;

namespace OpenSage.Mods.Generals.Gui;

[WndCallbacks]
public static class ControlBarCallbacks
{
    private const string GeneralsExpPointsWnd = "GeneralsExpPoints.wnd";

    public static void W3DCommandBarBackgroundDraw(Control control, DrawingContext2D drawingContext)
    {

    }

    public static void ControlBarSystem(Control control, WndWindowMessage message, ControlCallbackContext context)
    {
        switch (message.MessageType)
        {
            case WndWindowMessageType.SelectedButton:
                switch (message.Element.Name)
                {
                    case "ControlBar.wnd:ButtonLarge":
                        ((GeneralsControlBar)context.Game.Scene2D.ControlBar).ToggleSize();
                        break;
                    case "ControlBar.wnd:ButtonOptions":
                        context.WindowManager.PushWindow(Path.Combine("Menus", "QuitMenu.wnd"));
                        break;
                    case "ControlBar.wnd:ButtonGeneral":
                        if (context.WindowManager.TopWindow?.Name == GeneralsExpPointsWnd)
                        {
                            GeneralsExpPointsCallbacks.SetWindow(null);
                            context.WindowManager.PopWindow();
                        }
                        else
                        {
                            var window = context.WindowManager.PushWindow(GeneralsExpPointsWnd);
                            window.Name = GeneralsExpPointsWnd;
                            GeneralsExpPointsCallbacks.SetWindow(window);
                        }

                        break;
                }
                break;
        }
    }

    public static void ControlBarInput(Control control, WndWindowMessage message, ControlCallbackContext context)
    {
        // Forward control bar input events to the main control bar system
        ControlBarSystem(control, message, context);
    }

    public static void LeftHUDInput(Control control, WndWindowMessage message, ControlCallbackContext context)
    {
        if (message.MessageType != WndWindowMessageType.MouseDown
            && message.MessageType != WndWindowMessageType.MouseRightDown)
        {
            return;
        }

        var terrainPosition = context.Game.Scene3D.RadarDrawUtil.RadarToWorldSpace(
            message.MousePosition,
            control.ClientRectangle);

        // TODO: Fix left/right mouse handling
        // - If user has selected units, left mouse should move them and right mouse moves the camera
        // - If user has not selected units, both mouse buttons move the camera
        switch (message.MessageType)
        {
            case WndWindowMessageType.MouseDown: // Left mouse moves selected units
                var unit = context.Game.Scene3D.LocalPlayer.SelectedUnits.LastOrDefault();
                if (unit != null)
                {
                    // TODO: Duplicated in OrderGeneratorSystem
                    unit.OnLocalMove(context.Game.Audio);
                    var order = Order.CreateMoveOrder(context.Game.Scene3D.GetPlayerIndex(context.Game.Scene3D.LocalPlayer), terrainPosition);
                    context.Game.NetworkMessageBuffer.AddLocalOrder(order);
                }
                break;

            case WndWindowMessageType.MouseRightDown: // Right mouse moves camera.
                context.Game.Scene3D.TacticalView.LookAt(terrainPosition);
                break;
        }
    }

    public static void W3DLeftHUDDraw(Control control, DrawingContext2D drawingContext)
    {
        control.Window.Game.Scene3D.RadarDrawUtil.Draw(
            drawingContext,
            control.ClientRectangle);
    }

    public static void W3DGameWinDefaultDraw(Control control, DrawingContext2D drawingContext)
    {
        // Default game window draw - handled by rendering system
    }

    public static void BeaconWindowInput(Control control, WndWindowMessage message, ControlCallbackContext context)
    {
        // Beacon window input handling
    }

    public static void ControlBarObserverSystem(Control control, WndWindowMessage message, ControlCallbackContext context)
    {
        // Observer system for control bar state updates
        ControlBarSystem(control, message, context);
    }

    public static void GameWinDefaultSystem(Control control, WndWindowMessage message, ControlCallbackContext context)
    {
        // Default game window system
    }

    public static void W3DCommandBarGenExpDraw(Control control, DrawingContext2D drawingContext)
    {
        // General experience draw
    }

    public static void W3DRightHUDDraw(Control control, DrawingContext2D drawingContext)
    {
        // Right HUD draw
    }

    public static void GameWinDefaultInput(Control control, WndWindowMessage message, ControlCallbackContext context)
    {
        // Default game window input
    }

    public static void W3DCommandBarGridDraw(Control control, DrawingContext2D drawingContext)
    {
        // Command bar grid draw
    }

    public static void W3DGadgetPushButtonImageDraw(Control control, DrawingContext2D drawingContext)
    {
        // Button image draw
    }

    public static void W3DCommandBarForegroundDraw(Control control, DrawingContext2D drawingContext)
    {
        // Command bar foreground draw
    }

    public static void W3DPowerDraw(Control control, DrawingContext2D drawingContext)
    {
        // Power indicator draw
    }

    public static void W3DCommandBarTopDraw(Control control, DrawingContext2D drawingContext)
    {
        // Command bar top draw
    }

    public static void W3DCommandBarHelpPopupDraw(Control control, DrawingContext2D drawingContext)
    {
        // Help popup draw
    }
}
