using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Unity.Mathematics;

[Serializable]
public class TerrainGenerator : MonoBehaviour
{
    // PUBLIC
    public Transform viewer;
    public GameObject chunkPrefab;
    public AnimationCurve animationCurve;

    public int numChunksX;
    public int numChunksZ;
    private int numChunksY;
    public DensityValues densityValues;

    // PRIVATE
    private int numNodesPerAxis;
    private int numCubes;
    
    private int totalNumChunks;
    private int totalChunkSize;
    private int totalStackSize;
    private SampledAnimationCurve sampledAnimationCurve;

    private Dictionary<Vector3Int, TerrainChunk> terrainChunks;
    private Dictionary<Vector3, TerrainChunkConnector> terrainChunkConnectors;
    private Dictionary<Vector3Int, NativeArray<float>> terrainStackHeightPlanes; 
    private Dictionary<Vector3Int, NativeArray<float>> terrainStackHeightMaps;

    private void Start()
    {
        numNodesPerAxis = 8;
        numCubes = numNodesPerAxis - 1;
        numChunksY = 2;
        
        totalNumChunks = numChunksX * numChunksY * numChunksZ;
        totalChunkSize = numNodesPerAxis * numNodesPerAxis * numNodesPerAxis;
        totalStackSize = totalChunkSize * numChunksY;
        
        terrainChunks = new Dictionary<Vector3Int, TerrainChunk>();
        terrainChunkConnectors = new Dictionary<Vector3, TerrainChunkConnector>();
        terrainStackHeightPlanes = new Dictionary<Vector3Int, NativeArray<float>>();
        terrainStackHeightMaps = new Dictionary<Vector3Int, NativeArray<float>>();
        sampledAnimationCurve = new SampledAnimationCurve(animationCurve, 10000);
        
        NativeList<JobHandle> jobHandles = new NativeList<JobHandle>(totalNumChunks, Allocator.TempJob);
        
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Generate all chunks.
        for (int z = -numChunksZ / 2; z < numChunksZ / 2; ++z)
        {
            for (int x = -numChunksX / 2; x < numChunksX / 2; ++x)
            {
                Vector3Int chunkPosition = new Vector3Int(x, 0, z);
                jobHandles.Add(Initialize3DChunkHeightMap(chunkPosition, -1.0f)); // Stacks start at all air
            }
        }
        
        JobHandle.CompleteAll(jobHandles);
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        
        
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Generate terrain chunk height maps.
        for (int z = -numChunksZ / 2; z < numChunksZ / 2; ++z)
        {
            for (int x = -numChunksX / 2; x < numChunksX / 2; ++x)
            {
                Vector3Int chunkPosition = new Vector3Int(x, 0, z);
                jobHandles.Add(Generate2DChunkHeightMap(chunkPosition));
            }
        }
        
        JobHandle.CompleteAll(jobHandles);
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Initialize 3D chunk height values.
        for (int z = -numChunksZ / 2; z < numChunksZ / 2; ++z)
        {
            for (int x = -numChunksX / 2; x < numChunksX / 2; ++x)
            {
                Vector3Int chunkPosition = new Vector3Int(x, 0, z);
                jobHandles.Add(Generate3DChunkHeightMap(chunkPosition));
            }
        }
        
        JobHandle.CompleteAll(jobHandles);
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Generate terrain chunks from initialized height maps.
        foreach (KeyValuePair<Vector3Int, NativeArray<float>> terrainStackData in terrainStackHeightMaps)
        {
            Vector3Int stackPosition = terrainStackData.Key;
            NativeArray<float> terrainStack = terrainStackData.Value;

            int yIndex = 0;
            for (int chunkY = -numChunksY / 2; chunkY < numChunksY / 2; ++chunkY)
            {
                Vector3Int chunkPosition = new Vector3Int(stackPosition.x, stackPosition.y + chunkY, stackPosition.z);
                
                // Copy over data into new chunk height map.
                NativeArray<float> chunkHeightMap = new NativeArray<float>(totalChunkSize, Allocator.Persistent);
                for (int x = 0; x < numNodesPerAxis; ++x)
                {
                    for (int z = 0; z < numNodesPerAxis; ++z)
                    {
                        for (int y = 0; y < numNodesPerAxis; ++y)
                        {
                            int localIndex = x + z * numNodesPerAxis + y * numNodesPerAxis * numNodesPerAxis;
                            chunkHeightMap[localIndex] = terrainStack[localIndex + yIndex * totalChunkSize];
                        }
                    }
                }

                ++yIndex;
                
                TerrainChunk chunk = new TerrainChunk(numNodesPerAxis, numCubes, chunkPosition, chunkHeightMap, transform);
                terrainChunks.Add(chunkPosition, chunk);
                jobHandles.Add(GenerateChunkMesh(chunk));
            }
        }
        
        JobHandle.CompleteAll(jobHandles);
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Generate terrain between chunks.
        for (int z = -numChunksZ / 2; z < numChunksZ / 2; ++z)
        {
            for (int x = -numChunksX / 2; x < numChunksX / 2; ++x)
            {
                for (int y = -numChunksY / 2; y < numChunksY / 2; ++y)
                {
                    Vector3Int chunkPosition = new Vector3Int(x, y, z);
        
                    if (terrainChunks.ContainsKey(chunkPosition))
                    {
                        // Generate connectors for chunk sides.
                        Vector3 connectorPositionLeft = new Vector3(x - 0.5f, y, z);
                        Vector3 connectorPositionRight = new Vector3(x + 0.5f, y, z);
                        Vector3 connectorPositionTop = new Vector3(x, y + 0.5f, z);
                        Vector3 connectorPositionBottom = new Vector3(x, y - 0.5f, z);
                        Vector3 connectorPositionFront = new Vector3(x, y, z + 0.5f);
                        Vector3 connectorPositionBack = new Vector3(x, y, z - 0.5f);
        
                        // LEFT
                        if (!terrainChunkConnectors.ContainsKey(connectorPositionLeft))
                        {
                            Vector3Int otherChunk = new Vector3Int(chunkPosition.x - 1, chunkPosition.y, chunkPosition.z);
                            if (terrainChunks.ContainsKey(chunkPosition) && terrainChunks.ContainsKey(otherChunk))
                            {
                                // Left wall of this chunk, right wall of right chunk
                                TerrainChunk first = terrainChunks[chunkPosition];
                                TerrainChunk second = terrainChunks[otherChunk];
        
                                // Left wall
                                NativeArray<float> firstHeightMap = new NativeArray<float>(numNodesPerAxis * numNodesPerAxis, Allocator.Persistent);
                                int index = 0;
                                for (int i = 0; i < numNodesPerAxis; ++i) // z
                                {
                                    for (int j = 0; j < numNodesPerAxis; ++j) // y
                                    {
                                        int chunkIndex = 0 + numNodesPerAxis * i + j * numNodesPerAxis * numNodesPerAxis;
                                        firstHeightMap[index++] = first.heightMap[chunkIndex];
                                    }
                                }
                                    
                                // Right wall.
                                NativeArray<float> secondHeightMap = new NativeArray<float>(numNodesPerAxis * numNodesPerAxis, Allocator.Persistent);
                                index = 0;
                                for (int i = 0; i < numNodesPerAxis; ++i) // z
                                {
                                    for (int j = 0; j < numNodesPerAxis; ++j) // y
                                    {
                                        int chunkIndex = numNodesPerAxis - 1 + numNodesPerAxis * i + j * numNodesPerAxis * numNodesPerAxis;
                                        secondHeightMap[index++] = second.heightMap[chunkIndex];
                                    }
                                }
        
                                
                                TerrainChunkConnector terrainChunkConnector = new TerrainChunkConnector(numNodesPerAxis, new Vector3(x, y, z) * numNodesPerAxis - new Vector3(1.0f, -1.0f, 0.0f), firstHeightMap, secondHeightMap);
                                terrainChunkConnectors.Add(connectorPositionLeft, terrainChunkConnector);
                                jobHandles.Add(terrainChunkConnector.GenerateConnectorMesh());
                            }
                        }
                        
                        // RIGHT
                        if (!terrainChunkConnectors.ContainsKey(connectorPositionRight))
                        {
                            Vector3Int otherChunk = new Vector3Int(chunkPosition.x + 1, chunkPosition.y, chunkPosition.z);
                            if (terrainChunks.ContainsKey(chunkPosition) && terrainChunks.ContainsKey(otherChunk))
                            {
                                // Left wall of this chunk, right wall of right chunk
                                TerrainChunk first = terrainChunks[chunkPosition];
                                TerrainChunk second = terrainChunks[otherChunk];
        
                                // Left wall
                                NativeArray<float> firstHeightMap = new NativeArray<float>(numNodesPerAxis * numNodesPerAxis, Allocator.Persistent);
                                int index = 0;
                                for (int i = 0; i < numNodesPerAxis; ++i) // z
                                {
                                    for (int j = 0; j < numNodesPerAxis; ++j) // y
                                    {
                                        int chunkIndex = 0 + numNodesPerAxis * i + j * numNodesPerAxis * numNodesPerAxis;
                                        firstHeightMap[index++] = first.heightMap[chunkIndex];
                                    }
                                }
                                    
                                // Right wall.
                                NativeArray<float> secondHeightMap = new NativeArray<float>(numNodesPerAxis * numNodesPerAxis, Allocator.Persistent);
                                index = 0;
                                for (int i = 0; i < numNodesPerAxis; ++i) // z
                                {
                                    for (int j = 0; j < numNodesPerAxis; ++j) // y
                                    {
                                        int chunkIndex = numNodesPerAxis - 1 + numNodesPerAxis * i + j * numNodesPerAxis * numNodesPerAxis;
                                        secondHeightMap[index++] = second.heightMap[chunkIndex];
                                    }
                                }
        
                                
                                TerrainChunkConnector terrainChunkConnector = new TerrainChunkConnector(numNodesPerAxis, new Vector3(x, y, z) * numNodesPerAxis + new Vector3(1.0f, 0.0f, 0.0f), firstHeightMap, secondHeightMap);
                                terrainChunkConnectors.Add(connectorPositionRight, terrainChunkConnector);
                                jobHandles.Add(terrainChunkConnector.GenerateConnectorMesh());
                            }
                        }
                        
                        // TOP
                        if (!terrainChunkConnectors.ContainsKey(connectorPositionTop))
                        {
                            TerrainChunk first;
                            TerrainChunk second; 
                        }
                        
                        // BOTTOM
                        if (!terrainChunkConnectors.ContainsKey(connectorPositionBottom))
                        {
                            TerrainChunk first;
                            TerrainChunk second; 
                        }
                        
                        // FRONT
                        if (!terrainChunkConnectors.ContainsKey(connectorPositionFront))
                        {
                            TerrainChunk first;
                            TerrainChunk second; 
                        }
                        
                        // BACK
                        if (!terrainChunkConnectors.ContainsKey(connectorPositionBack))
                        {
                            TerrainChunk first;
                            TerrainChunk second; 
                        }
                        
                        // Generate connectors for chunk corners.
                        
                    }
                }
            }
        }
        
        JobHandle.CompleteAll(jobHandles);
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Build meshes.
        foreach (KeyValuePair<Vector3Int, TerrainChunk> terrainChunkData in terrainChunks)
        {
            TerrainChunk terrainChunk = terrainChunkData.Value;
            terrainChunk.ConstructMesh();
        }
        
        foreach (KeyValuePair<Vector3, TerrainChunkConnector> terrainConnectorData in terrainChunkConnectors)
        {
            TerrainChunkConnector terrainChunkConnector = terrainConnectorData.Value;
            terrainChunkConnector.ConstructMesh();
        }
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        
        // Cleanup
        for (int z = -numChunksZ / 2; z < numChunksZ / 2; ++z)
        {
            for (int x = -numChunksX / 2; x < numChunksX / 2; ++x)
            {
                for (int y = -numChunksY / 2; y < numChunksY / 2; ++y)
                {
                    Vector3Int chunkPosition = new Vector3Int(x, y, z);
                    if (terrainStackHeightPlanes.ContainsKey(chunkPosition))
                    {
                        terrainStackHeightPlanes[chunkPosition].Dispose();
                        terrainStackHeightPlanes.Remove(chunkPosition);
                    }

                    if (terrainStackHeightMaps.ContainsKey(chunkPosition))
                    {
                        terrainStackHeightMaps[chunkPosition].Dispose();
                        terrainStackHeightMaps.Remove(chunkPosition);
                    }
                }
            }
        }

        jobHandles.Dispose();
    }


