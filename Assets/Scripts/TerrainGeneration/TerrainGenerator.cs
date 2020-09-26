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
    private int numChunksPerAxis = 2;
    private int totalNumChunks;
    private int totalChunkSize;

    private Dictionary<Vector3Int, TerrainChunk> terrainChunks;
    private Dictionary<Vector3Int, NativeArray<float>> terrainChunkHeightMaps;
    private int maximumHeightInChunks;
    private int minimumHeightInChunks;
    
    private int shaderKernel;

    private void Start()
    {
        chunkSize = 65;
        terrainSeed = 23452345;
        terrainSmoothing = false;
        totalNumChunks = numChunksPerAxis * numChunksPerAxis * numChunksPerAxis * 8;
        totalChunkSize = chunkSize * chunkSize * chunkSize;
        
        terrainChunks = new Dictionary<Vector3Int, TerrainChunk>();
        terrainChunkHeightMaps = new Dictionary<Vector3Int, NativeArray<float>>();
        shaderKernel = terrainHeightMapComputeShader.FindKernel("TerrainGen");
        
        NativeArray<JobHandle> jobHandles = new NativeArray<JobHandle>(totalNumChunks, Allocator.TempJob);

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
                
                // Generate chunk mesh.
                TerrainChunk terrainChunk = new TerrainChunk(terrainChunkHeightMaps[chunkPosition], chunkSize, chunkPosition, surfaceLevel, terrainSmoothing, transform);
                jobHandles[counter] = terrainChunk.Schedule();
                terrainChunks.Add(chunkPosition, terrainChunk);

                ++counter;
            }
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
    }

    // Returns height map for chunk above and below
    private void InitializeHeightMap(float[,] source, Vector3Int chunkPosition)
    {
        NativeArray<float> chunkHeightMap = new NativeArray<float>(chunkSize * chunkSize * chunkSize, Allocator.Persistent);

        for (int y = 0; y < chunkSize; ++y)
        {
            for (int x = 0; x < chunkSize; ++x)
            {
                for (int z = 0; z < chunkSize; ++z)
                {
                    chunkHeightMap[x + y * chunkSize * chunkSize + z * chunkSize] = 1;
                }
            }
        }

        for (int x = 0; x < chunkSize; ++x)
        {
            for (int z = 0; z < chunkSize; ++z)
            {
                int noiseHeight = Mathf.FloorToInt(source[x, z] * animationCurve.Evaluate(source[x, z]) * chunkSize);

                for (int i = 0; i < noiseHeight && i < chunkSize; ++i)
                {
                    chunkHeightMap[x + i * chunkSize * chunkSize + z * chunkSize] = -1;
                }
                
                // for (int y = 0; y < chunkSize; ++y)
                // {
                //     destination[x + y * chunkSize * chunkSize + z * chunkSize] = Mathf.Clamp((y - (source[x, z] * 0.4f) / 3f * chunkSize) / chunkSize + noise.cnoise(new float3(x / 100.0f, y / 100.0f, z / 100.0f)) * .8f, -1, 1);
                // }
            }
        }

        terrainChunkHeightMaps.Add(chunkPosition, chunkHeightMap);
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
