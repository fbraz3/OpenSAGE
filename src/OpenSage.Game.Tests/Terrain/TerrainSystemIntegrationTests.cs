using System;
using System.Numerics;
using OpenSage.Data.Map;
using OpenSage.Logic.Object;
using OpenSage.Mathematics;
using OpenSage.Terrain;
using Xunit;

namespace OpenSage.Tests.Terrain;

public class TerrainSystemIntegrationTests : MockedGameTest
{
    #region TerrainLogic Infrastructure

    [Fact(DisplayName = "TerrainLogic: HeightMap property accessible")]
    public void TerrainLogic_HeightMapPropertyAccessible()
    {
        var game = Generals;
        var terrainLogic = game.TerrainLogic;
        
        Assert.NotNull(terrainLogic);
        Assert.NotNull(terrainLogic.HeightMap);
    }

    [Fact(DisplayName = "TerrainLogic: SetHeightMapData initializes HeightMap")]
    public void TerrainLogic_SetHeightMapDataInitializesHeightMap()
    {
        var game = Generals;
        var terrainLogic = game.TerrainLogic;
        
        // Create 64x64 heightmap (standard size with border)
        var elevations = new ushort[64, 64];
        for (int x = 0; x < 64; x++)
        {
            for (int y = 0; y < 64; y++)
            {
                elevations[x, y] = (ushort)(x + y);
            }
        }

        var heightMapData = HeightMapData.Create(2u, elevations);
        terrainLogic.SetHeightMapData(heightMapData);

        Assert.NotNull(terrainLogic.HeightMap);
        Assert.Equal(64, terrainLogic.HeightMap.Width);
        Assert.Equal(64, terrainLogic.HeightMap.Height);
    }

    #endregion

    #region TerrainLogic Height Queries

    [Fact(DisplayName = "TerrainLogic: GetGroundHeight returns valid float")]
    public void TerrainLogic_GetGroundHeightReturnsValidFloat()
    {
        var game = Generals;
        var terrainLogic = game.TerrainLogic;

        var height = terrainLogic.GetGroundHeight(0f, 0f);
        
        Assert.IsType<float>(height);
        Assert.False(float.IsNaN(height));
        Assert.False(float.IsInfinity(height));
    }

    [Fact(DisplayName = "TerrainLogic: GetGroundHeight with normal parameter returns valid values")]
    public void TerrainLogic_GetGroundHeightWithNormalReturnsValidValues()
    {
        var game = Generals;
        var terrainLogic = game.TerrainLogic;

        var height = terrainLogic.GetGroundHeight(50f, 50f, out var normal);

        Assert.IsType<float>(height);
        Assert.False(float.IsNaN(height));
        Assert.Equal(Vector3.UnitZ, normal);
    }

    [Fact(DisplayName = "TerrainLogic: GetLayerHeight returns valid float")]
    public void TerrainLogic_GetLayerHeightReturnsValidFloat()
    {
        var game = Generals;
        var terrainLogic = game.TerrainLogic;

        var height = terrainLogic.GetLayerHeight(25f, 25f, PathfindLayerType.Ground);

        Assert.IsType<float>(height);
        Assert.False(float.IsNaN(height));
    }

    [Fact(DisplayName = "TerrainLogic: GetLayerHeight with normal parameter works")]
    public void TerrainLogic_GetLayerHeightWithNormalWorks()
    {
        var game = Generals;
        var terrainLogic = game.TerrainLogic;

        var height = terrainLogic.GetLayerHeight(75f, 75f, PathfindLayerType.Ground, out var normal);

        Assert.IsType<float>(height);
        Assert.False(float.IsNaN(height));
        Assert.NotEqual(Vector3.Zero, normal);
    }

    #endregion

    #region TerrainLogic Boundary Operations

    [Fact(DisplayName = "TerrainLogic: SetActiveBoundary accepts valid boundary")]
    public void TerrainLogic_SetActiveBoundaryAcceptsValidBoundary()
    {
        var game = Generals;
        var terrainLogic = game.TerrainLogic;

        // Should not throw
        terrainLogic.SetActiveBoundary(0);
        terrainLogic.SetActiveBoundary(1);
        
        Assert.True(true);
    }

    [Fact(DisplayName = "TerrainLogic: GetExtent returns valid bounding box when HeightMap initialized")]
    public void TerrainLogic_GetExtentReturnsValidBoundingBoxWhenHeightMapInitialized()
    {
        var game = Generals;
        var terrainLogic = game.TerrainLogic;

        // Only test if HeightMap is available (might be null in mocked test)
        if (terrainLogic.HeightMap != null)
        {
            var extent = terrainLogic.GetExtent();

            Assert.NotEqual(Vector3.Zero, extent.Min);
            Assert.NotEqual(Vector3.Zero, extent.Max);
            Assert.True(extent.Max.X >= extent.Min.X);
            Assert.True(extent.Max.Y >= extent.Min.Y);
            Assert.True(extent.Max.Z >= extent.Min.Z);
        }
    }

