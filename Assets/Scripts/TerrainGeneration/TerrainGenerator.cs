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
    private int numChunksPerAxis = 1;
    private int totalNumChunks;
    private int totalChunkSize;

    private Dictionary<Vector3Int, TerrainChunk> terrainChunks;
    private List<TerrainChunk> previousFrameTerrainChunks;
    private int shaderKernel;

    private void Start()
    {
        chunkSize = 65;
        terrainSeed = 23452345;
        terrainSmoothing = true;
        totalNumChunks = numChunksPerAxis * numChunksPerAxis * numChunksPerAxis * 8;
        totalChunkSize = chunkSize * chunkSize * chunkSize;

        terrainChunks = new Dictionary<Vector3Int, TerrainChunk>();
        previousFrameTerrainChunks = new List<TerrainChunk>();
        shaderKernel = terrainHeightMapComputeShader.FindKernel("TerrainGen");
        
        // Generate starting chunks
        Vector3Int[] chunkPositions = new Vector3Int[totalNumChunks];

        NativeArray<JobHandle> jobHandles = new NativeArray<JobHandle>(totalNumChunks, Allocator.TempJob);

        // Generate terrain chunk height maps.
        int counter = 0;
        for (int y = -0; y < numChunksPerAxis; ++y)
        {
            for (int z = -0; z < numChunksPerAxis; ++z)
            {
                for (int x = -0; x < numChunksPerAxis; ++x)
                {
                    Vector3Int chunkPosition = new Vector3Int(x, y, z);
                    chunkPositions[counter] = chunkPosition;

                    // Generate chunk height map.
                    float[,] chunkHeightMap = NoiseMap.GenerateNoiseMap(chunkSize, terrainSeed, noiseScale, 1f, 0.5f, numNoiseOctaves, persistence, lacunarity, chunkPosition * chunkSize);
                    NativeArray<float> chunkHeightMapArray = new NativeArray<float>(totalChunkSize, Allocator.Persistent);
                    InitializeHeightMap(chunkHeightMap, chunkHeightMapArray);
                    
                    Color[] colorMap = new Color[chunkSize * chunkSize];

                    for (int i = 0; i < chunkSize; ++i)
                    {
                        for (int j = 0; j < chunkSize; ++j)
                        {
                            colorMap[i + chunkSize * j] = Color.Lerp(Color.white, Color.black, chunkHeightMap[i,j]);
                        }
                    }
        
                    Texture2D texture = new Texture2D(chunkSize, chunkSize);
                    texture.SetPixels(colorMap);
                    texture.Apply();
        
                    renderer.sharedMaterial.mainTexture = texture;

                    // Generate chunk mesh.
                    TerrainChunk terrainChunk = new TerrainChunk(chunkHeightMapArray, chunkSize, chunkPosition, surfaceLevel, terrainSmoothing, transform);
                    jobHandles[counter] = terrainChunk.Schedule();
                    terrainChunks.Add(chunkPosition, terrainChunk);

                    ++counter;
                }
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

    private void Update()
    {
        if (Input.GetKey(KeyCode.K))
        {
            // // Generate starting chunks
            // Vector3Int[] chunkPositions = new Vector3Int[totalNumChunks];
            //
            // NativeArray<JobHandle> jobHandles = new NativeArray<JobHandle>(totalNumChunks, Allocator.TempJob);
            //
            // // Generate terrain chunk height maps.
            // int counter = 0;
            // for (int y = -numChunksPerAxis; y < numChunksPerAxis; ++y)
            // {
            //     for (int z = -numChunksPerAxis; z < numChunksPerAxis; ++z)
            //     {
            //         for (int x = -numChunksPerAxis; x < numChunksPerAxis; ++x)
            //         {
            //             Vector3Int chunkPosition = new Vector3Int(x, y, z);
            //             chunkPositions[counter] = chunkPosition;
            //
            //             // Generate chunk height map.
            //             float[] chunkHeightMap = NoiseMap.GenerateNoiseMap(chunkSize, 2.0f, 0.8f, terrainSeed);
            //             NativeArray<float> chunkHeightMapArray = new NativeArray<float>(totalChunkSize, Allocator.Persistent);
            //             InitializeHeightMap(chunkHeightMap, chunkHeightMapArray);
            //
            //             // Generate chunk mesh.
            //             TerrainChunk terrainChunk = new TerrainChunk(chunkHeightMapArray, chunkSize, chunkPosition, surfaceLevel, terrainSmoothing, transform);
            //             jobHandles[counter] = terrainChunk.Schedule();
            //             terrainChunks.Add(chunkPosition, terrainChunk);
            //
            //             ++counter;
            //         }
            //     }
            // }
            //
            // JobHandle.CompleteAll(jobHandles);
            // jobHandles.Dispose();
            //
            // // Build meshes.
            // foreach (KeyValuePair<Vector3Int, TerrainChunk> terrainChunkData in terrainChunks)
            // {
            //     terrainChunkData.Value.ConstructMesh();
            // }
        }
    }

    private void OnDestroy()
    {
        foreach (KeyValuePair<Vector3Int, TerrainChunk> terrainChunk in terrainChunks)
        {
            terrainChunk.Value.OnDestroy();
        }
    }

    private void InitializeHeightMap(float[,] source, NativeArray<float> destination)
    {
        for (int x = 0; x < chunkSize; ++x)
        {
            for (int z = 0; z < chunkSize; ++z)
            {
                float value = source[x, z] * chunkSize;
                int index = z * chunkSize + x;
                int counter = 0;

                // Construct surface.
                for (int i = 0; i < (int) value; ++i)
                {
                    if (counter == chunkSize)
                    {
                        break;
                    }
                    
                    destination[index] = -1;
                    index += chunkSize * chunkSize;
                    ++counter;

                }

                if (counter == chunkSize)
                {
                    break;
                }
                
                // Reached middle between surface and air.
                float realValue = value - (int) value;
                destination[index] = realValue;
                index += chunkSize * chunkSize;
                ++counter;
                
                // Construct air.
                for (int i = 0; i < chunkSize - Mathf.CeilToInt(value); ++i)
                {
                    if (counter == chunkSize)
                    {
                        break;
                    }
                    
                    destination[index] = 1;
                    index += chunkSize * chunkSize;
                    ++counter;
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
