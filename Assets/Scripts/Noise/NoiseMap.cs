using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public static class NoiseMap
{
    private static System.Random seededGenerator;
    private static int chunkSize;
    private static float noiseScale;
    private static int numNoiseOctaves;
    private static float persistence;
    private static float lacunarity;
    private static Vector2[] octaveOffsets;
    
    public static float[,] GenerateNoiseMap(int chunkSize, int mapSeed, float noiseScale, float roughness, float falloff, int numNoiseOctaves, float persistence, float lacunarity, Vector3 manualOffset)
    {
        // Initialize data.
        NoiseMap.chunkSize = chunkSize;
        NoiseMap.noiseScale = noiseScale;
        NoiseMap.numNoiseOctaves = numNoiseOctaves;
        NoiseMap.persistence = persistence;
        NoiseMap.lacunarity = lacunarity;
        float[,] heightMap = new float[chunkSize, chunkSize];
        seededGenerator = new System.Random();
        
        // Generate noise octave offsets.
        octaveOffsets = new Vector2[numNoiseOctaves];
        for (int i = 0; i < numNoiseOctaves; ++i) {
            float offsetX = seededGenerator.Next(-100000, 100000) + manualOffset.x;
            float offsetY = seededGenerator.Next(-100000, 100000) + manualOffset.y;

            octaveOffsets[i] = new Vector2(offsetX, offsetY);
        }

        for (int x = 0; x < chunkSize; ++x)
        {
            for (int y = 0; y < chunkSize; ++y)
            {
                heightMap[x, y] = 0;
            }
        }

        heightMap[0, 0] = ((float) seededGenerator.NextDouble() + 1f) / 2f;
        heightMap[chunkSize - 1, 0] = ((float) seededGenerator.NextDouble() + 1f) / 2f;
        heightMap[0, chunkSize - 1] = ((float) seededGenerator.NextDouble() + 1f) / 2f;
        heightMap[chunkSize - 1, chunkSize - 1] = ((float) seededGenerator.NextDouble() + 1f) / 2f;

        int numIterations = (int)Math.Log(chunkSize - 1, 2);
        int step = chunkSize / 2;
        int previousStep = chunkSize;

        for (int iteration = 0; iteration < numIterations; ++iteration)
        {
            for (int x = step; x < chunkSize - 1; x += previousStep)
            {
                for (int y = step; y < chunkSize - 1; y += previousStep)
                {
                    DiamondStep(heightMap, x, y, step, roughness);
                }
            }
            
            for (int x = step; x < chunkSize - 1; x += previousStep)
            {
                for (int y = step; y < chunkSize - 1; y += previousStep)
                {
                    SquareStepKickoff(heightMap, chunkSize, x, y, step, roughness);
                }
            }
            
            previousStep = step;
            step /= 2;
            roughness *= falloff;
        }
        
        return heightMap;
    }

    private static void DiamondStep(float[,] array, int x, int y, int step, float roughness) {
        float runningSum = 0;

        runningSum += array[x - step, y - step];
        
        runningSum += array[x + step, y - step];

        runningSum += array[x - step, y + step];

        runningSum += array[x + step, y + step];

        float value = runningSum / 4f;// + ((float) seededGenerator.NextDouble() - 0.5f) * 2f) / 3f;
        array[x, y] = value;
    }

    private static void SquareStepKickoff(float[,] array, int chunkSize, int x, int y, int step, float roughness)
    {
        SquareStep(array, chunkSize, x, y - step, step, roughness);
        SquareStep(array, chunkSize, x, y + step, step, roughness);
        SquareStep(array, chunkSize, x - step, y, step, roughness);
        SquareStep(array, chunkSize, x + step, y, step, roughness);
    }

    private static void SquareStep(float[,] array, int chunkSize, int x, int y, int step, float roughness)
    {
        float runningSum = 0;
        int count = 0;

        if (x - step >= 0)
        {
            runningSum += array[x - step, y];
            ++count;
        }

        if (x + step < chunkSize)
        {
            runningSum += array[x + step, y];
            ++count;
        }

        if (y - step >= 0)
        {
            runningSum += array[x, y - step];
            ++count;
        }

        if (y + step < chunkSize)
        {
            runningSum += array[x, y + step];
            ++count;
        }

        float value = runningSum / count;// + ((float) seededGenerator.NextDouble() - 0.5f) * 2f) / 3f;
        array[x, y] = value;
    }

    private static float GenerateNoise(int x, int y)
    {
        float noiseHeight = 0.0f;
        // Generate perlin noise values in the map.
        float amplitude = 1.0f;
        float frequency = 1.0f;

        for (int i = 0; i < numNoiseOctaves; ++i)
        {
            float sampleX = (x + octaveOffsets[i].x) / noiseScale * frequency;
            float sampleY = (y * chunkSize + octaveOffsets[i].y) / noiseScale * frequency;

            // Perlin noise gets the same value each time if the arguments passed are integer values.
            float perlinValue = Mathf.PerlinNoise(sampleX, sampleY);
            noiseHeight += perlinValue * amplitude;

            amplitude *= persistence; // Persistence should be between 0 and 1 - amplitude decreases with each octave.
            frequency *= lacunarity; // Lacunarity should be greater than 1 - frequency increases with each octave.
        }

        return noiseHeight;
    }
}