    private void OnDrawGizmos()
    {
        foreach (KeyValuePair<Vector3, TerrainChunkConnector> chunkData in terrainChunkConnectors)
        {
            chunkData.Value.OnGizmos();
        }
        
        // foreach (KeyValuePair<Vector3Int, TerrainChunk> chunkData in terrainChunks)
        // {
        //     chunkData.Value.OnGizmos();
        // }
    }

    private JobHandle Generate2DChunkHeightMap(Vector3Int chunkPosition)
    {
        // Establish the map to write to.
        terrainStackHeightPlanes.Add(chunkPosition, new NativeArray<float>(numNodesPerAxis * numNodesPerAxis, Allocator.TempJob));
        
        PlaneHeightGenerationJob heightGenerationJob = new PlaneHeightGenerationJob
        {
            numNodesPerAxis = numNodesPerAxis,
            numNoiseOctaves = densityValues.numNoiseOctaves,
            noiseScale = densityValues.noiseScale,
            persistence = densityValues.persistence,
            lacunarity = densityValues.lacunarity,
            offset = new float3(chunkPosition.x, chunkPosition.y, chunkPosition.z) * numNodesPerAxis,
            terrainHeightMap = terrainStackHeightPlanes[chunkPosition],
            seededGenerator = new Unity.Mathematics.Random(densityValues.terrainSeed)
        };

        return heightGenerationJob.Schedule();
    }

