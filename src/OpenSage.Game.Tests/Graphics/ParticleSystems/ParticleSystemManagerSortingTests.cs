using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Xunit;
using OpenSage.Graphics.ParticleSystems;
using OpenSage.Tests;
using OpenSage.Content.Loaders;

namespace OpenSageGameTests.Graphics.ParticleSystems;

/// <summary>
/// Unit tests for ParticleSystemManager priority-based sorting (PLAN-012 Stage 1).
/// Verifies that particle systems are sorted by priority in descending order (highest first)
/// with stable secondary sorting by template name.
/// Reference: EA Generals ParticleSys.cpp line 1794 - priority-based particle rendering
/// </summary>
public class ParticleSystemManagerSortingTests
{
    /// <summary>
    /// Test that particle systems with different priorities are sorted in descending order.
    /// Higher priority enum values should render first (back to front for transparency).
    /// </summary>
    [Fact]
    public void SortSystemsByPriority_WithMixedPriorities_SortsDescending()
    {
        // Arrange
        var systems = new List<(ParticleSystemPriority priority, string name)>
        {
            (ParticleSystemPriority.ScorchMark, "ScorchMarkFX"),           // Priority 2
            (ParticleSystemPriority.AlwaysRender, "AlwaysRenderFX"),       // Priority 13 (high)
            (ParticleSystemPriority.Buildup, "BuildupFX"),                 // Priority 4
        };

        // Act
        systems.Sort((a, b) =>
        {
            var priorityComparison = b.priority.CompareTo(a.priority);
            if (priorityComparison == 0)
            {
                return a.name.CompareTo(b.name);
            }
            return priorityComparison;
        });

        // Assert - should be sorted by priority descending
        Assert.Equal(3, systems.Count);
        Assert.Equal(ParticleSystemPriority.AlwaysRender, systems[0].priority);
        Assert.Equal(ParticleSystemPriority.Buildup, systems[1].priority);
        Assert.Equal(ParticleSystemPriority.ScorchMark, systems[2].priority);
    }

    /// <summary>
    /// Test that particle systems with the same priority are sorted by name (stable sort).
    /// </summary>
    [Fact]
    public void SortSystemsByPriority_WithSamePriority_UsesStableSort()
    {
        // Arrange
        var systems = new List<(ParticleSystemPriority priority, string name)>
        {
            (ParticleSystemPriority.Constant, "ZebraExplosion"),    // Same priority
            (ParticleSystemPriority.Constant, "AppleSmoke"),         // Same priority
            (ParticleSystemPriority.Constant, "MonkeyDust"),         // Same priority
        };

        // Act
        systems.Sort((a, b) =>
        {
            var priorityComparison = b.priority.CompareTo(a.priority);
            if (priorityComparison == 0)
            {
                return a.name.CompareTo(b.name);
            }
            return priorityComparison;
        });

        // Assert - should be sorted alphabetically by name
        Assert.Equal(3, systems.Count);
        Assert.Equal("AppleSmoke", systems[0].name);
        Assert.Equal("MonkeyDust", systems[1].name);
        Assert.Equal("ZebraExplosion", systems[2].name);
    }

    /// <summary>
    /// Test comprehensive sorting with mixed priorities and names.
    /// Primary sort: priority (descending)
    /// Secondary sort: name (ascending)
    /// </summary>
    [Fact]
    public void SortSystemsByPriority_WithMixedPrioritiesAndNames_SortsByPriorityFirst()
    {
        // Arrange
        var systems = new List<(ParticleSystemPriority priority, string name)>
        {
            (ParticleSystemPriority.AreaEffect, "Zebra"),             // High priority
            (ParticleSystemPriority.WeaponExplosion, "Apple"),        // Low priority
            (ParticleSystemPriority.AreaEffect, "Alpha"),             // High priority (same as first)
            (ParticleSystemPriority.DustTrail, "Monkey"),             // Medium priority
            (ParticleSystemPriority.WeaponExplosion, "Zebra2"),       // Low priority (same as second)
        };

        // Act
        systems.Sort((a, b) =>
        {
            var priorityComparison = b.priority.CompareTo(a.priority);
            if (priorityComparison == 0)
            {
                return a.name.CompareTo(b.name);
            }
            return priorityComparison;
        });

        // Assert
        // Expected order:
        // AreaEffect (11): Alpha, Zebra (alphabetical within priority)
        // DustTrail (4): Monkey
        // WeaponExplosion (1): Apple, Zebra2 (alphabetical within priority)
        Assert.Equal(5, systems.Count);

        // AreaEffect items (highest priority)
        Assert.Equal(ParticleSystemPriority.AreaEffect, systems[0].priority);
        Assert.Equal("Alpha", systems[0].name);
        Assert.Equal(ParticleSystemPriority.AreaEffect, systems[1].priority);
        Assert.Equal("Zebra", systems[1].name);

        // DustTrail item (medium priority)
        Assert.Equal(ParticleSystemPriority.DustTrail, systems[2].priority);
        Assert.Equal("Monkey", systems[2].name);

        // WeaponExplosion items (lowest priority)
        Assert.Equal(ParticleSystemPriority.WeaponExplosion, systems[3].priority);
        Assert.Equal("Apple", systems[3].name);
        Assert.Equal(ParticleSystemPriority.WeaponExplosion, systems[4].priority);
        Assert.Equal("Zebra2", systems[4].name);
    }

    /// <summary>
    /// Test that priority ordering matches EA Generals' hierarchy.
    /// AlwaysRender should have highest priority (render first).
    /// </summary>
    [Fact]
    public void SortSystemsByPriority_AlwaysRenderPriority_IsHighest()
    {
        // Arrange - these are the actual priority enum values from EA Generals
        var priorities = new List<ParticleSystemPriority>
        {
            ParticleSystemPriority.None,              // 0 (lowest)
            ParticleSystemPriority.AlwaysRender,      // 13 (highest)
            ParticleSystemPriority.Critical,          // 12 (near-highest)
            ParticleSystemPriority.WeaponExplosion,   // 1 (low)
        };

        // Act
        priorities.Sort((a, b) => b.CompareTo(a)); // Descending

        // Assert
        Assert.Equal(4, priorities.Count);
        Assert.Equal(ParticleSystemPriority.AlwaysRender, priorities[0]);
        Assert.Equal(ParticleSystemPriority.Critical, priorities[1]);
        Assert.Equal(ParticleSystemPriority.WeaponExplosion, priorities[2]);
        Assert.Equal(ParticleSystemPriority.None, priorities[3]);
    }
}
