using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;
using Unity.Burst;

public class TerrainChunk {
    // Chunk mesh.
    private GameObject gameObject;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;

    private NativeArray<float> heightMap;
    private int chunkSize;
    private float terrainSurfaceLevel;
    private bool terrainSmoothing;
    private int totalNumCubes;

    public TerrainChunk(NativeArray<float> heightMap, int chunkSize, Vector3Int chunkPosition, float terrainSurfaceLevel, bool terrainSmoothing, Transform parentTransform)
    {
        this.chunkSize = chunkSize;
        this.heightMap = heightMap;
        this.terrainSurfaceLevel = terrainSurfaceLevel;
        this.terrainSmoothing = terrainSmoothing;
        totalNumCubes = (chunkSize - 1) * (chunkSize - 1) * (chunkSize - 1);
        Debug.Log("total num cubes: " + 15 * totalNumCubes);
        
        NativeArray<float3> meshVertices = new NativeArray<float3>(15 * totalNumCubes, Allocator.TempJob);
        NativeArray<int> numElementsPerCube = new NativeArray<int>(totalNumCubes, Allocator.TempJob);

        JobHandle handle = GenerateTerrainMesh(meshVertices, numElementsPerCube);
        handle.Complete();    
        
        // Create mesh generator.
        gameObject = new GameObject {
            name = "Terrain Chunk"
        };
        gameObject.transform.position = chunkPosition;
        gameObject.transform.parent = parentTransform;

        // Add mesh generator components.
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshCollider = gameObject.AddComponent<MeshCollider>();

        Mesh mesh = new Mesh();

        // Get the total number of vertices added to the mesh and the indices in the mesh array.
        int totalNumElements = 0;
        List<(int, int)> filledCubes = new List<(int, int)>();
        for (int i = 0; i < numElementsPerCube.Length; ++i)
        {
            int numElements = numElementsPerCube[i];
            if (numElements != 0)
            {
                filledCubes.Add((i, numElements));
                if (numElements % 3 != 0)
                {
                    Debug.DebugBreak();
                }
                totalNumElements += numElements;
            }
        }
        
        // Allocate mesh data.
        Vector3[] vertices = new Vector3[totalNumElements];
        int[] indices = new int[totalNumElements];

        // Transfer data over.
        int counter = 0;
        for (int i = 0; i < filledCubes.Count; ++i)
        {
            (int, int) currentCubeMeshData = filledCubes[i];
            int offset = currentCubeMeshData.Item1;
            
            // Copy over elements.
            for (int j = 0; j < currentCubeMeshData.Item2; ++j)
            {
                float3 meshVertex = meshVertices[offset * 15 + j];
                vertices[counter] = new Vector3(meshVertex.x, meshVertex.y, meshVertex.z) + chunkPosition * chunkSize;
                indices[counter] = counter;

                ++counter;
            }
        }

        // Create mesh.
        mesh.vertices = vertices;
        mesh.triangles = indices;
        mesh.RecalculateNormals();

        meshFilter.mesh = mesh;
        meshCollider.sharedMesh = mesh;

        meshVertices.Dispose();
        numElementsPerCube.Dispose();
    }

    private JobHandle GenerateTerrainMesh(NativeArray<float3> meshVertices, NativeArray<int> numElements)
    {
        TerrainMeshGenerationJob terrainMeshGenerationJob = new TerrainMeshGenerationJob()
        {
            cornerTable = MarchingCubesConfiguration.cornerTable,
            edgeTable = MarchingCubesConfiguration.edgeTable,
            triangleTable = MarchingCubesConfiguration.triangleTable,
            meshVertices = meshVertices,
            numElements = numElements,
            terrainHeightMap = heightMap,
            terrainSurfaceLevel = terrainSurfaceLevel,
            terrainSmoothing = terrainSmoothing,
            chunkSize = chunkSize
        };
        return terrainMeshGenerationJob.Schedule(totalNumCubes, 15);
    }

    public void UpdateTerrainChunk(Vector3 viewerPosition, float maxViewDistance) {
        // // Calculate the closest point on the chunk bounding cube to the position of the viewer.
        // float distanceToNearestPointSquared = chunkBounds.SqrDistance(viewerPosition);
        //
        // // The chunk is visible if the closest point on this chunk is within the maximum viewer view distance.
        // bool chunkIsVisible = distanceToNearestPointSquared <= (maxViewDistance * maxViewDistance);
        //
        // SetVisible(chunkIsVisible);
    }

    public void SetLayer(int layer) {
        gameObject.layer = layer;
    }

