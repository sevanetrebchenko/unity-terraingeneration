using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Unity.Mathematics;

[System.Serializable]
public class TerrainGenerator : MonoBehaviour
{
    // PUBLIC
    public Transform viewer;
    public Renderer renderer;
    public ComputeShader terrainHeightMapComputeShader;
    public AnimationCurve animationCurve;

    public int chunkSize = 21;

    public float surfaceLevel = 0f;
    public Color terrainColor = Color.white;
    public float noiseScale = 40.0f;
    [Range(1, 10)] public int numNoiseOctaves = 6;
    [Range(0, 1)] public float persistence = 0.15f;
    [Range(1, 20)] public float lacunarity = 4.0f;
    public int terrainSeed = 0;
    public Vector3 terrainOffset = Vector3.zero;
    public bool terrainSmoothing = true;

    // PRIVATE
    private int numChunksPerAxis = 8;
    private int totalNumChunks;
    private int totalChunkSize;

    private Dictionary<Vector3Int, TerrainChunk> terrainChunks;
    private Dictionary<Vector3Int, NativeArray<float>> terrainChunkHeightMaps;
    
    private int shaderKernel;

    private void Start()
    {
        chunkSize = 16;
        terrainSmoothing = false;

        totalNumChunks = numChunksPerAxis * numChunksPerAxis * numChunksPerAxis * 8;
        totalChunkSize = chunkSize * chunkSize * chunkSize;
        
        terrainChunks = new Dictionary<Vector3Int, TerrainChunk>();
        terrainChunkHeightMaps = new Dictionary<Vector3Int, NativeArray<float>>();
        shaderKernel = terrainHeightMapComputeShader.FindKernel("TerrainGen");
        
        NativeList<JobHandle> jobHandles = new NativeList<JobHandle>(totalNumChunks, Allocator.TempJob);

        // Generate terrain chunk height maps.
        int counter = 0;
        for (int z = -numChunksPerAxis; z < numChunksPerAxis; ++z)
        {
            for (int x = -numChunksPerAxis; x < numChunksPerAxis; ++x)
            {
                Vector3Int chunkPosition = new Vector3Int(x, 0, z);

                // Generate chunk height map.
                float[,] chunkHeightMap = NoiseMap.PerlinNoiseAlgorithm(chunkSize, terrainSeed, noiseScale, 1f, 0.3f, numNoiseOctaves, persistence, lacunarity, chunkPosition * chunkSize);
                InitializeHeightMap(chunkHeightMap, chunkPosition);
            }
        }

        foreach (KeyValuePair<Vector3Int, NativeArray<float>> chunkHeightMapData in terrainChunkHeightMaps)
        {
            TerrainChunk terrainChunk = new TerrainChunk(chunkHeightMapData.Value, chunkSize, chunkHeightMapData.Key, surfaceLevel, terrainSmoothing, transform);
            jobHandles.Add(terrainChunk.Schedule());
            terrainChunks.Add(chunkHeightMapData.Key, terrainChunk);
        }

        JobHandle.CompleteAll(jobHandles);
        jobHandles.Dispose();

        // Build meshes.
        foreach (KeyValuePair<Vector3Int, TerrainChunk> terrainChunkData in terrainChunks)
        {
            terrainChunkData.Value.ConstructMesh();
        }
    }

    private void OnDrawGizmos()
    {
        foreach (KeyValuePair<Vector3Int, TerrainChunk> chunkData in terrainChunks)
        {
            chunkData.Value.OnDrawGizmos();
        }
    }

    private void Update()
    {
 
    }

    private void OnDestroy()
    {
        foreach (KeyValuePair<Vector3Int, TerrainChunk> terrainChunk in terrainChunks)
        {
            terrainChunk.Value.OnDestroy();
        }
        
        foreach (KeyValuePair<Vector3Int, NativeArray<float>> heightMap in terrainChunkHeightMaps)
        {
            heightMap.Value.Dispose();
        }
    }

