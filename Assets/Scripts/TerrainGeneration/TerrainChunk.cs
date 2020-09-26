using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;
using Unity.Burst;

public class TerrainChunk
{
    // Chunk mesh.
    private GameObject gameObject;
    private MeshFilter meshFilter;
    private MeshCollider meshCollider;

    private NativeArray<float> heightMap;
    private int chunkSize;
    private float terrainSurfaceLevel;
    private bool terrainSmoothing;
    private int totalNumCubes;
    private Vector3 chunkWorldPosition;

    private NativeArray<float3> meshVertices;
    private NativeArray<int> numElementsPerCube;

    public TerrainChunk(NativeArray<float> heightMap, int chunkSize, Vector3 chunkPosition, float terrainSurfaceLevel, bool terrainSmoothing, Transform parentTransform)
    {
        this.chunkSize = chunkSize;
        this.heightMap = heightMap;
        this.terrainSurfaceLevel = terrainSurfaceLevel;
        this.terrainSmoothing = terrainSmoothing;
        totalNumCubes = (chunkSize - 1) * (chunkSize - 1) * (chunkSize - 1);
        
        chunkWorldPosition = chunkPosition * chunkSize;
        
        meshVertices = new NativeArray<float3>(15 * totalNumCubes, Allocator.Persistent);
        numElementsPerCube = new NativeArray<int>(totalNumCubes, Allocator.Persistent);

        // Create mesh generator.
        gameObject = new GameObject {
            name = "Terrain Chunk"
        };
        gameObject.transform.position = chunkPosition;
        gameObject.transform.parent = parentTransform;
        gameObject.isStatic = true;

        // Add mesh generator components.
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshCollider = gameObject.AddComponent<MeshCollider>();
        MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = new Material(Shader.Find("Diffuse"));
        SetLayer(8);
    }

    public void OnDestroy()
    {
        heightMap.Dispose();
    }

    public JobHandle Schedule()
    {
        return GenerateTerrainMesh(meshVertices, numElementsPerCube);
    }

    public void OnDrawGizmos()
    {
        for (int y = 0; y < chunkSize; ++y)
        {
            for (int x = 0; x < chunkSize; ++x)
            {
                for (int z = 0; z < chunkSize; ++z)
                {
                    float heightValue = heightMap[x + y * (chunkSize) * (chunkSize) + z * (chunkSize)];
                    if (heightValue > terrainSurfaceLevel)
                    {
                        Gizmos.color = Color.white;
                    }
                    else
                    {
                        Gizmos.color = Color.black;
                    }
                    
                    Gizmos.DrawSphere(new Vector3(x, y, z), 0.2f);
                }
            }
        }
    }

    public void ConstructMesh()
    {
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
            
            // Copy over elements.
            for (int j = 0; j < currentCubeMeshData.Item2; ++j)
            {
                float3 meshVertex = meshVertices[currentCubeMeshData.Item1 * 15 + j];
                vertices[counter] = new Vector3(meshVertex.x, meshVertex.y, meshVertex.z) + chunkWorldPosition; // Covert mesh vertices to world position.
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
        
        // Each job marches one layer of the chunk.
        return terrainMeshGenerationJob.Schedule(totalNumCubes, (chunkSize) * (chunkSize));
    }

    public void SetLayer(int layer) {
        gameObject.layer = layer;
    }
}

//[BurstCompile]
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


