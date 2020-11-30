using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class TerrainChunkConnector
{
    private NativeArray<float> first;
    private NativeArray<float> second;
    private NativeArray<float> third;
    private NativeArray<float> fourth;

    private NativeArray<float> combined;

    private NativeArray<float3> vertices;
    private NativeArray<int> numElements;

    private int chunkSize;
    private Vector3 worldPosition;
    private int numConnections;
    private int3 numNodesPerAxis;
    private int3 axisDimensionsInCubes;
    private int totalNumNodes;
    private int totalNumCubes;
    
    // Components
    private GameObject gameObject;
    private MeshFilter meshFilter;
    private MeshCollider meshCollider;
    
    // Terrain chunk connector between two chunk sides
    public TerrainChunkConnector(int numNodesPerAxis, Vector3 worldPosition, NativeArray<float> chunkSide1, NativeArray<float> chunkSide2)
    {
        numConnections = 2;
        first = chunkSide1;
        second = chunkSide2;
        
        chunkSize = numNodesPerAxis;
        this.worldPosition = worldPosition;
        this.numNodesPerAxis = new int3(2, numNodesPerAxis, numNodesPerAxis);
        totalNumNodes = numNodesPerAxis * numNodesPerAxis * 2; // 2 sides
        axisDimensionsInCubes = new int3(1, numNodesPerAxis - 1, numNodesPerAxis - 1);
        totalNumCubes = (numNodesPerAxis - 1) * (numNodesPerAxis - 1) * 2;
        
        combined = new NativeArray<float>(totalNumNodes, Allocator.Persistent);

        // Emplace data from first edge.
        for (int z = 0; z < this.numNodesPerAxis.z; ++z)
        {
            for (int y = 0; y < this.numNodesPerAxis.y; ++y)
            {
                int mapIndex = z * this.numNodesPerAxis.y + y;
                int index = this.numNodesPerAxis.x * z + this.numNodesPerAxis.z * this.numNodesPerAxis.x * y; // First edge, no additional offset into the x.
                combined[index] = chunkSide1[mapIndex];
            }
        }
        
        // Emplace data from second edge.
        for (int z = 0; z < this.numNodesPerAxis.z; ++z)
        {
            for (int y = 0; y < this.numNodesPerAxis.y; ++y)
            {
                int mapIndex = z * this.numNodesPerAxis.y + y;// + this.numNodesPerAxis.z * y;
                int index = 1 + this.numNodesPerAxis.x * z + this.numNodesPerAxis.z * this.numNodesPerAxis.x * y; // Second edge, needs additional offset into the x.
                combined[index] = chunkSide2[mapIndex];
            }
        }
        
        vertices = new NativeArray<float3>(totalNumCubes * 15, Allocator.Persistent);
        numElements = new NativeArray<int>(1, Allocator.Persistent);
        
        // Add components.
        gameObject = new GameObject {
            name = "Terrain Chunk"
        };
        gameObject.transform.position = worldPosition;
        gameObject.transform.parent = null;
        gameObject.isStatic = true;

        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshCollider = gameObject.AddComponent<MeshCollider>();
        MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = new Material(Shader.Find("Diffuse"));
        SetLayer(8);
    }

    public TerrainChunkConnector(int numNodesPerAxis, NativeArray<float> chunkCorner1, NativeArray<float> chunkCorner2, NativeArray<float> chunkCorner3, NativeArray<float> chunkCorner4)
    {
        numConnections = 4;
        first = chunkCorner1;
        second = chunkCorner2;
        third = chunkCorner3;
        fourth = chunkCorner4;
        
        this.numNodesPerAxis = new int3(2, numNodesPerAxis, 2);
        totalNumNodes = numNodesPerAxis * 4; // 4 edges
        axisDimensionsInCubes = new int3(1, numNodesPerAxis - 1, 1);
        totalNumCubes = (numNodesPerAxis - 1);
        
        combined = new NativeArray<float>(totalNumNodes, Allocator.Persistent); 
        vertices = new NativeArray<float3>(totalNumCubes * 15, Allocator.Persistent);
        numElements = new NativeArray<int>(1, Allocator.Persistent);
    }

    public JobHandle GenerateConnectorMesh()
    {
        // Establish the map to write to.
        MeshGenerationJob meshGenerationJob = new MeshGenerationJob
        {
            cornerTable = MarchingCubes.cornerTable,
            edgeTable = MarchingCubes.edgeTable,
            triangleTable = MarchingCubes.triangleTable,
            vertices = vertices,
            numElements = numElements,
            terrainHeightMap = combined,
            terrainSurfaceLevel = 0.0f, // TODO
            terrainSmoothing = false, // TODO
            axisDimensionsInCubes = axisDimensionsInCubes,
            numNodesPerAxis = numNodesPerAxis
        };

        return meshGenerationJob.Schedule();
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

    public void OnGizmos()
    {
        for (int x = 0; x < numNodesPerAxis.x; ++x)
        {
            for (int z = 0; z < numNodesPerAxis.z; ++z)
            {
                for (int y = 0; y < numNodesPerAxis.y; ++y)
                {
                    int index = x + numNodesPerAxis.x * z + numNodesPerAxis.z * numNodesPerAxis.x * y;
                    float noiseValue = combined[index];

                    if (noiseValue < 0.0f)
                    {
                        Gizmos.color = Color.black;
                        Gizmos.DrawSphere(worldPosition + new Vector3(x, y, z), 0.1f);
                    }

                }
            }
        }
    }
    
    public void SetLayer(int layer) {
        gameObject.layer = layer;
    }
    
}
