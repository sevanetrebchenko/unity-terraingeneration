using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct PerlinNoiseChunkHeightMapJob : IJob
{
    [ReadOnly] public int chunkSize;
    [ReadOnly] public int terrainSeed;
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
                    frequency *= lacunarity; // Lacunarity should be greater than 1 - frequency increases with each octave.
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

[BurstCompile]
public struct TerrainMeshGenerationJob : IJobParallelFor
{
    // Marching cube configuration
    [ReadOnly] public NativeArray<int> cornerTable;
    [ReadOnly] public NativeArray<int> edgeTable;
    [ReadOnly] public NativeArray<int> triangleTable;

    [NativeDisableParallelForRestriction, WriteOnly] public NativeArray<float3> meshVertices;
    [NativeDisableParallelForRestriction, WriteOnly] public NativeArray<int> numElements;
    
    [ReadOnly] public NativeArray<float> terrainHeightMap;
    public float terrainSurfaceLevel;
    public bool terrainSmoothing;

    public int chunkSize;

    public void Execute(int index)
    {
        NativeArray<float> cubeCornerValues = new NativeArray<float>(8, Allocator.Temp);
        // Construct cube with noise values.
        int3 normalizedCubePosition = PointFromIndex(index);
        cubeCornerValues[0] = terrainHeightMap[IndexFromCoordinate(normalizedCubePosition.x, normalizedCubePosition.y, normalizedCubePosition.z)];
        cubeCornerValues[1] = terrainHeightMap[IndexFromCoordinate(normalizedCubePosition.x + 1, normalizedCubePosition.y, normalizedCubePosition.z)];
        cubeCornerValues[2] = terrainHeightMap[IndexFromCoordinate(normalizedCubePosition.x + 1, normalizedCubePosition.y + 1, normalizedCubePosition.z)];
        cubeCornerValues[3] = terrainHeightMap[IndexFromCoordinate(normalizedCubePosition.x, normalizedCubePosition.y + 1, normalizedCubePosition.z)];
        cubeCornerValues[4] = terrainHeightMap[IndexFromCoordinate(normalizedCubePosition.x, normalizedCubePosition.y, normalizedCubePosition.z + 1)];
        cubeCornerValues[5] = terrainHeightMap[IndexFromCoordinate(normalizedCubePosition.x + 1, normalizedCubePosition.y, normalizedCubePosition.z + 1)];
        cubeCornerValues[6] = terrainHeightMap[IndexFromCoordinate(normalizedCubePosition.x + 1, normalizedCubePosition.y + 1, normalizedCubePosition.z + 1)];
        cubeCornerValues[7] = terrainHeightMap[IndexFromCoordinate(normalizedCubePosition.x, normalizedCubePosition.y + 1, normalizedCubePosition.z + 1)];

        // March cube.
        int configuration = GetCubeConfiguration(cubeCornerValues);

        if (configuration == 0 || configuration == 255)
        {
            return;
        }

        int numWrittenElements = 0;
        int edgeIndex = 0;
        bool breakOut = false;
        
        // A configuration has maximum 5 triangles in it.
        for (int i = 0; i < 5; ++i) {
            // A configuration element (triangle) consists of 3 points.
            for (int j = 0; j < 3; ++j) {
                int triangleIndex = triangleTable[configuration * 16 + edgeIndex];

                // Reached the end of this configuration.
                if (triangleIndex == -1)
                {
                    breakOut = true;
                    break;
                }

                int edgeVertex1Index = triangleIndex * 2 + 0;
                int edgeVertex2Index = triangleIndex * 2 + 1;

                int corner1Index = edgeTable[edgeVertex1Index] * 3;
                int corner2Index = edgeTable[edgeVertex2Index] * 3;
                
                int3 corner1 = new int3(cornerTable[corner1Index + 0], cornerTable[corner1Index + 1], cornerTable[corner1Index + 2]);
                int3 corner2 = new int3(cornerTable[corner2Index + 0], cornerTable[corner2Index + 1], cornerTable[corner2Index + 2]);
                
                float3 edgeVertex1 = normalizedCubePosition + corner1;
                float3 edgeVertex2 = normalizedCubePosition + corner2;

                float3 vertexPosition;

                if (terrainSmoothing) {
                    float edgeVertex1Noise = cubeCornerValues[edgeTable[edgeVertex1Index]];
                    float edgeVertex2Noise = cubeCornerValues[edgeTable[edgeVertex2Index]];

                    vertexPosition = Interpolate(edgeVertex1, edgeVertex1Noise, edgeVertex2, edgeVertex2Noise);
                }
                else {
                    vertexPosition = (edgeVertex1 + edgeVertex2) / 2.0f;
                }

                meshVertices[index * 15 + numWrittenElements] = vertexPosition;
                ++numWrittenElements;
                ++edgeIndex;
            }

            if (breakOut)
            {
                break;
            }
        }

        numElements[index] = numWrittenElements;
        cubeCornerValues.Dispose();
    }

    float3 Interpolate(float3 vertex1, float vertex1Value, float3 vertex2, float vertex2Value)
    {
        float t = (terrainSurfaceLevel - vertex1Value) / (vertex2Value - vertex1Value);
        float3 vert = vertex1 + t * (vertex2 - vertex1);
        return vert;
    }
    
    int3 PointFromIndex(int index)
    {
        return new int3(index % (chunkSize - 1), index / ((chunkSize - 1) * (chunkSize - 1)), (index / (chunkSize - 1)) % (chunkSize - 1));
    }

    int IndexFromCoordinate(int x, int y, int z)
    {
        return x + y * (chunkSize) * (chunkSize) + z * (chunkSize);
    }

    int GetCubeConfiguration(NativeArray<float> cubeCornerValues)
    {
        int configuration = 0;

        for (int i = 0; i < 8; ++i) {
            if (cubeCornerValues[i] > terrainSurfaceLevel) {
                configuration |= 1 << i;
            }
        }
        
        return configuration;
    }
}
