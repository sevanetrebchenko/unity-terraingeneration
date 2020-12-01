using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;

[BurstCompile]
public struct MeshGenerationJob : IJob
{
    // Marching cube configuration
    [ReadOnly] public NativeArray<int> cornerTable;
    [ReadOnly] public NativeArray<int> edgeTable;
    [ReadOnly] public NativeArray<int> triangleTable;

    [WriteOnly] public NativeArray<float3> vertices;
    [WriteOnly] public NativeArray<int> numElements;
    
    [ReadOnly] public NativeArray<float> terrainHeightMap;
    public float terrainSurfaceLevel;
    public bool terrainSmoothing;

    public int3 axisDimensionsInCubes;
    public int3 numNodesPerAxis;

    public void Execute()
    {
        NativeArray<float> cubeCornerValues = new NativeArray<float>(8, Allocator.Temp);

        int index = 0;
        for (int x = 0; x < axisDimensionsInCubes.x; ++x)
        {
            for (int z = 0; z < axisDimensionsInCubes.z; ++z)
            {
                for (int y = 0; y < axisDimensionsInCubes.y; ++y)
                {
                    // Construct cube with noise values.
                    int3 normalizedCubePosition = new int3(x, y, z);
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
                        continue;
                    }

                    int edgeIndex = 0;
                    bool breakOut = false;
                    
                    // A configuration has maximum 5 triangles in it.
                    for (int i = 0; i < 5; ++i) {
                        if (breakOut)
                        {
                            break;
                        }
                        
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

                            float3 vertexPosition = (edgeVertex1 + edgeVertex2) / 2.0f;
                            //
                            // if (terrainSmoothing) {
                            //     float edgeVertex1Noise = cubeCornerValues[edgeTable[edgeVertex1Index]];
                            //     float edgeVertex2Noise = cubeCornerValues[edgeTable[edgeVertex2Index]];
                            //
                            //     vertexPosition = Interpolate(edgeVertex1, edgeVertex1Noise, edgeVertex2, edgeVertex2Noise);
                            // }
                            // else {
                                // vertexPosition = (edgeVertex1 + edgeVertex2) / 2.0f;
                            // }

                            vertices[index++] = vertexPosition;
                            ++edgeIndex;
                        }
                    }
                }
            }
        }

        numElements[0] = index; // numElements only has 1 element.
        cubeCornerValues.Dispose();
    }

    float3 Interpolate(float3 vertex1, float vertex1Value, float3 vertex2, float vertex2Value)
    {
        float t = (terrainSurfaceLevel - vertex1Value) / (vertex2Value - vertex1Value);
        float3 vert = vertex1 + t * (vertex2 - vertex1);
        return vert;
    }

    int IndexFromCoordinate(int x, int y, int z)
    {
        return x + z * numNodesPerAxis.x + y * numNodesPerAxis.x * numNodesPerAxis.z;
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

