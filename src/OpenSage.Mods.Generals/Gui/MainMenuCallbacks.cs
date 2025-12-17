using System;
using System.Linq;
using OpenSage.Content.Translation;
using OpenSage.Gui;
using OpenSage.Gui.Wnd;
using OpenSage.Gui.Wnd.Controls;
using OpenSage.Network;

namespace OpenSage.Mods.Generals.Gui;

[WndCallbacks]
public static class MainMenuCallbacks
{
    private static bool DoneMainMenuFadeIn;

    private static string CurrentSide;
    private static string CurrentSideWindowSuffix;

    public static void W3DMainMenuInit(Window window, IGame game)
    {
        if (!game.Configuration.LoadShellMap)
        {
            // Draw the main menu background if no map is loaded.
            window.Root.DrawCallback = window.Root.DefaultDraw;
        }

        // We'll show these later via window transitions.
        window.Controls.FindControl("MainMenu.wnd:MainMenuRuler").Hide();
        window.Controls.FindControl("MainMenu.wnd:MainMenuRuler").Opacity = 0;

        var initiallyHiddenSections = new[]
        {
            "MainMenu.wnd:MapBorder",
            "MainMenu.wnd:MapBorder1",
            "MainMenu.wnd:MapBorder2",
            "MainMenu.wnd:MapBorder3",
            "MainMenu.wnd:MapBorder4"
        };

        foreach (var controlName in initiallyHiddenSections)
        {
            var control = window.Controls.FindControl(controlName);
            control.Opacity = 0;

            foreach (var button in control.Controls.First().Controls.ToList())
            {
                button.Opacity = 0;
                button.TextOpacity = 0;
            }
        }

        window.Controls.FindControl("MainMenu.wnd:ButtonUSARecentSave").Hide();
        window.Controls.FindControl("MainMenu.wnd:ButtonUSALoadGame").Hide();

        window.Controls.FindControl("MainMenu.wnd:ButtonGLARecentSave").Hide();
        window.Controls.FindControl("MainMenu.wnd:ButtonGLALoadGame").Hide();

        window.Controls.FindControl("MainMenu.wnd:ButtonChinaRecentSave").Hide();
        window.Controls.FindControl("MainMenu.wnd:ButtonChinaLoadGame").Hide();

        // TODO: Show faction icons when WinScaleUpTransition is implemented.

        DoneMainMenuFadeIn = false;
    }

    public static void MainMenuSystem(Control control, WndWindowMessage message, ControlCallbackContext context)
    {
        void QueueTransition(string transition)
        {
            context.WindowManager.TransitionManager.QueueTransition(null, control.Window, transition);
        }

        void OpenSinglePlayerSideMenu(string side, string sideWindowSuffix)
        {
            CurrentSide = side;
            CurrentSideWindowSuffix = sideWindowSuffix;

            var selectDifficultyLabel = (Label)control.Window.Controls.FindControl("MainMenu.wnd:StaticTextSelectDifficulty");
            // TODO: This should be animated as part of the transition.
            selectDifficultyLabel.Opacity = 1;
            selectDifficultyLabel.TextOpacity = 1;
            selectDifficultyLabel.Show();

            QueueTransition("MainMenuSinglePlayerMenuBack");
            QueueTransition($"MainMenuDifficultyMenu{sideWindowSuffix}");
        }

        var translation = context.Game.ContentManager.TranslationManager;

        switch (message.MessageType)
        {
            case WndWindowMessageType.SelectedButton:
                switch (message.Element.Name)
                {
                    case "MainMenu.wnd:ButtonSinglePlayer":
                        QueueTransition("MainMenuDefaultMenuBack");
                        QueueTransition("MainMenuSinglePlayerMenu");
                        break;

                    case "MainMenu.wnd:ButtonTRAINING":
                        OpenSinglePlayerSideMenu("TRAINING", "Training");
                        break;

                    case "MainMenu.wnd:ButtonChina":
                        OpenSinglePlayerSideMenu("China", "China");
                        break;

                    case "MainMenu.wnd:ButtonGLA":
                        OpenSinglePlayerSideMenu("GLA", "GLA");
                        break;

                    case "MainMenu.wnd:ButtonUSA":
                        OpenSinglePlayerSideMenu("USA", "US");
                        break;

                    case "MainMenu.wnd:ButtonEasy":
                    case "MainMenu.wnd:ButtonMedium":
                    case "MainMenu.wnd:ButtonHard":
                        context.Game.StartCampaign(CurrentSide);
                        break;

                    case "MainMenu.wnd:ButtonDiffBack":
                        QueueTransition($"MainMenuDifficultyMenu{CurrentSideWindowSuffix}Back");
                        QueueTransition($"MainMenuSinglePlayer{CurrentSideWindowSuffix}MenuFromDiff");
                        break;

                    case "MainMenu.wnd:ButtonSkirmish":
                        context.Game.SkirmishManager = new LocalSkirmishManager(context.Game);
                        context.WindowManager.SetWindow(@"Menus\SkirmishGameOptionsMenu.wnd");
                        break;

                    case "MainMenu.wnd:ButtonSingleBack":
                        QueueTransition("MainMenuSinglePlayerMenuBack");
                        QueueTransition("MainMenuDefaultMenu");
                        break;

                    case "MainMenu.wnd:ButtonMultiplayer":
                        QueueTransition("MainMenuDefaultMenuBack");
                        QueueTransition("MainMenuMultiPlayerMenu");
                        break;

                    case "MainMenu.wnd:ButtonNetwork":
                        context.WindowManager.SetWindow(@"Menus\LanLobbyMenu.wnd");
                        break;

                    case "MainMenu.wnd:ButtonOnline":
                        // this should load another window, but we don't have a lobby server yet,
                        // so we just show the "Direct Connect" window

                        context.WindowManager.SetWindow(@"Menus\NetworkDirectConnect.wnd", NetworkUtils.OnlineTag);
                        break;

                    case "MainMenu.wnd:ButtonMultiBack":
                        QueueTransition("MainMenuMultiPlayerMenuReverse");
                        QueueTransition("MainMenuDefaultMenu");
                        break;

                    case "MainMenu.wnd:ButtonLoadReplay":
                        QueueTransition("MainMenuDefaultMenuBack");
                        QueueTransition("MainMenuLoadReplayMenu");
                        break;

                    case "MainMenu.wnd:ButtonLoadReplayBack":
                        QueueTransition("MainMenuLoadReplayMenuBack");
                        QueueTransition("MainMenuDefaultMenu");
                        break;

                    case "MainMenu.wnd:ButtonReplay":
                        context.WindowManager.SetWindow(@"Menus\ReplayMenu.wnd");
                        break;

                    case "MainMenu.wnd:ButtonLoadGame":
                        context.WindowManager.SetWindow(@"Menus\SaveLoad.wnd");
                        break;

                    case "MainMenu.wnd:ButtonOptions":
                        context.WindowManager.PushWindow(@"Menus\OptionsMenu.wnd");
                        break;

                    case "MainMenu.wnd:ButtonCredits":
                        context.WindowManager.SetWindow(@"Menus\CreditsMenu.wnd");
                        break;

                    case "MainMenu.wnd:ButtonExit":
                        var exitWindow = context.WindowManager.PushWindow(@"Menus\QuitMessageBox.wnd");
                        exitWindow.Controls.FindControl("QuitMessageBox.wnd:StaticTextTitle").Text = "GUI:QuitPopupTitle".Translate();
                        ((Label)exitWindow.Controls.FindControl("QuitMessageBox.wnd:StaticTextTitle")).TextAlignment = TextAlignment.Leading;
                        exitWindow.Controls.FindControl("QuitMessageBox.wnd:StaticTextMessage").Text = "GUI:QuitPopupMessage".Translate();
                        exitWindow.Controls.FindControl("QuitMessageBox.wnd:ButtonOk").Show();
                        exitWindow.Controls.FindControl("QuitMessageBox.wnd:ButtonOk").Text = "GUI:Yes".Translate();
                        exitWindow.Controls.FindControl("QuitMessageBox.wnd:ButtonCancel").Show();
                        exitWindow.Controls.FindControl("QuitMessageBox.wnd:ButtonCancel").Text = "GUI:No".Translate();
                        break;
                }
                break;
        }
    }

