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

        // Calculate tooltip position with edge wrapping
        var tooltipPos = CalculateTooltipPosition(tooltipText, viewportSize);
        
        // Draw tooltip background
        DrawTooltipBackground(drawingContext, tooltipPos, tooltipText);
        
        // Draw tooltip text
        DrawTooltipText(drawingContext, tooltipPos, tooltipText);
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
        // TODO: Get per-control delay from WND definition
        // For now, use default
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
            // For now, return empty - callback will be implemented in Control system
            return null;
        }

        // Fall back to static text from WND definition
        // TODO: Get from WND control definition
        return null;
    }

    private Point2D CalculateTooltipPosition(string tooltipText, Size viewportSize)
    {
        // Start with offset from mouse
        var x = _lastMousePosition.X + TooltipOffsetX;
        var y = _lastMousePosition.Y + TooltipOffsetY;

        // TODO: Calculate text bounds to get tooltip dimensions
        var tooltipWidth = 200; // Placeholder
        var tooltipHeight = 40; // Placeholder

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

    private void DrawTooltipBackground(DrawingContext2D drawingContext, Point2D position, string tooltipText)
    {
        // TODO: Draw tooltip background rectangle with border
        // Based on EA Generals: semi-transparent background, thin border
    }

    private void DrawTooltipText(DrawingContext2D drawingContext, Point2D position, string tooltipText)
    {
        // TODO: Draw tooltip text
        // Positioned within tooltip background with padding
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