    public void InputTriggered(Vector3Int hitCubePosition, bool place, int miningRadius) {
        //
        // int numCubesPerSide = chunkSize;
        //
        // for (int z = 0; z < numCubesPerSide; ++z) {
        //     for (int y = 0; y < numCubesPerSide; ++y) {
        //         for (int x = 0; x < numCubesPerSide; ++x) {
        //             Vector3Int currentCubePosition = new Vector3Int(x, y, z);
        //             float distance = Vector3.Distance(hitCubePosition, currentCubePosition);
        //
        //             // Found a position that is affected by this mining radius.
        //             if (distance <= miningRadius) {
        //                 // Affect terrain value based on parameters.
        //                 float terrainValueModification = Mathf.Lerp(0.08f, 0.0001f, distance / (float)miningRadius);
        //                 ref float terrainValue = ref heightMap[currentCubePosition.x, currentCubePosition.y, currentCubePosition.z];
        //
        //                 if (place) {
        //                     terrainValue -= terrainValueModification;
        //
        //                     if (terrainValue < 0) {
        //                         terrainValue = 0;
        //                     }
        //                 }
        //                 else {
        //                     terrainValue += terrainValueModification;
        //
        //                     if (terrainValue > 1) {
        //                         terrainValue = 1;
        //                     }
        //                 }
        //
        //                 // Add all adjacent blocks (removing duplicates).
        //                 for (int zAdjacent = -1; zAdjacent < 2; ++zAdjacent) {
        //                     for (int yAdjacent = -1; yAdjacent < 2; ++yAdjacent) {
        //                         for (int xAdjacent = -1; xAdjacent < 2; ++xAdjacent) {
        //                             Vector3Int adjacentCube = currentCubePosition + new Vector3Int(xAdjacent, yAdjacent, zAdjacent);
        //
        //                             if (adjacentCube.x < numCubesPerSide && adjacentCube.x >= 0 && adjacentCube.y < numCubesPerSide && adjacentCube.y >= 0 && adjacentCube.z < numCubesPerSide && adjacentCube.z >= 0) {
        //                                 remarchCubePositionList.Add(adjacentCube);
        //                             }
        //                         }
        //                     }
        //                 }
        //
        //             }
        //         }
        //     }
        // }
    }

    // Regenerate the mesh if the terrain generator or chunk size was changed.
    public void SetVisible(bool chunkIsVisible) {
        gameObject.SetActive(chunkIsVisible);
    }

    public bool IsVisible() {
        return gameObject.activeSelf;
    }
}

public struct TerrainMeshGenerationJob : IJobParallelFor
{
    // Marching cube configuration
    [ReadOnly] public NativeArray<int> cornerTable;
    [ReadOnly] public NativeArray<int> edgeTable;
    [ReadOnly] public NativeArray<int> triangleTable;

    [NativeDisableParallelForRestriction, WriteOnly] public NativeArray<float3> meshVertices;
    [WriteOnly] public NativeArray<int> numElements;
    
    // Terrain height map
    [ReadOnly] public NativeArray<float> terrainHeightMap;
    public float terrainSurfaceLevel;
    public bool terrainSmoothing;

    public int chunkSize;

    public void Execute(int index)
    {
        // Construct cube with noise values.
        int3 normalizedCubePosition = PointFromIndex(index);
        NativeArray<float> cubeCornerValues = new NativeArray<float>(8, Allocator.Temp)
        {
            [0] = terrainHeightMap[IndexFromCoordinate(normalizedCubePosition.x, normalizedCubePosition.y, normalizedCubePosition.z)],
            [1] = terrainHeightMap[IndexFromCoordinate(normalizedCubePosition.x + 1, normalizedCubePosition.y, normalizedCubePosition.z)],
            [2] = terrainHeightMap[IndexFromCoordinate(normalizedCubePosition.x, normalizedCubePosition.y, normalizedCubePosition.z + 1)],
            [3] = terrainHeightMap[IndexFromCoordinate(normalizedCubePosition.x + 1, normalizedCubePosition.y, normalizedCubePosition.z + 1)],
            [4] = terrainHeightMap[IndexFromCoordinate(normalizedCubePosition.x, normalizedCubePosition.y + 1, normalizedCubePosition.z)],
            [5] = terrainHeightMap[IndexFromCoordinate(normalizedCubePosition.x + 1, normalizedCubePosition.y + 1, normalizedCubePosition.z)],
            [6] = terrainHeightMap[IndexFromCoordinate(normalizedCubePosition.x, normalizedCubePosition.y + 1, normalizedCubePosition.z + 1)],
            [7] = terrainHeightMap[IndexFromCoordinate(normalizedCubePosition.x + 1, normalizedCubePosition.y + 1, normalizedCubePosition.z + 1)]
        };
        
        // March cube.
        int configuration = GetCubeConfiguration(cubeCornerValues);

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

                int3 corner1 = new int3(cornerTable[edgeTable[triangleIndex * 2 + 0] + 0], cornerTable[edgeTable[triangleIndex * 2 + 0] + 1], cornerTable[edgeTable[triangleIndex * 2 + 0] + 2]);
                int3 corner2 = new int3(cornerTable[edgeTable[triangleIndex * 2 + 1] + 3], cornerTable[edgeTable[triangleIndex * 2 + 1] + 4], cornerTable[edgeTable[triangleIndex * 2 + 1] + 5]);
                
                float3 edgeVertex1 = normalizedCubePosition + corner1;
                float3 edgeVertex2 = normalizedCubePosition + corner2;

                float3 vertexPosition;

                if (terrainSmoothing) {
                    float edgeVertex1Noise = cubeCornerValues[edgeTable[triangleIndex * 3 + 0]];
                    float edgeVertex2Noise = cubeCornerValues[edgeTable[triangleIndex * 3 + 1]];
                    
                    float diff = edgeVertex2Noise - edgeVertex1Noise;
                    
                    if (diff == 0) {
                        diff = terrainSurfaceLevel;
                    }
                    else {
                        diff = (terrainSurfaceLevel - edgeVertex1Noise) / diff;
                    }
                    
                    vertexPosition = edgeVertex1 + ((edgeVertex2 - edgeVertex1) * diff);
                }
                else {
                    vertexPosition = (edgeVertex1 + edgeVertex2) / 2.0f;
                }

                meshVertices[index * 15 + numWrittenElements] = vertexPosition;
                numWrittenElements++;
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
    
    int3 PointFromIndex(int index)
    {
        return new int3(index % chunkSize, index / (chunkSize * chunkSize), (index / chunkSize) % chunkSize);
    }

    int IndexFromCoordinate(int x, int y, int z)
    {
        return x + y * chunkSize * chunkSize + z * chunkSize;
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


