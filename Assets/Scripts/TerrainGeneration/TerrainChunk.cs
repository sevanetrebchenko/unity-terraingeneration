using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

public class TerrainChunk {
    public TerrainGenerator terrainGenerator;

    // Chunk mesh.
    private GameObject gameObject;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;

    private MeshData meshData;
    private List<Vector3Int> remarchCubePositionList;

    private Vector3 chunkPosition;
    private Bounds chunkBounds;

    private float[,,] heightMap;
    private bool meshDataReceived = false;

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
        remarchCubePositionList = new List<Vector3Int>();
        terrainGenerator.RequestMesh(OnTerrainMeshReceived, chunkPosition, true);
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

    public void InputTriggered(Vector3Int cubePosition, bool place) {

        // Register input.
        if (place) {
            heightMap[cubePosition.x, cubePosition.y, cubePosition.z] -= 0.05f;

            if (heightMap[cubePosition.x, cubePosition.y, cubePosition.z] < 0) {
                heightMap[cubePosition.x, cubePosition.y, cubePosition.z] = 0;
            }
        }
        else {
            heightMap[cubePosition.x, cubePosition.y, cubePosition.z] += 0.05f;

            if (heightMap[cubePosition.x, cubePosition.y, cubePosition.z] > 1) {
                heightMap[cubePosition.x, cubePosition.y, cubePosition.z] = 1;
            }
        }

        int dimension = heightMap.GetLength(0) - 1;

        // Remarch all cubes within 1 block that are affected by the change.
        for (int z = -1; z < 2; ++z) {
            for (int y = -1; y < 2; ++y) {
                for (int x = -1; x < 2; ++x) {
                    Vector3Int position = cubePosition + new Vector3Int(x, y, z);

                    if (position.x < dimension && position.x >= 0 && position.y < dimension && position.y >= 0 && position.z < dimension && position.z >= 0) {
                        remarchCubePositionList.Add(position);
                    }
                }
            }
        }
    }

    // Regenerate the mesh if the terrain generator or chunk size was changed.
    public void Regenerate() {
        meshDataReceived = false;
        terrainGenerator.RequestMesh(OnTerrainMeshReceived, meshData, remarchCubePositionList, heightMap);
    }

    public void BuildMesh() {
        if (meshDataReceived) {
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


