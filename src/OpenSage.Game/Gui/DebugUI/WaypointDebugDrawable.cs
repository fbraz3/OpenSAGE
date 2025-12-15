using System;
using System.Collections.Generic;
using System.Numerics;
using OpenSage.Graphics.Cameras;
using OpenSage.Mathematics;
using OpenSage.Scripting;

namespace OpenSage.Gui.DebugUI;

/// <summary>
/// Debug drawable for rendering waypoints and waypoint connections.
/// Displays waypoint nodes as colored circles and paths as lines between connected waypoints.
/// </summary>
internal class WaypointDebugDrawable : IDebugDrawable
{
    private readonly WaypointCollection _waypoints;
    private readonly bool _showLabels;

    public float? Timer { get; set; }

    /// <summary>
    /// Creates a waypoint debug drawable.
    /// </summary>
    /// <param name="waypoints">The waypoint collection to render</param>
    /// <param name="showLabels">Whether to show waypoint labels and IDs</param>
    /// <param name="duration">How long to display in seconds. Null for permanent.</param>
    public WaypointDebugDrawable(WaypointCollection waypoints, bool showLabels = true, float? duration = null)
    {
        _waypoints = waypoints ?? throw new ArgumentNullException(nameof(waypoints));
        _showLabels = showLabels;
        Timer = duration;
    }

    public void Render(DrawingContext2D context, Camera camera)
    {
        if (_waypoints == null)
        {
            return;
        }

        // Draw waypoint paths (connections)
        DrawWaypointPaths(context, camera);

        // Draw waypoint nodes
        DrawWaypointNodes(context, camera);
    }

    private void DrawWaypointPaths(DrawingContext2D context, Camera camera)
    {
        // Track drawn paths to avoid duplicates
        var drawnPaths = new HashSet<(int, int)>();

        foreach (var waypoint in GetAllWaypoints())
        {
            foreach (var connectedWaypoint in waypoint.ConnectedWaypoints)
            {
                var pathKey = (Math.Min(waypoint.ID, connectedWaypoint.ID), Math.Max(waypoint.ID, connectedWaypoint.ID));

                if (drawnPaths.Contains(pathKey))
                {
                    continue;
                }

                drawnPaths.Add(pathKey);

                // Draw line from waypoint to connected waypoint
                DebugDrawingUtils.DrawLine(
                    context,
                    camera,
                    waypoint.Position,
                    connectedWaypoint.Position,
                    new ColorRgbaF(0.5f, 0.5f, 1.0f, 1.0f)); // Light blue
            }
        }
    }

    private void DrawWaypointNodes(DrawingContext2D context, Camera camera)
    {
        foreach (var waypoint in GetAllWaypoints())
        {
            var nodeColor = GetWaypointColor(waypoint.Name);

            // Draw waypoint node as a circle
            var screenRect = camera.WorldToScreenRectangle(waypoint.Position, new SizeF(8.0f));
            if (screenRect.HasValue)
            {
                context.DrawRectangle(screenRect.Value, nodeColor, 2);
            }

            // Draw labels if enabled
            if (_showLabels)
            {
                DrawWaypointLabel(context, camera, waypoint);
            }
        }
    }

    private void DrawWaypointLabel(DrawingContext2D context, Camera camera, Waypoint waypoint)
    {
        var screenPos = camera.WorldToScreenPoint(waypoint.Position);

        if (!camera.IsWithinViewportDepth(screenPos))
        {
            return;
        }

        // Label format: "Name (ID)"
        var label = $"{waypoint.Name} ({waypoint.ID})";
        var offset = new Vector2(10, 10);
        var labelPos = screenPos.Vector2XY() + offset;

        // For now, we just indicate the position - actual text rendering would need font support
        // Draw a small indicator line pointing to label location
        DebugDrawingUtils.DrawLine(
            context,
            camera,
            waypoint.Position,
            waypoint.Position + Vector3.UnitZ * 2,
            GetWaypointColor(waypoint.Name));
    }

    private static ColorRgbaF GetWaypointColor(string waypointName)
    {
        // Color waypoints based on type
        if (waypointName.Contains("Start", StringComparison.OrdinalIgnoreCase))
        {
            return new ColorRgbaF(0.0f, 1.0f, 0.0f, 1.0f); // Green for start points
        }

        if (waypointName.Contains("Rally", StringComparison.OrdinalIgnoreCase))
        {
            return new ColorRgbaF(1.0f, 1.0f, 0.0f, 1.0f); // Yellow for rally points
        }

        if (waypointName.Contains("Path", StringComparison.OrdinalIgnoreCase) ||
            waypointName.Contains("Route", StringComparison.OrdinalIgnoreCase))
        {
            return new ColorRgbaF(1.0f, 0.5f, 0.0f, 1.0f); // Orange for path waypoints
        }

        // Default: cyan for other waypoints
        return new ColorRgbaF(0.0f, 1.0f, 1.0f, 1.0f);
    }

    private System.Collections.Generic.IEnumerable<Waypoint> GetAllWaypoints()
    {
        return _waypoints.GetAllWaypoints();
    }
}
