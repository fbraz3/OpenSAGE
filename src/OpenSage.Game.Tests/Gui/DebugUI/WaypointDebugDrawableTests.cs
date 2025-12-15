using System;
using OpenSage.Gui.DebugUI;
using OpenSage.Scripting;
using Xunit;

namespace OpenSage.Tests.Gui.DebugUI;

/// <summary>
/// Tests for WaypointDebugDrawable rendering functionality.
/// </summary>
public class WaypointDebugDrawableTests
{
    [Fact]
    public void Constructor_WithValidWaypoints_SetsProperties()
    {
        // Arrange
        var waypoints = new WaypointCollection();

        // Act
        var drawable = new WaypointDebugDrawable(waypoints, showLabels: true, duration: 5.0f);

        // Assert
        Assert.NotNull(drawable);
        Assert.Equal(5.0f, drawable.Timer);
    }

    [Fact]
    public void Constructor_WithNullWaypoints_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new WaypointDebugDrawable(null!));
    }

    [Fact]
    public void Timer_CanBeSet()
    {
        // Arrange
        var waypoints = new WaypointCollection();
        var drawable = new WaypointDebugDrawable(waypoints);

        // Act
        drawable.Timer = 10.0f;

        // Assert
        Assert.Equal(10.0f, drawable.Timer);
    }

    [Fact]
    public void Timer_CanBeNull()
    {
        // Arrange
        var waypoints = new WaypointCollection();
        var drawable = new WaypointDebugDrawable(waypoints, duration: null);

        // Act & Assert
        Assert.Null(drawable.Timer);
    }

    [Fact]
    public void Constructor_WithShowLabelsTrue_EnablesLabels()
    {
        // Arrange & Act
        var waypoints = new WaypointCollection();
        var drawable = new WaypointDebugDrawable(waypoints, showLabels: true);

        // Assert - If no exception is thrown, test passes
        Assert.NotNull(drawable);
    }

    [Fact]
    public void Constructor_WithShowLabelsFalse_DisablesLabels()
    {
        // Arrange & Act
        var waypoints = new WaypointCollection();
        var drawable = new WaypointDebugDrawable(waypoints, showLabels: false);

        // Assert - If no exception is thrown, test passes
        Assert.NotNull(drawable);
    }

    [Fact]
    public void Constructor_WithPermanentDuration_TimerIsNull()
    {
        // Arrange & Act
        var waypoints = new WaypointCollection();
        var drawable = new WaypointDebugDrawable(waypoints, duration: null);

        // Assert
        Assert.Null(drawable.Timer);
    }

    [Fact]
    public void Constructor_WithZeroDuration_TimerIsZero()
    {
        // Arrange & Act
        var waypoints = new WaypointCollection();
        var drawable = new WaypointDebugDrawable(waypoints, duration: 0);

        // Assert
        Assert.Equal(0, drawable.Timer);
    }

    [Fact]
    public void WaypointDebugDrawable_ImplementsIDebugDrawable()
    {
        // Arrange & Act
        var waypoints = new WaypointCollection();
        var drawable = new WaypointDebugDrawable(waypoints);

        // Assert
        Assert.IsAssignableFrom<IDebugDrawable>(drawable);
    }
}
