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

    public NativeArray<float> heightMap;
    private int chunkSize;
    private float terrainSurfaceLevel;
    private bool terrainSmoothing;
    private int totalNumCubes;
    private Vector3 chunkWorldPosition;

    private NativeArray<float3> meshVertices;
    private NativeArray<int> numElementsPerCube;

    public TerrainChunk(int chunkSize, Vector3 chunkPosition, float terrainSurfaceLevel, bool terrainSmoothing, Transform parentTransform)
    {
        this.chunkSize = chunkSize;
        this.heightMap = new NativeArray<float>(chunkSize * chunkSize * chunkSize, Allocator.Persistent);
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
        TerrainMeshGenerationJob terrainMeshGenerationJob = new TerrainMeshGenerationJob
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


