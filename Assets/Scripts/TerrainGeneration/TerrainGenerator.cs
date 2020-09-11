using System;
using System.Collections.Generic;
using TreeEditor;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Unity.Mathematics;

[System.Serializable, BurstCompile]
public class TerrainGenerator : MonoBehaviour {
    // PUBLIC
    public Transform viewer;

    public int chunkSize = 21;

    public float surfaceLevel = 0.5f;
    public Color terrainColor = Color.white;
    public float noiseScale = 40.0f;
    [Range(1, 10)]
    public int numNoiseOctaves = 6;
    [Range(0, 1)]
    public float persistence = 0.15f;
    [Range(1, 20)]
    public float lacunarity = 4.0f;
    public uint terrainSeed = 0;
    public Vector3 terrainOffset = Vector3.zero;
    public bool terrainSmoothing = false;

    // PRIVATE
    private int numChunksPerAxis = 2;
    private int totalNumChunks;
    private int totalChunkSize;
    
    private NativeArray<float> terrainHeightMap;
    private Dictionary<Vector3Int, TerrainChunk> terrainChunks;
    private List<TerrainChunk> previousFrameTerrainChunks;

    private void Start()
    {
        chunkSize = 21;
        terrainSeed = 23452345;
        totalNumChunks = numChunksPerAxis * numChunksPerAxis * numChunksPerAxis * 8;
        totalChunkSize = chunkSize * chunkSize * chunkSize;
        
        terrainChunks = new Dictionary<Vector3Int, TerrainChunk>();
        previousFrameTerrainChunks = new List<TerrainChunk>();
        
        // Generate starting chunks
        Vector3Int[] chunkPositions = new Vector3Int[totalNumChunks];
        terrainHeightMap = new NativeArray<float>(totalChunkSize * totalNumChunks, Allocator.Persistent);

        // Generate terrain chunk height maps.
        int counter = 0;
        for (int y = -numChunksPerAxis; y < numChunksPerAxis; ++y)
        {
            for (int z = -numChunksPerAxis; z < numChunksPerAxis; ++z)
            {
                for (int x = -numChunksPerAxis; x < numChunksPerAxis; ++x)
                {
                    Vector3Int chunkPosition = new Vector3Int(x, y, z);
                    chunkPositions[counter] = chunkPosition;
                    
                    // Generate chunk height map.
                    NativeArray<float> chunkHeightMap = new NativeArray<float>(totalChunkSize, Allocator.TempJob);
                    JobHandle handle = GenerateTerrainChunkHeightMap(chunkHeightMap, chunkPosition);
                    handle.Complete();
                    NativeArray<float>.Copy(chunkHeightMap, 0, terrainHeightMap, counter * totalChunkSize, totalChunkSize);

                    chunkHeightMap.Dispose();
                    ++counter;
                }
            }
        }
        
        // Create terrain chunks.
        for (int i = 0; i < chunkPositions.Length; ++i)
        {
            Vector3Int chunkPosition = chunkPositions[i];
            // Generate chunk from height map.
            TerrainChunk terrainChunk = new TerrainChunk(terrainHeightMap.GetSubArray(i * totalChunkSize, totalChunkSize), chunkSize, chunkPosition, surfaceLevel, terrainSmoothing, transform);
            terrainChunk.SetLayer(8);
            terrainChunks.Add(chunkPosition, terrainChunk);
        }
        
    }

    private void Update()
    {
        
    }

