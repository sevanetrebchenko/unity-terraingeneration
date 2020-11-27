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

    public int chunkSize;
    public Vector3Int numChunksPerAxis;
    public DensityValues densityValues;

    // PRIVATE
    private int totalNumChunks;
    private int totalChunkSize;
    private int totalStackSize;
    private SampledAnimationCurve sampledAnimationCurve;

    private Dictionary<Vector3Int, TerrainChunk> terrainChunks;
    private Dictionary<Vector3Int, NativeArray<float>> terrainStackHeightPlanes; 
    private Dictionary<Vector3Int, NativeArray<float>> terrainStackHeightMaps;

    private void Start() {
        totalNumChunks = numChunksPerAxis.x * numChunksPerAxis.y * numChunksPerAxis.z;
        totalChunkSize = chunkSize * chunkSize * chunkSize;
        totalStackSize = totalChunkSize * numChunksPerAxis.y;
        
        terrainChunks = new Dictionary<Vector3Int, TerrainChunk>();
        terrainStackHeightPlanes = new Dictionary<Vector3Int, NativeArray<float>>();
        terrainStackHeightMaps = new Dictionary<Vector3Int, NativeArray<float>>();
        sampledAnimationCurve = new SampledAnimationCurve(animationCurve, 10000);
        
        NativeList<JobHandle> jobHandles = new NativeList<JobHandle>(totalNumChunks, Allocator.TempJob);
        
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Generate all chunks.
        for (int z = -numChunksPerAxis.z; z < numChunksPerAxis.z; ++z)
        {
            for (int x = -numChunksPerAxis.x; x < numChunksPerAxis.x; ++x)
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
        for (int z = -numChunksPerAxis.z; z < numChunksPerAxis.z; ++z)
        {
            for (int x = -numChunksPerAxis.x; x < numChunksPerAxis.x; ++x)
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
        for (int z = -numChunksPerAxis.z; z < numChunksPerAxis.z; ++z)
        {
            for (int x = -numChunksPerAxis.x; x < numChunksPerAxis.x; ++x)
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

            for (int chunkY = 0; chunkY < numChunksPerAxis.y; ++chunkY)
            {
                Vector3Int chunkPosition = new Vector3Int(stackPosition.x, stackPosition.y + chunkY, stackPosition.z);
                
                // Copy over data into new chunk height map.
                NativeArray<float> chunkHeightMap = new NativeArray<float>(totalChunkSize, Allocator.Persistent);
                for (int x = 0; x < chunkSize; ++x)
                {
                    for (int z = 0; z < chunkSize; ++z)
                    {
                        for (int y = 0; y < chunkSize; ++y)
                        {
                            int localIndex = x + z * chunkSize + y * chunkSize * chunkSize;
                            chunkHeightMap[localIndex] = terrainStack[localIndex + chunkY * totalChunkSize];
                        }
                    }
                }
                
                TerrainChunk chunk = new TerrainChunk(chunkSize, chunkPosition, chunkHeightMap, transform);
                terrainChunks.Add(chunkPosition, chunk);
                jobHandles.Add(GenerateChunkMesh(chunk));
            }
        }
        
        JobHandle.CompleteAll(jobHandles);
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Build meshes.
        foreach (KeyValuePair<Vector3Int, TerrainChunk> terrainChunkData in terrainChunks)
        {
            TerrainChunk terrainChunk = terrainChunkData.Value;
            terrainChunk.ConstructMesh();
        }
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        
        // Cleanup
        for (int z = -numChunksPerAxis.z; z < numChunksPerAxis.z; ++z)
        {
            for (int x = -numChunksPerAxis.x; x < numChunksPerAxis.x; ++x)
            {
                Vector3Int chunkPosition = new Vector3Int(x, 0, z);
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

        jobHandles.Dispose();
    }


    private void OnDrawGizmos()
    {
        foreach (KeyValuePair<Vector3Int, TerrainChunk> chunkData in terrainChunks)
        {
            chunkData.Value.OnGizmos();
        }
    }

    private JobHandle Generate2DChunkHeightMap(Vector3Int chunkPosition)
    {
        // Establish the map to write to.
        terrainStackHeightPlanes.Add(chunkPosition, new NativeArray<float>(chunkSize * chunkSize, Allocator.TempJob));
        
        PlaneHeightGenerationJob heightGenerationJob = new PlaneHeightGenerationJob
        {
            chunkSize = chunkSize,
            numNoiseOctaves = densityValues.numNoiseOctaves,
            noiseScale = densityValues.noiseScale,
            persistence = densityValues.persistence,
            lacunarity = densityValues.lacunarity,
            offset = new float3(chunkPosition.x, chunkPosition.y, chunkPosition.z) * chunkSize,
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
            chunkSize = chunkSize,
            chunkStackDimensions = new int3(chunkSize, chunkSize * numChunksPerAxis.y, chunkSize),
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
            startingHeight = (chunkSize * numChunksPerAxis.y) / 2,
            stackHeight = chunkSize * numChunksPerAxis.y,
            heightMultiplier = densityValues.heightMultiplier,
            chunkSize = chunkSize,
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
            chunkSize = chunkSize
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