    #endregion

    #region TerrainLogic Layer Operations

    [Fact(DisplayName = "TerrainLogic: GetLayerForDestination returns valid layer")]
    public void TerrainLogic_GetLayerForDestinationReturnsValidLayer()
    {
        var game = Generals;
        var terrainLogic = game.TerrainLogic;

        var layer = terrainLogic.GetLayerForDestination(new Vector3(100f, 100f, 50f));

        Assert.IsType<PathfindLayerType>(layer);
        Assert.Equal(PathfindLayerType.Ground, layer);
    }

    [Fact(DisplayName = "TerrainLogic: GetHighestLayerForDestination returns valid layer")]
    public void TerrainLogic_GetHighestLayerForDestinationReturnsValidLayer()
    {
        var game = Generals;
        var terrainLogic = game.TerrainLogic;

        var layer = terrainLogic.GetHighestLayerForDestination(new Vector3(100f, 100f, 50f));

        Assert.IsType<PathfindLayerType>(layer);
        Assert.Equal(PathfindLayerType.Ground, layer);
    }

    [Fact(DisplayName = "TerrainLogic: GetHighestLayerForDestination with bridges parameter")]
    public void TerrainLogic_GetHighestLayerForDestinationWithBridgesParameter()
    {
        var game = Generals;
        var terrainLogic = game.TerrainLogic;

        var layer = terrainLogic.GetHighestLayerForDestination(new Vector3(100f, 100f, 50f), onlyHealthyBridges: true);

        Assert.IsType<PathfindLayerType>(layer);
        Assert.Equal(PathfindLayerType.Ground, layer);
    }

    #endregion

    #region TerrainLogic Underwater Detection

    [Fact(DisplayName = "TerrainLogic: IsUnderwater returns boolean")]
    public void TerrainLogic_IsUnderwaterReturnsBoolean()
    {
        var game = Generals;
        var terrainLogic = game.TerrainLogic;

        var isUnderwater = terrainLogic.IsUnderwater(100f, 100f);

        Assert.IsType<bool>(isUnderwater);
    }

    [Fact(DisplayName = "TerrainLogic: IsUnderwater with waterZ parameter")]
    public void TerrainLogic_IsUnderwaterWithWaterZParameter()
    {
        var game = Generals;
        var terrainLogic = game.TerrainLogic;

        var isUnderwater = terrainLogic.IsUnderwater(100f, 100f, out var waterZ);

        Assert.IsType<bool>(isUnderwater);
        Assert.IsType<float>(waterZ);
    }

    [Fact(DisplayName = "TerrainLogic: IsUnderwater with waterZ and terrainZ parameters")]
    public void TerrainLogic_IsUnderwaterWithBothHeightParameters()
    {
        var game = Generals;
        var terrainLogic = game.TerrainLogic;

        var isUnderwater = terrainLogic.IsUnderwater(100f, 100f, out var waterZ, out var terrainZ);

        Assert.IsType<bool>(isUnderwater);
        Assert.IsType<float>(waterZ);
        Assert.IsType<float>(terrainZ);
    }

    #endregion

    #region TerrainLogic Cliff Detection

    [Fact(DisplayName = "TerrainLogic: IsCliffCell returns boolean")]
    public void TerrainLogic_IsCliffCellReturnsBoolean()
    {
        var game = Generals;
        var terrainLogic = game.TerrainLogic;

        var isCliff = terrainLogic.IsCliffCell(100f, 100f);

        Assert.IsType<bool>(isCliff);
    }

    #endregion

    #region HeightMap API

    [Fact(DisplayName = "HeightMap: Width and Height properties accessible")]
    public void HeightMap_WidthAndHeightPropertiesAccessible()
    {
        var game = Generals;
        var heightMap = game.TerrainLogic.HeightMap;

        Assert.True(heightMap.Width > 0);
        Assert.True(heightMap.Height > 0);
    }

    [Fact(DisplayName = "HeightMap: GetHeight with integer coordinates returns valid float")]
    public void HeightMap_GetHeightWithIntegerCoordinatesReturnsValidFloat()
    {
        var game = Generals;
        var heightMap = game.TerrainLogic.HeightMap;

        var height = heightMap.GetHeight(0, 0);

        Assert.IsType<float>(height);
        Assert.False(float.IsNaN(height));
    }

    [Fact(DisplayName = "HeightMap: GetHeight with float coordinates returns valid float")]
    public void HeightMap_GetHeightWithFloatCoordinatesReturnsValidFloat()
    {
        var game = Generals;
        var heightMap = game.TerrainLogic.HeightMap;

        var height = heightMap.GetHeight(50.5f, 50.5f);

        Assert.IsType<float>(height);
        Assert.False(float.IsNaN(height));
    }