    private JobHandle Initialize3DChunkHeightMap(Vector3Int chunkPosition, float heightValue)
    {
        NativeArray<float> chunkHeightMap = new NativeArray<float>(totalStackSize, Allocator.TempJob);
        terrainStackHeightMaps.Add(chunkPosition, chunkHeightMap);
        
        ChunkStackInitializationJob heightInitializationJob = new ChunkStackInitializationJob
        {
            numNodesPerAxis = numNodesPerAxis,
            chunkStackDimensions = new int3(numNodesPerAxis, numNodesPerAxis * numChunksY, numNodesPerAxis),
            heightValue = heightValue,
            heightMap = chunkHeightMap
        };

        return heightInitializationJob.Schedule();
    }

    private JobHandle Generate3DChunkHeightMap(Vector3Int chunkPosition)
    {
        // Establish the map to write to.
        ChunkHeightGenerationJob heightGenerationJob = new ChunkHeightGenerationJob
        {
            startingHeight = (numNodesPerAxis * numChunksY) / 5 * 3, // 4 chunks of air, 6 chunks of caves
            stackHeight = numNodesPerAxis * numChunksY,
            heightMultiplier = densityValues.heightMultiplier,
            numNodesPerAxis = numNodesPerAxis,
            terrainStackHeightMap = terrainStackHeightMaps[chunkPosition],
            terrainHeightMapPlane = terrainStackHeightPlanes[chunkPosition],
            sampledAnimationCurve = sampledAnimationCurve
        };

        return heightGenerationJob.Schedule();
    }
    
