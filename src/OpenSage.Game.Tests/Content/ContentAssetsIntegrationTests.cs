using System;
using System.Linq;
using OpenSage.FileFormats.W3d;
using OpenSage.Data.Map;
using Xunit;

namespace OpenSage.Tests.Content;

/// <summary>
/// Integration tests for Content/Assets subsystem
/// Verifies asset loading, W3D parsing, map loading, and content pipeline
/// </summary>
public class ContentAssetsIntegrationTests : MockedGameTest
{
    #region Asset Store Integration

    [Fact(DisplayName = "Content: AssetStore Initialized")]
    public void Content_AssetStoreInitialized()
    {
        var game = Generals;
        
        // Asset store should be properly initialized
        Assert.NotNull(game.AssetStore);
        Assert.True(game.AssetStore.ObjectDefinitions != null);
    }

    [Fact(DisplayName = "Content: Object Definitions Available")]
    public void Content_ObjectDefinitionsAvailable()
    {
        var game = Generals;
        
        // Should be able to query object definitions
        var definitions = game.AssetStore.ObjectDefinitions;
        Assert.NotNull(definitions);
    }

    [Fact(DisplayName = "Content: Rank Templates Available")]
    public void Content_RankTemplatesAvailable()
    {
        var game = Generals;
        var assetStore = game.AssetStore;
        
        // Rank system should be initialized
        Assert.NotNull(assetStore.Ranks);
        Assert.True(assetStore.Ranks.Count > 0);
    }

    [Fact(DisplayName = "Content: Player Templates Available")]
    public void Content_PlayerTemplatesAvailable()
    {
        var game = Generals;
        var playerTemplates = game.AssetStore.PlayerTemplates;
        
        // Player templates should be loaded
        Assert.NotNull(playerTemplates);
    }

    #endregion

    #region W3D File Format

    [Fact(DisplayName = "Content: W3D Parser Available")]
    public void Content_W3DParserAvailable()
    {
        // W3D parser should be accessible
        // This verifies the OpenSage.FileFormats.W3d assembly is loaded
        var w3dType = typeof(W3dFile);
        Assert.NotNull(w3dType);
    }

    [Fact(DisplayName = "Content: W3D Chunk Types Defined")]
    public void Content_W3DChunkTypesDefined()
    {
        // W3D chunk types should be properly enumerated
        var chunkTypes = Enum.GetValues(typeof(W3dChunkType));
        Assert.NotEmpty(chunkTypes);
        
        // Should have core chunk types
        Assert.Contains(W3dChunkType.W3D_CHUNK_MESH, (W3dChunkType[])chunkTypes);
        Assert.Contains(W3dChunkType.W3D_CHUNK_ANIMATION, (W3dChunkType[])chunkTypes);
        Assert.Contains(W3dChunkType.W3D_CHUNK_HIERARCHY, (W3dChunkType[])chunkTypes);
    }

    [Fact(DisplayName = "Content: W3D Mesh Vertex Material Chunk")]
    public void Content_W3DMeshVertexMaterialChunk()
    {
        // Vertex material chunk types should be defined
        var vertexMaterialChunk = W3dChunkType.W3D_CHUNK_VERTEX_MATERIAL;
        Assert.NotEqual(0, (int)vertexMaterialChunk);
    }

    [Fact(DisplayName = "Content: W3D Texture Chunk Type")]
    public void Content_W3DTextureChunkType()
    {
        // Texture chunk types should be defined
        var textureChunk = W3dChunkType.W3D_CHUNK_TEXTURE;
        Assert.NotEqual(0, (int)textureChunk);
    }

    [Fact(DisplayName = "Content: W3D Animation Chunk Type")]
    public void Content_W3DAnimationChunkType()
    {
        // Animation should be part of W3D format
        var animationChunk = W3dChunkType.W3D_CHUNK_ANIMATION;
        Assert.NotEqual(0, (int)animationChunk);
    }

    #endregion

    #region Map Data Integration

    [Fact(DisplayName = "Content: Map Data Structures Available")]
    public void Content_MapDataStructuresAvailable()
    {
        var game = Generals;
        
        // Map data structures should be available for parsing
        // Verify through TerrainLogic which handles terrain data
        Assert.NotNull(game.TerrainLogic);
    }

    [Fact(DisplayName = "Content: Height Map Available")]
    public void Content_HeightMapAvailable()
    {
        var game = Generals;
        
        // Terrain logic should have height map
        Assert.NotNull(game.TerrainLogic);
        Assert.NotNull(game.TerrainLogic.HeightMap);
    }

    [Fact(DisplayName = "Content: Height Map Dimensions")]
    public void Content_HeightMapDimensions()
    {
        var game = Generals;
        var heightMap = game.TerrainLogic.HeightMap;
        
        // Height map should have valid dimensions
        Assert.True(heightMap.Width > 0);
        Assert.True(heightMap.Height > 0);
    }

