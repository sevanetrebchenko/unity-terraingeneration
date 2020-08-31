using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

public class TerrainChunk {
    public TerrainGenerator terrainGenerator;
    public float[,,] heightMap;

    // Chunk mesh.
    GameObject gameObject;
    MeshFilter meshFilter;
    MeshData meshData;
    MeshRenderer meshRenderer;
    MeshCollider meshCollider;

    Vector3 chunkPosition;
    Bounds chunkBounds;

    bool meshDataReceived = false;

    // Constructor takes in a position in the number of chunks away from (0, 0, 0), which gets scaled to world position.
    public TerrainChunk(TerrainGenerator terrainGenerator, Vector3 normalizedChunkPosition, int chunkSize, Transform parentTransform) {
        this.terrainGenerator = terrainGenerator;
        chunkPosition = normalizedChunkPosition * chunkSize;
        chunkBounds = new Bounds(chunkPosition, Vector3.one * chunkSize);

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

        // Set properties.
        meshRenderer.sharedMaterial = new Material(Shader.Find("Diffuse")) {
            color = terrainGenerator.terrainColor
        };
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // Turn shadows off
        meshRenderer.receiveShadows = false;

        // Generate and build terrain mesh.
        terrainGenerator.RequestMesh(OnTerrainMeshReceived, chunkPosition, false);
    }

    public void UpdateTerrainChunk(Vector3 viewerPosition, float maxViewDistance) {
        // Calculate the closest point on the chunk bounding cube to the position of the viewer.
        float distanceToNearestPoint = Mathf.Sqrt(chunkBounds.SqrDistance(viewerPosition));

        // The chunk is visible if the closest point on this chunk is within the maximum viewer view distance.
        bool chunkIsVisible = distanceToNearestPoint <= maxViewDistance;

        SetVisible(chunkIsVisible);
    }

    public void SetLayer(int layer) {
        gameObject.layer = layer;
    }

    // Regenerate the mesh if the terrain generator or chunk size was changed.
    public void Regenerate() {
        meshDataReceived = false;
        Debug.Log("requesting mesh");
        terrainGenerator.RequestMesh(OnTerrainMeshReceived, chunkPosition, heightMap, false);
    }

    public void BuildMesh() {
        if (meshDataReceived) {
            Debug.Log("building mesh");
            Mesh mesh = meshData.BuildMesh();
            meshFilter.mesh = mesh;
            meshCollider.sharedMesh = mesh;
        }
    }
    public void SetVisible(bool chunkIsVisible) {
        gameObject.SetActive(chunkIsVisible);
    }

    public bool IsVisible() {
        return gameObject.activeSelf;
    }

    private void OnTerrainMeshReceived(MeshData meshData) {
        this.meshData = meshData;
        heightMap = meshData.heightMap;
        meshDataReceived = true;
        BuildMesh();
    }
}