    private JobHandle GenerateChunkMesh(TerrainChunk terrainChunk)
    {
        // Establish the map to write to.
        MeshGenerationJob meshGenerationJob = new MeshGenerationJob
        {
            cornerTable = MarchingCubes.cornerTable,
            edgeTable = MarchingCubes.edgeTable,
            triangleTable = MarchingCubes.triangleTable,
            vertices = terrainChunk.vertices,
            numElements = terrainChunk.numElements,
            terrainHeightMap = terrainChunk.heightMap,
            terrainSurfaceLevel = densityValues.surfaceLevel,
            terrainSmoothing = densityValues.terrainSmoothing,
            axisDimensionsInCubes = new int3(numCubes, numCubes, numCubes),
            numNodesPerAxis = new int3(numNodesPerAxis, numNodesPerAxis, numNodesPerAxis)
        };

        return meshGenerationJob.Schedule();
    }

    private void OnDestroy()
    {
        foreach (KeyValuePair<Vector3Int, TerrainChunk> terrainChunk in terrainChunks)
        {
            terrainChunk.Value.OnDestroy();
        }
        
        foreach (KeyValuePair<Vector3Int, NativeArray<float>> heightMap in terrainStackHeightPlanes)
        {
            heightMap.Value.Dispose();
        }
        
        sampledAnimationCurve.Dispose();
    }
    
}
