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

    public static float[,] PerlinNoiseAlgorithm(int chunkSize, int mapSeed, float noiseScale, float roughness, float falloff, int numNoiseOctaves, float persistence, float lacunarity, Vector3Int manualOffset)
    {
        // Initialize data.
        NoiseMap.chunkSize = chunkSize;
        NoiseMap.noiseScale = noiseScale;
        NoiseMap.numNoiseOctaves = numNoiseOctaves;
        NoiseMap.persistence = persistence;
        NoiseMap.lacunarity = lacunarity;
        float[,] heightMap = new float[chunkSize, chunkSize];

        seededGenerator = new System.Random(mapSeed);
        octaveOffsets = new Vector2[numNoiseOctaves];
        for (int i = 0; i < numNoiseOctaves; ++i)
        {
            float offsetX = seededGenerator.Next(-100000, 100000) + manualOffset.x;
            float offsetY = seededGenerator.Next(-100000, 100000) + manualOffset.z;

            octaveOffsets[i] = new Vector2(offsetX, offsetY);
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
                    float sampleX = (float) (x + octaveOffsets[i].x) / noiseScale * frequency;
                    float sampleY = (float) (y + octaveOffsets[i].y) / noiseScale * frequency;

                    // Perlin noise gets the same value each time if the arguments passed are integer values.
                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY);
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= persistence; // Persistence should be between 0 and 1 - amplitude decreases with each octave.
                    frequency *= lacunarity; // Lacunarity should be greater than 1 - frequency increases with each octave.
                }

                heightMap[x, y] = noiseHeight;
            }
        }

        return heightMap;
    }


    public static float[,] DiamondSquaresAlgorithm(int chunkSize, int mapSeed, float noiseScale, float roughness,
        float falloff, int numNoiseOctaves, float persistence, float lacunarity, Vector3Int manualOffset)
    {
        // Initialize data.
        NoiseMap.chunkSize = chunkSize;
        NoiseMap.noiseScale = noiseScale;
        NoiseMap.numNoiseOctaves = numNoiseOctaves;
        NoiseMap.persistence = persistence;
        NoiseMap.lacunarity = lacunarity;
        float[,] heightMap = new float[chunkSize, chunkSize];

        seededGenerator = new System.Random(mapSeed);
        octaveOffsets = new Vector2[numNoiseOctaves];
        for (int i = 0; i < numNoiseOctaves; ++i)
        {
            float offsetX = seededGenerator.Next(-100000, 100000) + manualOffset.x;
            float offsetY = seededGenerator.Next(-100000, 100000) + manualOffset.y;

            octaveOffsets[i] = new Vector3(offsetX, offsetY);
        }

        // Middle row.
        heightMap[0, 0] = GetHeight(0 + manualOffset.x, 0 + manualOffset.y);
        heightMap[chunkSize - 1, 0] = GetHeight(chunkSize + manualOffset.x, 0 + manualOffset.y);
        heightMap[0, chunkSize - 1] = GetHeight(0 + manualOffset.x, chunkSize + manualOffset.y);
        heightMap[chunkSize - 1, chunkSize - 1] = GetHeight(chunkSize + manualOffset.x, chunkSize + manualOffset.y);

        int numIterations = (int) Math.Log(chunkSize - 1, 2);
        int step = chunkSize / 2;
        int previousStep = chunkSize;

        for (int iteration = 0; iteration < numIterations; ++iteration)
        {
            for (int x = step; x < chunkSize - 1; x += previousStep)
            {
                for (int y = step; y < chunkSize - 1; y += previousStep)
                {
                    DiamondStep(heightMap, x, y, step, roughness);
                    DiamondStep(heightMap, x, y, step, roughness);
                    DiamondStep(heightMap, x, y, step, roughness);
                }
            }

            for (int x = step; x < chunkSize - 1; x += previousStep)
            {
                for (int y = step; y < chunkSize - 1; y += previousStep)
                {
                    SquareStepKickoff(heightMap, chunkSize, x, y, step, roughness);
                    SquareStepKickoff(heightMap, chunkSize, x, y, step, roughness);
                    SquareStepKickoff(heightMap, chunkSize, x, y, step, roughness);
                }
            }

            previousStep = step;
            step /= 2;
            roughness *= falloff;
        }

        return heightMap;
    }

    private static float GetHeight(int x, int y)
    {
        float amplitude = 1.0f;
        float frequency = 1.0f;
        float noiseHeight = 0.0f;

        for (int i = 0; i < numNoiseOctaves; ++i)
        {
            float sampleX = (x + octaveOffsets[i].x) / noiseScale * frequency;
            float sampleY = (y + octaveOffsets[i].y) / noiseScale * frequency;

            // Perlin noise gets the same value each time if the arguments passed are integer values.
            float perlinValue = noise.cnoise(new float2(sampleX, sampleY)) + 0.5f;
            noiseHeight += perlinValue * amplitude;

            amplitude *= persistence; // Persistence should be between 0 and 1 - amplitude decreases with each octave.
            frequency *= lacunarity; // Lacunarity should be greater than 1 - frequency increases with each octave.
        }

        return noiseHeight;
    }

    private static void DiamondStep(float[,] array, int x, int y, int step, float roughness)
    {
        float runningSum = 0;

        runningSum += array[x - step, y - step];

        runningSum += array[x + step, y - step];

        runningSum += array[x - step, y + step];

        runningSum += array[x + step, y + step];

        float
            value = runningSum /
                    4f; // + roughness / ((float) seededGenerator.NextDouble() * seededGenerator.Next(40, 50));// + roughness / (((float) seededGenerator.NextDouble() - 0.5f) * 2f * seededGenerator.Next(20, 30));// + ((float) seededGenerator.NextDouble() + 1.0f) * 2f) / 3f;
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

        float
            value = runningSum /
                    count; // + roughness / ((float) seededGenerator.NextDouble() * seededGenerator.Next(40, 50));// + ((float) seededGenerator.NextDouble() - 0.5f) * 2f) / 3f;
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