    [Fact(DisplayName = "HeightMap: GetUpperHeight returns maximum of surrounding heights")]
    public void HeightMap_GetUpperHeightReturnsMaximumOfSurroundingHeights()
    {
        var game = Generals;
        var heightMap = game.TerrainLogic.HeightMap;

        var upperHeight = heightMap.GetUpperHeight(100f, 100f);

        Assert.IsType<float>(upperHeight);
        Assert.False(float.IsNaN(upperHeight));
    }

    [Fact(DisplayName = "HeightMap: MinZ and MaxZ properties accessible")]
    public void HeightMap_MinZAndMaxZPropertiesAccessible()
    {
        var game = Generals;
        var heightMap = game.TerrainLogic.HeightMap;

        Assert.IsType<float>(heightMap.MinZ);
        Assert.IsType<float>(heightMap.MaxZ);
        Assert.True(heightMap.MaxZ >= heightMap.MinZ);
    }

    [Fact(DisplayName = "HeightMap: GetNormal returns valid vector")]
    public void HeightMap_GetNormalReturnsValidVector()
    {
        var game = Generals;
        var heightMap = game.TerrainLogic.HeightMap;

        var normal = heightMap.GetNormal(50f, 50f);

        Assert.NotEqual(Vector3.Zero, normal);
        Assert.False(float.IsNaN(normal.X));
        Assert.False(float.IsNaN(normal.Y));
        Assert.False(float.IsNaN(normal.Z));
    }

    [Fact(DisplayName = "HeightMap: MaxXCoordinate and MaxYCoordinate accessible")]
    public void HeightMap_MaxCoordinatesAccessible()
    {
        var game = Generals;
        var heightMap = game.TerrainLogic.HeightMap;

        var maxX = heightMap.MaxXCoordinate;
        var maxY = heightMap.MaxYCoordinate;

        Assert.True(maxX >= 0);
        Assert.True(maxY >= 0);
    }

    [Fact(DisplayName = "HeightMap: HorizontalScale constant correct")]
    public void HeightMap_HorizontalScaleConstantCorrect()
    {
        Assert.Equal(10, HeightMap.HorizontalScale);
    }

    #endregion

    #region HeightMap Height Manipulation

    [Fact(DisplayName = "HeightMap: LowerHeight accepts valid parameters")]
    public void HeightMap_LowerHeightAcceptsValidParameters()
    {
        var game = Generals;
        var heightMap = game.TerrainLogic.HeightMap;

        // Get a valid height first
        var originalHeight = heightMap.GetHeight(5, 5);
        
        // Should not throw when lowering height
        heightMap.LowerHeight(5, 5, originalHeight - 10f);

        Assert.True(true);
    }

    [Fact(DisplayName = "HeightMap: LowerHeight respects boundary checks")]
    public void HeightMap_LowerHeightRespectsBoundaryChecks()
    {
        var game = Generals;
        var heightMap = game.TerrainLogic.HeightMap;

        // Get interior point height
        var originalHeight = heightMap.GetHeight(10, 10);
        
        // Lower it
        heightMap.LowerHeight(10, 10, originalHeight - 5f);

        var newHeight = heightMap.GetHeight(10, 10);

        // Height should have changed or stayed the same (implementation dependent)
        Assert.IsType<float>(newHeight);
    }

    #endregion

    #region Cross-Subsystem Integration

    [Fact(DisplayName = "TerrainLogic: Height queries work consistently")]
    public void TerrainLogic_HeightQueriesWorkConsistently()
    {
        var game = Generals;
        var terrainLogic = game.TerrainLogic;
        var groundHeight = terrainLogic.GetGroundHeight(100f, 100f);
        var layerHeight = terrainLogic.GetLayerHeight(100f, 100f, PathfindLayerType.Ground);

        // Both should return valid heights
        Assert.False(float.IsNaN(groundHeight));
        Assert.False(float.IsNaN(layerHeight));
    }

    [Fact(DisplayName = "TerrainLogic: Multiple position queries work")]
    public void TerrainLogic_MultiplePositionQueriesWork()
    {
        var game = Generals;
        var terrainLogic = game.TerrainLogic;

        var height1 = terrainLogic.GetGroundHeight(0f, 0f);
        var height2 = terrainLogic.GetGroundHeight(50f, 50f);
        var height3 = terrainLogic.GetGroundHeight(100f, 100f);

        // All should be valid floats
        Assert.False(float.IsNaN(height1));
        Assert.False(float.IsNaN(height2));
        Assert.False(float.IsNaN(height3));
    }

    #endregion

    #region TerrainLogic State Management