    private JobHandle GenerateTerrainChunkHeightMap(NativeArray<float> terrainHeightMap, Vector3Int chunkPosition)
    {
        TerrainHeightMapGenerationJob terrainHeightMapGenerationJob = new TerrainHeightMapGenerationJob()
        {
            terrainHeightMap = terrainHeightMap,
            noiseMap = PerlinNoise.PermutationArray,
            chunkSize = chunkSize,
            mapSeed = terrainSeed,
            noiseScale = noiseScale,
            numNoiseOctaves = numNoiseOctaves,
            persistence = persistence,
            lacunarity = lacunarity,
            manualOffset = chunkPosition * chunkSize + terrainOffset,
        };
        // Each job computes one layer of the chunk.
        return terrainHeightMapGenerationJob.ScheduleBatch(totalChunkSize, chunkSize * chunkSize);
    }
    //
    // public void ReceiveClick(Transform objectTransform, Vector3 hitPoint, bool place, int miningRadius) {
    //     Debug.Log("Input");
    //     Vector3 relativeObjectPosition = objectTransform.position - transform.position;
    //     Vector3Int normalizedObjectPosition = new Vector3Int(Mathf.RoundToInt(relativeObjectPosition.x), Mathf.RoundToInt(relativeObjectPosition.y), Mathf.RoundToInt(relativeObjectPosition.z)) / (chunkSize - 1);
    //
    //     Vector3 relativeHitLocation = hitPoint - transform.position - objectTransform.position;
    //     Vector3Int normalizedHitPosition = new Vector3Int(Mathf.RoundToInt(relativeHitLocation.x), Mathf.RoundToInt(relativeHitLocation.y), Mathf.RoundToInt(relativeHitLocation.z));
    //
    //     if (terrainChunks.ContainsKey(normalizedObjectPosition)) {
    //         // TerrainChunk hitChunk = terrainChunks[normalizedObjectPosition];
    //         // hitChunk.InputTriggered(normalizedHitPosition, place, miningRadius);
    //         // hitChunk.Regenerate();
    //     }
    // }
    //
    // private void UpdateVisibleChunks() {
    //     // Set previous chunks to be invisible.
    //     for (int i = 0; i < previousFrameTerrainChunks.Count; i++) {
    //         previousFrameTerrainChunks[i].SetVisible(false);
    //     }
    //     previousFrameTerrainChunks.Clear();
    //
    //     // Get the current viewer position.
    //     Vector3 viewerPosition = viewer.transform.position;
    //     int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
    //     int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);
    //     int currentChunkCoordZ = Mathf.RoundToInt(viewerPosition.z / chunkSize);
    //
    //     for (int yOffset = -numChunksPerAxis; yOffset <= numChunksPerAxis; yOffset++) {
    //         for (int xOffset = -numChunksPerAxis; xOffset <= numChunksPerAxis; xOffset++) {
    //             for (int zOffset = -numChunksPerAxis; zOffset <= numChunksPerAxis; zOffset++) {
    //                 Vector3Int viewedChunkCoord = new Vector3Int(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset, currentChunkCoordZ + zOffset);
    //
    //                 // Terrain chunk exists already.
    //                 if (terrainChunks.ContainsKey(viewedChunkCoord)) {
    //                     // Update terrain chunk to recalculate visibility.
    //                     terrainChunks[viewedChunkCoord].UpdateTerrainChunk(viewer.position, numChunksPerAxis * chunkSize);
    //
    //                     // If the terrain chunk is visible, add it to the previousFrameTerrain chunks.
    //                     if (terrainChunks[viewedChunkCoord].IsVisible()) {
    //                         previousFrameTerrainChunks.Add(terrainChunks[viewedChunkCoord]);
    //                     }
    //                 }
    //                 // Terrain chunk does not exist, generate a new one.
    //                 else {
    //                     // // Terrain chunk does not need to be updated as it is always going to be visible upon creation.
    //                     // TerrainChunk chunk = new TerrainChunk(this, viewedChunkCoord, chunkSize - 1, transform, terrainSmoothing);
    //                     // chunk.SetLayer(gameObject.layer);
    //                     // terrainChunks.Add(viewedChunkCoord, chunk);
    //                 }
    //             }
    //         }
    //     }
    // }
}

[BurstCompile]
public struct TerrainHeightMapGenerationJob : IJobParallelForBatch
{
    [NativeDisableParallelForRestriction, WriteOnly] public NativeArray<float> terrainHeightMap; // Array for the height maps for all terrain chunks.
    [ReadOnly] public NativeArray<int> noiseMap;
    
    public int chunkSize;
    
    public uint mapSeed;
    public float noiseScale;
    public int numNoiseOctaves;
    public float persistence;
    public float lacunarity;
    public float3 manualOffset;
    
