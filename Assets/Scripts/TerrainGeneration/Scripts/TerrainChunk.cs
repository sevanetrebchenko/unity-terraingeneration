using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;
using Unity.Burst;

public class TerrainChunk
{
    public NativeArray<float3> vertices;
    public NativeArray<int> numElements;

    public Vector3Int chunkPosition;
    public NativeArray<float> heightMap;

    private int numNodesPerAxis;
    private int numCubes;
    
    // Components
    private GameObject gameObject;
    private MeshFilter meshFilter;
    private MeshCollider meshCollider;

    public TerrainChunk(int numNodesPerAxis, int numCubes, Vector3Int chunkPosition, NativeArray<float> heightMap, Transform parentTransform)
    {
        this.numNodesPerAxis = numNodesPerAxis;
        this.numCubes = numCubes;
        this.chunkPosition = chunkPosition;
        this.heightMap = heightMap;
        
        // Allocate space for mesh components.
        // Each cube can have a maximum of 15 vertices.
        int totalNumElements = numCubes * numCubes * numCubes * 15;
        vertices = new NativeArray<float3>(totalNumElements, Allocator.Persistent);
        numElements = new NativeArray<int>(1, Allocator.Persistent);

        // Add components.
        gameObject = new GameObject {
            name = "Terrain Chunk"
        };
        gameObject.transform.position = chunkPosition * numNodesPerAxis;
        gameObject.transform.parent = parentTransform;
        gameObject.isStatic = true;

        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshCollider = gameObject.AddComponent<MeshCollider>();
        MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = new Material(Shader.Find("Diffuse"));
        SetLayer(8);
    }

    public void OnGizmos()
    {
        for (int x = 0; x < numNodesPerAxis; ++x)
        {
            for (int z = 0; z < numNodesPerAxis; ++z)
            {
                for (int y = 0; y < numNodesPerAxis; ++y)
                {
                    int index = x + z * numNodesPerAxis + y * numNodesPerAxis * numNodesPerAxis;
                    float noiseValue = heightMap[index];
                    
                    Gizmos.color = new Color(noiseValue, noiseValue, noiseValue);
                    Gizmos.DrawSphere(chunkPosition * numNodesPerAxis + new Vector3Int(x, y, z), 0.1f);
                }
            }
        }
    }
    
    public void OnDestroy()
    {
        heightMap.Dispose();
        vertices.Dispose();
        numElements.Dispose();
    }

    public void ConstructMesh()
    {
        Mesh mesh = new Mesh();

        // Allocate mesh data.
        int size = numElements[0];
        Vector3[] meshVertices = new Vector3[size];
        int[] triangles = new int[size];

        // Transfer data over.
        for (int i = 0; i < size; ++i)
        {
            meshVertices[i] = vertices[i];
            triangles[i] = i;
        }

        // Create mesh.
        mesh.vertices = meshVertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        meshFilter.mesh = mesh;
        meshCollider.sharedMesh = mesh;
    }

    public void SetLayer(int layer) {
        gameObject.layer = layer;
    }
}