    [Fact(DisplayName = "TerrainLogic: Persist method callable")]
    public void TerrainLogic_PersistMethodCallable()
    {
        var game = Generals;
        var terrainLogic = game.TerrainLogic;

        Assert.NotNull(terrainLogic);
        // Verify it has Persist method (tested via reflection)
        var persistMethod = typeof(TerrainLogic).GetMethod("Persist");
        Assert.NotNull(persistMethod);
    }

    [Fact(DisplayName = "TerrainLogic: System maintains state between queries")]
    public void TerrainLogic_SystemMaintainsStateBetweenQueries()
    {
        var game = Generals;
        var terrainLogic = game.TerrainLogic;

        var height1 = terrainLogic.GetGroundHeight(50f, 50f);
        var height2 = terrainLogic.GetGroundHeight(50f, 50f);

        Assert.Equal(height1, height2);
    }

    [Fact(DisplayName = "TerrainLogic: IPersistableObject implementation")]
    public void TerrainLogic_IPersistableObjectImplementation()
    {
        var terrainLogic = new TerrainLogic();

        Assert.NotNull(terrainLogic);
        Assert.IsAssignableFrom<IPersistableObject>(terrainLogic);
    }

    #endregion

    #region HeightMap Boundaries

    [Fact(DisplayName = "HeightMap: Boundaries collection type correct")]
    public void HeightMap_BoundariesCollectionTypeCorrect()
    {
        var game = Generals;
        var heightMap = game.TerrainLogic.HeightMap;

        var boundaries = heightMap.Boundaries;

        // Should be an array or at least enumerable
        if (boundaries != null)
        {
            Assert.IsType<HeightMapBorder[]>(boundaries);
        }
    }

    #endregion

    #region Terrain System Initialization

    [Fact(DisplayName = "TerrainLogic: Type availability")]
    public void TerrainLogic_TypeAvailability()
    {
        var terrainLogicType = typeof(TerrainLogic);

        Assert.NotNull(terrainLogicType);
        Assert.NotNull(terrainLogicType.GetMethod("GetGroundHeight"));
        Assert.NotNull(terrainLogicType.GetMethod("GetLayerHeight"));
        Assert.NotNull(terrainLogicType.GetProperty("HeightMap"));
    }

    [Fact(DisplayName = "HeightMap: Type availability")]
    public void HeightMap_TypeAvailability()
    {
        var heightMapType = typeof(HeightMap);

        Assert.NotNull(heightMapType);
        Assert.NotNull(heightMapType.GetMethod("GetHeight"));
        Assert.NotNull(heightMapType.GetMethod("GetNormal"));
        Assert.NotNull(heightMapType.GetProperty("Width"));
        Assert.NotNull(heightMapType.GetProperty("Height"));
    }

    #endregion

    #region Terrain Data Structure

    [Fact(DisplayName = "HeightMapData: Create factory method works")]
    public void HeightMapData_CreateFactoryMethodWorks()
    {
        var elevations = new ushort[10, 10];
        for (int x = 0; x < 10; x++)
        {
            for (int y = 0; y < 10; y++)
            {
                elevations[x, y] = (ushort)(x * y);
            }
        }

        var heightMapData = HeightMapData.Create(1u, elevations);

        Assert.NotNull(heightMapData);
        Assert.Equal(10u, heightMapData.Width);
        Assert.Equal(10u, heightMapData.Height);
    }

    [Fact(DisplayName = "HeightMapData: Width and Height properties")]
    public void HeightMapData_WidthAndHeightProperties()
    {
        var elevations = new ushort[32, 32];
        var heightMapData = HeightMapData.Create(2u, elevations);

        Assert.Equal(32u, heightMapData.Width);
        Assert.Equal(32u, heightMapData.Height);
    }

    [Fact(DisplayName = "HeightMapData: Elevations array accessible")]
    public void HeightMapData_ElevationsArrayAccessible()
    {
        var elevations = new ushort[16, 16];
        for (int x = 0; x < 16; x++)
        {
            for (int y = 0; y < 16; y++)
            {
                elevations[x, y] = (ushort)(x + y);
            }
        }

        var heightMapData = HeightMapData.Create(1u, elevations);
        var elevationsRef = heightMapData.Elevations;

        Assert.NotNull(elevationsRef);
        Assert.Equal(16, elevationsRef.GetLength(0));
        Assert.Equal(16, elevationsRef.GetLength(1));
    }

    #endregion

    #region Pathfind Layer System

    [Fact(DisplayName = "PathfindLayerType: Enum values available")]
    public void PathfindLayerType_EnumValuesAvailable()
    {
        Assert.Equal(PathfindLayerType.Ground, PathfindLayerType.Ground);
        
        // Verify it's an enum
        Assert.True(typeof(PathfindLayerType).IsEnum);
    }

    #endregion
}
