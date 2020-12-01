using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class TerrainChunkConnector
{
    private NativeArray<float> combined;
    private NativeArray<float3> vertices;
    private NativeArray<int> numElements;

    private int chunkSize;
    private Vector3 worldPosition;
    private int3 numNodesPerAxis;
    private int3 axisDimensionsInCubes;

    // Components
    private GameObject gameObject;
    private MeshFilter meshFilter;
    private MeshCollider meshCollider;
    
    
     public TerrainChunkConnector(int numNodesPerAxis, Vector3 worldPosition, NativeArray<float> chunkSide1)
    {
        chunkSize = numNodesPerAxis;
        this.worldPosition = worldPosition;
        this.numNodesPerAxis = new int3(1, numNodesPerAxis, numNodesPerAxis);
        int totalNumNodes = numNodesPerAxis * numNodesPerAxis * 1;
        axisDimensionsInCubes = new int3(1, numNodesPerAxis - 1, numNodesPerAxis - 1);
        int totalNumCubes = (numNodesPerAxis - 1) * (numNodesPerAxis - 1) * 2;
        
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

        vertices = new NativeArray<float3>(totalNumCubes * 15, Allocator.Persistent);
        numElements = new NativeArray<int>(1, Allocator.Persistent);
        
        // Add components.
        gameObject = new GameObject {
            name = "Terrain Connector"
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
    
    
    
    // Terrain chunk connector between two chunk sides
    public TerrainChunkConnector(int numNodesPerAxis, Vector3 worldPosition, Vector3 worldRotation, NativeArray<float> chunkSide1, NativeArray<float> chunkSide2, Transform parentTransform)
    {
        chunkSize = numNodesPerAxis;
        this.worldPosition = worldPosition;
        this.numNodesPerAxis = new int3(2, numNodesPerAxis, numNodesPerAxis);
        int totalNumNodes = numNodesPerAxis * numNodesPerAxis * 2;
        axisDimensionsInCubes = new int3(1, numNodesPerAxis - 1, numNodesPerAxis - 1);
        int totalNumCubes = (numNodesPerAxis - 1) * (numNodesPerAxis - 1) * 2;
        
        combined = new NativeArray<float>(totalNumNodes, Allocator.Persistent);

        // Emplace data from first edge.
        for (int z = 0; z < this.numNodesPerAxis.z; ++z)
        {
            for (int y = 0; y < this.numNodesPerAxis.y; ++y)
            {
                int mapIndex = z * this.numNodesPerAxis.y + y;
                int index = 1 + this.numNodesPerAxis.x * z + this.numNodesPerAxis.z * this.numNodesPerAxis.x * y; // First edge, no additional offset into the x.
                combined[index] = chunkSide1[mapIndex];
            }
        }
        
        // Emplace data from second edge.
        for (int z = 0; z < this.numNodesPerAxis.z; ++z)
        {
            for (int y = 0; y < this.numNodesPerAxis.y; ++y)
            {
                int mapIndex = z * this.numNodesPerAxis.y + y;// + this.numNodesPerAxis.z * y;
                int index = this.numNodesPerAxis.x * z + this.numNodesPerAxis.z * this.numNodesPerAxis.x * y; // Second edge, needs additional offset into the x.
                combined[index] = chunkSide2[mapIndex];
            }
        }
        
        vertices = new NativeArray<float3>(totalNumCubes * 15, Allocator.Persistent);
        numElements = new NativeArray<int>(1, Allocator.Persistent);
        
        // Add components.
        gameObject = new GameObject {
            name = "Terrain Connector"
        };
        gameObject.transform.position = worldPosition;
        gameObject.transform.Rotate(worldRotation, Space.Self);
        gameObject.transform.parent = parentTransform;
        gameObject.isStatic = true;

        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshCollider = gameObject.AddComponent<MeshCollider>();
        MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = new Material(Shader.Find("Diffuse"));
        SetLayer(8);
    }

    public TerrainChunkConnector(int numNodesPerAxis, Vector3 worldPosition, Vector3 worldRotation,  NativeArray<float> topLeftCorner, NativeArray<float> topRightCorner, NativeArray<float> bottomLeftCorner, NativeArray<float> bottomRightCorner, Transform parentTransform)
    {
        chunkSize = numNodesPerAxis;
        this.worldPosition = worldPosition;
        this.numNodesPerAxis = new int3(2, numNodesPerAxis, 2);
        int totalNumNodes = numNodesPerAxis * 4;
        axisDimensionsInCubes = new int3(1, numNodesPerAxis - 1, 1);
        int totalNumCubes = (numNodesPerAxis - 1);
        
        combined = new NativeArray<float>(totalNumNodes, Allocator.Persistent);

        int mapIndex = 0;
        
        // Emplace data from top left corner
        for (int y = 0; y < this.numNodesPerAxis.y; ++y)
        {
            int index = this.numNodesPerAxis.x + this.numNodesPerAxis.z * this.numNodesPerAxis.x * y;
            combined[index] = topLeftCorner[mapIndex++];
        }
        
        // Emplace data from top right corner.
        mapIndex = 0;
        for (int y = 0; y < this.numNodesPerAxis.y; ++y)
        {
            int index = 1 + this.numNodesPerAxis.x + this.numNodesPerAxis.z * this.numNodesPerAxis.x * y;
            combined[index] = topRightCorner[mapIndex++];
        }
        
        // Emplace data from bottom left corner.
        mapIndex = 0;
        for (int y = 0; y < this.numNodesPerAxis.y; ++y)
        {
            int index = this.numNodesPerAxis.z * this.numNodesPerAxis.x * y;
            combined[index] = bottomLeftCorner[mapIndex++];
        }
        
        // Emplace data from bottom right corner.
        mapIndex = 0;
        for (int y = 0; y < this.numNodesPerAxis.y; ++y)
        {
            int index = 1 + this.numNodesPerAxis.z * this.numNodesPerAxis.x * y;
            combined[index] = bottomRightCorner[mapIndex++];
        }
        
        vertices = new NativeArray<float3>(totalNumCubes * 15, Allocator.Persistent);
        numElements = new NativeArray<int>(1, Allocator.Persistent);
        
        // Add components.
        gameObject = new GameObject {
            name = "Terrain Connector"
        };
        gameObject.transform.position = worldPosition;
        gameObject.transform.Rotate(worldRotation, Space.Self);
        gameObject.transform.parent = parentTransform;
        gameObject.isStatic = true;

        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshCollider = gameObject.AddComponent<MeshCollider>();
        MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = new Material(Shader.Find("Diffuse"));
        SetLayer(8);
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
                    Gizmos.color = new Color(noiseValue, noiseValue, noiseValue);
                    Gizmos.DrawSphere(worldPosition + new Vector3(x, y, z), 0.1f);
                }
            }
        }
    }
    
    public void SetLayer(int layer) {
        gameObject.layer = layer;
    }
    
}
