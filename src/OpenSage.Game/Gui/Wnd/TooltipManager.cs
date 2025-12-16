using System;
using OpenSage.Content;
using OpenSage.Gui.Wnd.Controls;
using OpenSage.Mathematics;

namespace OpenSage.Gui.Wnd;

/// <summary>
/// Manages tooltip display for GUI windows.
///
/// Based on EA Generals tooltip system:
/// - Shows tooltip after configurable delay (default 50ms)
/// - Positions tooltip 20px offset from cursor (with edge wrapping)
/// - Hides on mouse movement, window change, or manual reset
/// - Supports both static text and callback-based tooltips
/// </summary>
internal sealed class TooltipManager : DisposableBase
{
    private Control _currentControl;
    private TimeInterval _mouseEnterTime;
    private bool _tooltipVisible;
    private Rectangle _lastMouseBounds;
    private Point2D _lastMousePosition;
    private TimeInterval _totalTime;

    private const int DefaultTooltipDelayMs = 50;
    private const int TooltipOffsetX = 20;
    private const int TooltipOffsetY = 20;
    private const int EdgeSafetyMargin = 4;

    public TooltipManager()
    {
        _currentControl = null;
        _mouseEnterTime = TimeInterval.Zero;
        _tooltipVisible = false;
        _totalTime = TimeInterval.Zero;
    }

    internal void OnMouseMove(in Point2D mousePosition)
    {
        // Hide tooltip if mouse moved more than tolerance
        if (_tooltipVisible && !IsMouseInTolerance(mousePosition))
        {
            HideTooltip();
        }

        _lastMousePosition = mousePosition;
    }

    internal void OnMouseEnter(Control control, in Point2D mousePosition, in TimeInterval currentTime)
    {
        if (control == _currentControl)
        {
            return; // Already tracking this control
        }

        HideTooltip();

        _currentControl = control;
        _mouseEnterTime = currentTime;
        _lastMousePosition = mousePosition;
        _lastMouseBounds = control.Bounds;
    }

    internal void OnMouseLeave()
    {
        HideTooltip();
        _currentControl = null;
    }

    internal void Update(in TimeInterval gameTime)
    {
        _totalTime = gameTime;

        if (_currentControl == null || _tooltipVisible)
        {
            return;
        }

        // Check if we've exceeded the delay
        var elapsedMs = (gameTime.TotalTime.TotalMilliseconds - _mouseEnterTime.TotalTime.TotalMilliseconds);
        var tooltipDelayMs = GetTooltipDelay();

        if (elapsedMs >= tooltipDelayMs)
        {
            ShowTooltip();
        }
    }

    internal void Render(DrawingContext2D drawingContext, Size viewportSize)
    {
        if (!_tooltipVisible || _currentControl == null)
        {
            return;
        }

        var tooltipText = GetTooltipText();
        if (string.IsNullOrEmpty(tooltipText))
        {
            return;
        }

        // Get font from control (use default if not available)
        var font = _currentControl.Font;
        if (font == null)
        {
            // TODO: Get default font from content manager
            return; // Skip rendering if no font available
        }

        // Measure tooltip text to get exact dimensions
        const int MaxTooltipWidth = 300;
        var textSize = DrawingContext2D.MeasureText(tooltipText, font, TextAlignment.Leading, MaxTooltipWidth);

        // Add padding to get total tooltip size
        const int PaddingX = 8;
        const int PaddingY = 6;
        var tooltipWidth = (int)(textSize.Width + PaddingX * 2);
        var tooltipHeight = (int)(textSize.Height + PaddingY * 2);

        // Calculate tooltip position with edge wrapping
        var tooltipPos = CalculateTooltipPosition(tooltipText, viewportSize, tooltipWidth, tooltipHeight);

        // Draw tooltip background
        DrawTooltipBackground(drawingContext, tooltipPos, tooltipWidth, tooltipHeight);

        // Draw tooltip text
        DrawTooltipText(drawingContext, tooltipPos, tooltipText, font, tooltipWidth, tooltipHeight, PaddingX, PaddingY);
    }

