using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OpenSage.Content.Loaders;
using OpenSage.Data.Map;
using OpenSage.Graphics.Rendering;

namespace OpenSage.Terrain;

public sealed class WaterAreaCollection : DisposableBase
{
    private readonly List<WaterArea> _waterAreas;
    private readonly List<StandingWaveArea> _waveAreaData;

    public WaterAreaCollection()
    {
        _waterAreas = new List<WaterArea>();
        _waveAreaData = new List<StandingWaveArea>();
    }

    internal WaterAreaCollection(PolygonTriggers polygonTriggers, StandingWaterAreas standingWaterAreas,
                                StandingWaveAreas standingWaveAreas, AssetLoadContext loadContext)
        : this()
    {
        if (polygonTriggers != null)
        {
            // TODO: Handle rivers differently. Water texture should be animated "downstream".
            foreach (var polygonTrigger in polygonTriggers.Triggers.Where(t => t.IsWater || t.IsRiver))
            {
                if (WaterArea.TryCreate(loadContext, polygonTrigger, out var waterArea))
                {
                    _waterAreas.Add(AddDisposable(waterArea));
                }
            }
        }

        if (standingWaterAreas != null)
        {
            foreach (var standingWaterArea in standingWaterAreas.Areas)
            {
                if (WaterArea.TryCreate(loadContext, standingWaterArea, out var waterArea))
                {
                    _waterAreas.Add(AddDisposable(waterArea));
                }
            }
        }

        if (standingWaveAreas != null)
        {
            foreach (var standingWaveArea in standingWaveAreas.Areas)
            {
                if (WaterArea.TryCreate(loadContext, standingWaveArea, out var waterArea))
                {
                    _waterAreas.Add(AddDisposable(waterArea));
                    _waveAreaData.Add(standingWaveArea);
                }
            }
        }
    }

    internal void BuildRenderList(RenderList renderList)
    {
        foreach (var waterArea in _waterAreas)
        {
            waterArea.BuildRenderList(renderList);
        }
    }

    internal void Update(float deltaTime)
    {
        foreach (var waterArea in _waterAreas)
        {
            waterArea.Update(deltaTime);
        }
    }

    /// <summary>
    /// Spawns a wave at a specific location within the standing wave area.
    /// </summary>
    internal void SpawnWave(int waterAreaIndex, Vector2 position, Vector2 direction)
    {
        if (waterAreaIndex < 0 || waterAreaIndex >= _waterAreas.Count)
        {
            return;
        }

        var waterArea = _waterAreas[waterAreaIndex];
        var waveData = _waveAreaData[waterAreaIndex];
        var waveSimulation = waterArea.GetWaveSimulation();

        if (waveSimulation == null)
        {
            return;
        }

        waveSimulation.SpawnWave(
            position,
            direction,
            initialVelocity: waveData.InitialVelocity,
            initialWidth: waveData.InitialWidthFraction,
            initialHeight: waveData.InitialHeightFraction,
            finalWidth: waveData.FinalWidth,
            finalHeight: waveData.FinalHeight,
            timeToFade: waveData.TimeToFade);
    }
}