    public static void MainMenuInput(Control control, WndWindowMessage message, ControlCallbackContext context)
    {
        // Any input at all (mouse, keyboard) will trigger the main menu fade-in.
        if (!DoneMainMenuFadeIn)
        {
            var logPath = "/tmp/opensage_transitions.log";
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] MainMenuInput: Starting fade-in\n");
            
            // Prepare all menu elements
            var ruler = control.Window.Controls.FindControl("MainMenu.wnd:MainMenuRuler");
            var borderSections = new[]
            {
                "MainMenu.wnd:MapBorder",
                "MainMenu.wnd:MapBorder1",
                "MainMenu.wnd:MapBorder2",
                "MainMenu.wnd:MapBorder3",
                "MainMenu.wnd:MapBorder4"
            };

            // IMPORTANT: Show elements FIRST before transitions
            // Elements must be visible before fade-in transitions can work
            if (ruler != null)
            {
                ruler.Show();
                ruler.Opacity = 0;  // Set opacity to 0 for fade-in animation
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] MainMenuRuler: Visible={ruler.Visible}, Opacity={ruler.Opacity}\n");
            }

            foreach (var borderName in borderSections)
            {
                var border = control.Window.Controls.FindControl(borderName);
                if (border != null)
                {
                    border.Show();
                    border.Opacity = 0;  // Set opacity to 0 for fade-in animation
                    System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {borderName}: Visible={border.Visible}, Opacity={border.Opacity}\n");
                }
            }

            // Queue transitions: fade-in title, then show menu items
            // Both transitions are needed: MainMenuFade (immediate) fades background,
            // MainMenuDefaultMenu queues after it to fade in menu buttons/elements
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] Queueing MainMenuFade transition\n");
            context.WindowManager.TransitionManager.QueueTransition(null, control.Window, "MainMenuFade");
            
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] Queueing MainMenuDefaultMenu transition\n");
            context.WindowManager.TransitionManager.QueueTransition(null, control.Window, "MainMenuDefaultMenu");
            
            DoneMainMenuFadeIn = true;
        }
    }

    public static void MainMenuUpdate(Window window, IGame game)
    {
        // Called periodically to update main menu state
        // No special update logic needed currently
    }

    public static void MainMenuShutdown(Window window, IGame game)
    {
        // Called when main menu is shutting down
        DoneMainMenuFadeIn = false;
        CurrentSide = null;
        CurrentSideWindowSuffix = null;
    }
}
