using System;
using OpenSage.Data.Ini;
using OpenSage.Gui.Wnd.Controls;
using OpenSage.Mathematics;

namespace OpenSage.Gui.Wnd.Transitions;

internal abstract class WindowTransitionOperation
{
    protected static readonly ColorRgbaF FlashColor = new ColorRgba(255, 187, 0, 255).ToColorRgbaF();

    public static WindowTransitionOperation Create(
        Window window,
        WindowTransitionWindow transitionWindow,
        TimeSpan currentTime)
    {
        var element = window.Controls.FindControl(transitionWindow.WinName);
        var startTime = currentTime + TimeSpan.FromSeconds(transitionWindow.FrameDelay / 30.0f);

        var logPath = "/tmp/opensage_transitions.log";
        var msg = $"[{DateTime.Now:HH:mm:ss.fff}] Creating {transitionWindow.Style} for '{transitionWindow.WinName}' (found: {element != null})";
        if (element != null)
        {
            msg += $" | Opacity={element.Opacity}, Visible={element.Visible}";
        }
        System.IO.File.AppendAllText(logPath, msg + "\n");

        if (element == null)
        {
            // If the target control isn't present, return a no-op transition
            // to avoid null-reference exceptions in specific transition types.
            return new NullTransition(element, startTime);
        }

        switch (transitionWindow.Style)
        {
            case WindowTransitionStyle.WinFade:
                return new WinFadeTransition(element, startTime);

            case WindowTransitionStyle.Flash:
                return new FlashTransition(element, startTime);

            case WindowTransitionStyle.ButtonFlash:
                return new ButtonFlashTransition(element, startTime);

            case WindowTransitionStyle.WinScaleUp:
                return new WinScaleUpTransition(element, startTime);

            case WindowTransitionStyle.ReverseSound:
                return new ReverseSoundTransition(element, startTime);

            case WindowTransitionStyle.MainMenuScaleUp:
            case WindowTransitionStyle.MainMenuMediumScaleUp: // TODO
                return new MainMenuScaleUpTransition(element, startTime);

            case WindowTransitionStyle.TypeText:
                return new TypeTextTransition(element, startTime);

            default:
                throw new NotImplementedException();
        }
    }

    protected Control Element { get; }

    protected abstract int FrameDuration { get; }

    public TimeSpan StartTime { get; }

    public TimeSpan Duration { get; }

    public TimeSpan EndTime { get; }

    protected WindowTransitionOperation(Control element, TimeSpan startTime)
    {
        Element = element;

        StartTime = startTime;

        Duration = TimeSpan.FromSeconds(FrameDuration / 30.0);

        EndTime = startTime + Duration;
    }

    // Null/no-op transition used when the target control is missing.
    private sealed class NullTransition : WindowTransitionOperation
    {
        protected override int FrameDuration => 1;

        public NullTransition(Control element, TimeSpan startTime)
            : base(element, startTime)
        {
        }

        protected override void OnUpdate(float progress)
        {
            // no-op
        }

        protected override void OnFinish()
        {
            // no-op
        }
    }

    public void Update(TimeSpan currentTime)
    {
        var relativeTime = currentTime - StartTime;

        var unclampedProgress = (float)(relativeTime.TotalSeconds / Duration.TotalSeconds);

        var progress = Math.Clamp(
            unclampedProgress,
            0,
            1);

        if (progress >= 0 && progress <= 1)
        {
            OnUpdate(progress);
        }
        else if (progress > 1)
        {
            OnFinish();
        }
    }

    protected abstract void OnUpdate(float progress);

    public void Finish()
    {
        OnFinish();
    }

    protected virtual void OnFinish() { }
}