    // Returns height map for chunk above and below
    private void InitializeHeightMap(float[,] source, Vector3Int chunkPosition)
    {
        NativeArray<float> chunkHeightMap = new NativeArray<float>(chunkSize * chunkSize * chunkSize, Allocator.Persistent);
        DefaultConstructNativeArray(chunkHeightMap, 1);

        for (int x = 0; x < chunkSize; ++x)
        {
            for (int z = 0; z < chunkSize; ++z)
            {
                int noiseHeight = Mathf.FloorToInt(source[x, z] * animationCurve.Evaluate(source[x, z]) * 64);

                // Create additional chunks in the y direction to accommodate for height differences.
                // Negative height.
                if (noiseHeight < 0)
                {
                    int nextChunkPosition = chunkPosition.y - 1;
                    int numChunks = Mathf.Abs(Mathf.FloorToInt((float)noiseHeight / chunkSize));

                    for (int i = 0; i < numChunks; ++i)
                    {
                        Vector3Int newChunkPosition = new Vector3Int(chunkPosition.x, nextChunkPosition--, chunkPosition.z);
                        NativeArray<float> terrainHeightMap;
                        
                        if (!terrainChunkHeightMaps.ContainsKey(newChunkPosition))
                        {
                            terrainHeightMap = new NativeArray<float>(chunkSize * chunkSize * chunkSize, Allocator.Persistent);
                            DefaultConstructNativeArray(terrainHeightMap, -1);
                            terrainChunkHeightMaps.Add(newChunkPosition, terrainHeightMap);
                        }
                        else
                        {
                            terrainHeightMap = terrainChunkHeightMaps[newChunkPosition];
                        }
                        
                        // Negative height value, build from top down.
                        for (int y = chunkSize - 1; y >= 0 && noiseHeight != 0; --y)
                        {
                            terrainHeightMap[x + y * chunkSize * chunkSize + z * chunkSize] = 1;
                            ++noiseHeight;
                        }
                    }
                }
                // Positive height ABOVE 1 chunk
                else if (noiseHeight >= chunkSize)
                {
                    // Fill in chunk before proceeding.
                    for (int y = 0; y < chunkSize && noiseHeight != 0; ++y)
                    {
                        chunkHeightMap[x + y * chunkSize * chunkSize + z * chunkSize] = -1;
                        --noiseHeight;
                    }
                    
                    int nextChunkPosition = chunkPosition.y + 1;
                    // We're building one above this chunk.
                    int numChunks = Mathf.CeilToInt((float)noiseHeight / chunkSize);

                    for (int i = 0; i < numChunks; ++i)
                    {
                        Vector3Int newChunkPosition = new Vector3Int(chunkPosition.x, nextChunkPosition++, chunkPosition.z);
                        NativeArray<float> terrainHeightMap;
                        
                        if (!terrainChunkHeightMaps.ContainsKey(newChunkPosition))
                        {
                            terrainHeightMap = new NativeArray<float>(chunkSize * chunkSize * chunkSize, Allocator.Persistent);
                            DefaultConstructNativeArray(terrainHeightMap, 1);
                            terrainChunkHeightMaps.Add(newChunkPosition, terrainHeightMap);
                        }
                        else
                        {
                            terrainHeightMap = terrainChunkHeightMaps[newChunkPosition];
                        }
                        
                        // Negative height value, build from top down.
                        for (int y = 0; y < chunkSize && noiseHeight != 0; ++y)
                        {
                            terrainHeightMap[x + y * chunkSize * chunkSize + z * chunkSize] = -1;
                            --noiseHeight;
                        }
                    }
                }
                // Just this one chunk.
                else
                {
                    for (int y = 0; y < noiseHeight; ++y)
                    {
                        chunkHeightMap[x + y * chunkSize * chunkSize + z * chunkSize] = -1;
                    }
                }
            }
        }

        terrainChunkHeightMaps.Add(chunkPosition, chunkHeightMap);
    }

    private void DefaultConstructNativeArray(NativeArray<float> source, int value)
    {
        for (int y = 0; y < chunkSize; ++y)
        {
            for (int x = 0; x < chunkSize; ++x)
            {
                for (int z = 0; z < chunkSize; ++z)
                {
                    source[x + y * chunkSize * chunkSize + z * chunkSize] = value;
                }
            }
        }
    }

    private float[] GenerateTerrainChunkHeightMap(Vector3Int chunkPosition)
    {
        // Terrain height map buffer.
        ComputeBuffer terrainHeightMapBuffer = new ComputeBuffer(totalChunkSize, 4);

        Vector3[] noiseOctaves = new Vector3[numNoiseOctaves];
        System.Random seededGenerator = new System.Random(terrainSeed);

        // Calculate noise octaves.
        for (int i = 0; i < numNoiseOctaves; ++i)
        {
            noiseOctaves[i] = new Vector3((float) seededGenerator.NextDouble() * 2 - 1,
                (float) seededGenerator.NextDouble() * 2 - 1, (float) seededGenerator.NextDouble() * 2 - 1) * 1000;
        }

        // Create second compute buffer
        ComputeBuffer noiseOctavesBuffer = new ComputeBuffer(numNoiseOctaves, 12);
        noiseOctavesBuffer.SetData(noiseOctaves);

        // Set shader data.
        terrainHeightMapComputeShader.SetBuffer(shaderKernel, "terrainHeightMap", terrainHeightMapBuffer);
        terrainHeightMapComputeShader.SetBuffer(shaderKernel, "offsets", noiseOctavesBuffer);
        terrainHeightMapComputeShader.SetInt("chunkSize", chunkSize);
        terrainHeightMapComputeShader.SetFloat("noiseScale", noiseScale);
        terrainHeightMapComputeShader.SetInt("numNoiseOctaves", numNoiseOctaves);
        terrainHeightMapComputeShader.SetFloat("persistence", persistence);
        terrainHeightMapComputeShader.SetFloat("lacunarity", lacunarity);

        terrainHeightMapComputeShader.Dispatch(shaderKernel, Mathf.RoundToInt((float) totalChunkSize / 1024), 1, 1);
        float[] terrainHeightMap = new float[totalChunkSize];
        terrainHeightMapBuffer.GetData(terrainHeightMap);

        terrainHeightMapBuffer.Release();
        noiseOctavesBuffer.Release();

        return terrainHeightMap;
    }
}