    public void Execute(int startingIndex, int count)
    {
        NativeArray<float3> octaveOffsets = new NativeArray<float3>(numNoiseOctaves, Allocator.Temp);

        for (int currentIndex = 0; currentIndex < count; ++currentIndex)
        {
            int index = startingIndex + currentIndex;
            Unity.Mathematics.Random seededGenerator = new Unity.Mathematics.Random(mapSeed);
            for (int i = 0; i < numNoiseOctaves; ++i) {
                float offsetX = seededGenerator.NextFloat(-100000, 100000) + manualOffset.x;
                float offsetY = seededGenerator.NextFloat(-100000, 100000) + manualOffset.y;
                float offsetZ = seededGenerator.NextFloat(-100000, 100000) + manualOffset.z;

                octaveOffsets[i] = new Vector3(offsetX, offsetY, offsetZ);
            }

            if (noiseScale <= 0) {
                noiseScale = 0.0001f;
            }
            
            int3 normalizedCubePosition = PointFromIndex(index);
            
            // Generate perlin noise values in the map.
            float amplitude = 1.0f;
            float frequency = 1.0f;
            float noiseHeight = 0.0f;

            for (int i = 0; i < numNoiseOctaves; ++i) {
                float sampleX = (normalizedCubePosition.x + manualOffset.x) / noiseScale * frequency;
                float sampleY = (normalizedCubePosition.y + manualOffset.y) / noiseScale * frequency;
                float sampleZ = (normalizedCubePosition.z + manualOffset.z) / noiseScale * frequency;

                // Perlin noise gets the same value each time if the arguments passed are integer values.
                float perlinValue = Noise(sampleX, sampleY, sampleZ) + 0.5f;
                noiseHeight += perlinValue * amplitude;

                amplitude *= persistence; // Persistence should be between 0 and 1 - amplitude decreases with each octave.
                frequency *= lacunarity;  // Lacunarity should be greater than 1 - frequency increases with each octave.
            }

            terrainHeightMap[index] = noiseHeight;
        }

        octaveOffsets.Dispose();
    }

    
    private int3 PointFromIndex(int index)
    {
        return new int3(index % chunkSize, index / (chunkSize * chunkSize), (index / chunkSize) % chunkSize);
    }
    
    // DENSITY FUNCTION
    // Taken from https://github.com/keijiro/PerlinNoise/blob/master/Assets/Perlin.cs
    private float Noise(float x, float y, float z) {
        var X = Mathf.FloorToInt(x) & 0xff;
        var Y = Mathf.FloorToInt(y) & 0xff;
        var Z = Mathf.FloorToInt(z) & 0xff;
        x -= Mathf.Floor(x);
        y -= Mathf.Floor(y);
        z -= Mathf.Floor(z);
        var u = Fade(x);
        var v = Fade(y);
        var w = Fade(z);
        var A = (noiseMap[X] + Y) & 0xff;
        var B = (noiseMap[X + 1] + Y) & 0xff;
        var AA = (noiseMap[A] + Z) & 0xff;
        var BA = (noiseMap[B] + Z) & 0xff;
        var AB = (noiseMap[A + 1] + Z) & 0xff;
        var BB = (noiseMap[B + 1] + Z) & 0xff;
        return Lerp(w, Lerp(v, Lerp(u, Grad(noiseMap[AA], x, y, z), Grad(noiseMap[BA], x - 1, y, z)),
                Lerp(u, Grad(noiseMap[AB], x, y - 1, z), Grad(noiseMap[BB], x - 1, y - 1, z))),
            Lerp(v, Lerp(u, Grad(noiseMap[AA + 1], x, y, z - 1), Grad(noiseMap[BA + 1], x - 1, y, z - 1)),
                Lerp(u, Grad(noiseMap[AB + 1], x, y - 1, z - 1), Grad(noiseMap[BB + 1], x - 1, y - 1, z - 1))));
    }

    private float Fade(float t) {
        return t * t * t * (t * (t * 6 - 15) + 10);
    }

    private float Lerp(float t, float a, float b) {
        return a + t * (b - a);
    }

    private static float Grad(int hash, float x, float y, float z) {
        var h = hash & 15;
        var u = h < 8 ? x : y;
        var v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }
}

