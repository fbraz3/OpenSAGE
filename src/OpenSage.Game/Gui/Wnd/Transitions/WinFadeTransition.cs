using System;
using OpenSage.Gui.Wnd.Controls;
using OpenSage.Mathematics;

namespace OpenSage.Gui.Wnd.Transitions;

internal sealed class WinFadeTransition : WindowTransitionOperation
{
    private readonly float _startOpacity;
    private readonly float _endOpacity;

    protected override int FrameDuration => 12;

    public WinFadeTransition(Control element, TimeSpan startTime)
        : base(element, startTime)
    {
        _startOpacity = element.Opacity;
        _endOpacity = element.Opacity == 1 ? 0 : 1;
        var logPath = "/tmp/opensage_transitions.log";
        System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] WinFadeTransition: {element?.Name} | Start={_startOpacity} End={_endOpacity} ({(_endOpacity == 1 ? "FADE-IN" : "FADE-OUT")})\n");
    }

    protected override void OnUpdate(float progress)
    {
        Element.Opacity = MathUtility.Lerp(
            _startOpacity,
            _endOpacity,
            progress);
    }
}