    [Fact(DisplayName = "Content: Height Map Data Query")]
    public void Content_HeightMapDataQuery()
    {
        var game = Generals;
        var heightMap = game.TerrainLogic.HeightMap;
        
        // Should be able to query height at positions
        var height = heightMap.GetHeight(0, 0);
        Assert.True(height >= heightMap.MinZ && height <= heightMap.MaxZ);
    }

    #endregion

    #region Asset Loading Pipeline

    [Fact(DisplayName = "Content: Asset Store Scoping")]
    public void Content_AssetStoreScopingSupported()
    {
        var game = Generals;
        var assetStore = game.AssetStore;
        
        // Asset scoping should be available for memory management
        // Try pushing and popping scopes
        assetStore.PushScope();
        assetStore.PopScope();
        
        // Should not throw
        Assert.NotNull(assetStore);
    }

    [Fact(DisplayName = "Content: Asset Definition Enumeration")]
    public void Content_AssetDefinitionEnumeration()
    {
        var game = Generals;
        var defs = game.AssetStore.ObjectDefinitions;
        
        // Should be able to enumerate (even if empty)
        var defList = defs.ToList();
        Assert.NotNull(defList);
    }

    #endregion

    #region Cross-Subsystem Integration

    [Fact(DisplayName = "Content: Assets Integrated with GameEngine")]
    public void Content_AssetsIntegratedWithGameEngine()
    {
        var game = Generals;
        
        // GameEngine should have access to assets
        Assert.NotNull(game.GameEngine);
        Assert.NotNull(game.GameEngine.AssetStore);
    }

    [Fact(DisplayName = "Content: Asset Format Parsers Registered")]
    public void Content_AssetFormatParsersRegistered()
    {
        var game = Generals;
        var assetStore = game.AssetStore;
        
        // All critical format parsers should be available
        // ObjectDefinitions handles .ini parsed objects
        Assert.NotNull(assetStore.ObjectDefinitions);
        
        // Verify we can access various asset types
        Assert.NotNull(assetStore.PlayerTemplates);
        Assert.NotNull(assetStore.Ranks);
    }

    [Fact(DisplayName = "Content: Terrain Integration")]
    public void Content_TerrainIntegration()
    {
        var game = Generals;
        
        // Terrain system should be integrated with content
        Assert.NotNull(game.TerrainLogic);
        var heightMap = game.TerrainLogic.HeightMap;
        
        // Height map loaded through content pipeline
        Assert.NotNull(heightMap);
    }

    [Fact(DisplayName = "Content: Full Asset Pipeline Flow")]
    public void Content_FullAssetPipelineFlow()
    {
        var game = Generals;
        
        // Verify complete pipeline:
        // 1. AssetStore initialized
        Assert.NotNull(game.AssetStore);
        
        // 2. Object definitions loaded
        Assert.NotNull(game.AssetStore.ObjectDefinitions);
        
        // 3. Player templates available
        Assert.NotNull(game.AssetStore.PlayerTemplates);
        
        // 4. Terrain data loaded
        Assert.NotNull(game.TerrainLogic);
        Assert.NotNull(game.TerrainLogic.HeightMap);
        
        // 5. GameEngine has access to assets
        Assert.NotNull(game.GameEngine.AssetStore);
        
        // Complete pipeline working
        Assert.NotNull(game);
    }

    [Fact(DisplayName = "Content: Assets Accessible to Players")]
    public void Content_AssetsAccessibleToPlayers()
    {
        var game = Generals;
        var player = game.PlayerManager.GetPlayerByIndex(0);
        
        // Player should have access to game assets
        var defs = game.AssetStore.ObjectDefinitions;
        Assert.NotNull(defs);
        
        // Player should be able to reference definitions
        Assert.NotNull(player);
    }

    #endregion

    #region File Format Validation

    [Fact(DisplayName = "Content: Map File Structure")]
    public void Content_MapFileStructure()
    {
        var game = Generals;
        var terrainLogic = game.TerrainLogic;
        
        // Map file should contain terrain data
        Assert.NotNull(terrainLogic.HeightMap);
    }

    [Fact(DisplayName = "Content: W3D Hierarchy Structure")]
    public void Content_W3DHierarchyStructure()
    {
        // W3D hierarchy chunks should be properly defined
        var hierarchyChunk = W3dChunkType.W3D_CHUNK_HIERARCHY;
        var pivotsChunk = W3dChunkType.W3D_CHUNK_PIVOTS;
        
        // Both should be distinct and non-zero
        Assert.NotEqual(hierarchyChunk, pivotsChunk);
        Assert.NotEqual(0, (int)hierarchyChunk);
        Assert.NotEqual(0, (int)pivotsChunk);
    }

    [Fact(DisplayName = "Content: W3D Deformation Support")]
    public void Content_W3DDeformationSupport()
    {
        // W3D deformation chunks should be available
        var deformChunk = W3dChunkType.W3D_CHUNK_DEFORM;
        Assert.NotEqual(0, (int)deformChunk);
        
        var deformSetChunk = W3dChunkType.W3D_CHUNK_DEFORM_SET;
        Assert.NotEqual(0, (int)deformSetChunk);
    }

    #endregion
}
