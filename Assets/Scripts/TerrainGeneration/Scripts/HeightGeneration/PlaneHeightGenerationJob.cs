
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Unity.Burst;
using Unity.Mathematics;

[BurstCompile]
public struct PlaneHeightGenerationJob : IJob
{
    [ReadOnly] public int chunkSize;
    [ReadOnly] public int numNoiseOctaves;
    [ReadOnly] public float noiseScale;
    [ReadOnly] public float persistence;
    [ReadOnly] public float lacunarity;
    [ReadOnly] public float3 offset;

    [WriteOnly] public NativeArray<float> terrainHeightMap;
    [ReadOnly] public Unity.Mathematics.Random seededGenerator;
    
    public void Execute()
    {
        // Generate terrain offsets.
        NativeArray<float2> octaveOffsets = new NativeArray<float2>(numNoiseOctaves, Allocator.Temp);
        
        for (int i = 0; i < numNoiseOctaves; ++i)
        {
            float offsetX = seededGenerator.NextFloat(-100000, 100000) + offset.x;
            float offsetY = seededGenerator.NextFloat(-100000, 100000) + offset.z;
            
            octaveOffsets[i] = new float2(offsetX, offsetY);
        }
        
        if (noiseScale <= 0)
        {
            noiseScale = 0.0001f;
        }
        
        // Generate perlin noise values in the map.
        for (int y = 0; y < chunkSize; ++y)
        {
            for (int x = 0; x < chunkSize; ++x)
            {
                float amplitude = 1.0f;
                float frequency = 1.0f;
                float noiseHeight = 0.0f;

                for (int i = 0; i < numNoiseOctaves; ++i)
                {
                    float sampleX = (x + octaveOffsets[i].x) / noiseScale * frequency;
                    float sampleY = (y + octaveOffsets[i].y) / noiseScale * frequency;

                    // Get perlin values from -1 to 1
                    float perlinValue = (Mathf.PerlinNoise(sampleX, sampleY) - 0.5f) * 2f;
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= persistence; // Persistence should be between 0 and 1 - amplitude decreases with each octave.
                    frequency *= lacunarity;  // Lacunarity should be greater than 1 - frequency increases with each octave.
                }

                terrainHeightMap[ExpandIndex(x, y)] = noiseHeight;
            }
        }

        octaveOffsets.Dispose();
    }

    private int ExpandIndex(int x, int y)
    {
        return x + (chunkSize) * y;
    }
}