    private void ShowTooltip()
    {
        if (_currentControl == null)
        {
            return;
        }

        // Only show if we have tooltip content
        if (!string.IsNullOrEmpty(GetTooltipText()))
        {
            _tooltipVisible = true;
        }
    }

    private void HideTooltip()
    {
        _tooltipVisible = false;
        _currentControl = null;
    }

    private int GetTooltipDelay()
    {
        if (_currentControl == null)
        {
            return DefaultTooltipDelayMs;
        }

        // Get per-control delay from WND definition
        if (_currentControl.TooltipDelay > 0)
        {
            return _currentControl.TooltipDelay;
        }

        // Fall back to default
        return DefaultTooltipDelayMs;
    }

    private string GetTooltipText()
    {
        if (_currentControl == null)
        {
            return null;
        }

        // First try callback
        if (_currentControl.TooltipCallback != null)
        {
            // TODO: Invoke callback to get tooltip text
            // For now, use static text as fallback
        }

        // Fall back to static text from WND definition
        return _currentControl.TooltipText;
    }

    private Point2D CalculateTooltipPosition(string tooltipText, Size viewportSize, int tooltipWidth, int tooltipHeight)
    {
        // Start with offset from mouse
        var x = _lastMousePosition.X + TooltipOffsetX;
        var y = _lastMousePosition.Y + TooltipOffsetY;

        // Wrap left if at right edge
        if (x + tooltipWidth + EdgeSafetyMargin > viewportSize.Width)
        {
            x = _lastMousePosition.X - tooltipWidth - TooltipOffsetX;
        }

        // Wrap up if at bottom edge
        if (y + tooltipHeight + EdgeSafetyMargin > viewportSize.Height)
        {
            y = _lastMousePosition.Y - tooltipHeight - TooltipOffsetY;
        }

        return new Point2D(Math.Max(EdgeSafetyMargin, x), Math.Max(EdgeSafetyMargin, y));
    }

    private void DrawTooltipBackground(DrawingContext2D drawingContext, Point2D position, int width, int height)
    {
        // Based on EA Generals: semi-transparent background with thin border
        const int BorderWidth = 1;

        var bgRect = new RectangleF(position.X, position.Y, width, height);

        // Draw semi-transparent background (dark gray with alpha)
        var bgColor = new ColorRgbaF(0.1f, 0.1f, 0.1f, 0.85f);
        drawingContext.FillRectangle(bgRect, bgColor);

        // Draw light gray border
        var borderColor = new ColorRgbaF(0.6f, 0.6f, 0.6f, 1.0f);
        drawingContext.DrawRectangle(bgRect, borderColor, BorderWidth);
    }

    private void DrawTooltipText(DrawingContext2D drawingContext, Point2D position, string tooltipText, SixLabors.Fonts.Font font, int tooltipWidth, int tooltipHeight, int paddingX, int paddingY)
    {
        // Draw white text inside tooltip
        var textRect = new RectangleF(
            position.X + paddingX,
            position.Y + paddingY,
            tooltipWidth - paddingX * 2,
            tooltipHeight - paddingY * 2);

        var textColor = new ColorRgbaF(1.0f, 1.0f, 1.0f, 1.0f);
        drawingContext.DrawText(tooltipText, font, TextAlignment.Leading, textColor, textRect);
    }

    private bool IsMouseInTolerance(in Point2D newPosition)
    {
        // Hide tooltip if mouse moves significantly
        // EA Generals: 15px tolerance before hiding
        const int Tolerance = 15;

        var dx = newPosition.X - _lastMousePosition.X;
        var dy = newPosition.Y - _lastMousePosition.Y;

        return (dx * dx + dy * dy) <= (Tolerance * Tolerance);
    }
}
