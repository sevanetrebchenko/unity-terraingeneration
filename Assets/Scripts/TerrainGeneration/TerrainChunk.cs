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

    public void InputTriggered(Vector3Int cubePosition, bool place, float miningRadius) {
        int width = heightMap.GetLength(0) - 1;
        int height = heightMap.GetLength(1) - 1;
        int depth = heightMap.GetLength(2) - 1;

        for (int z = 0; z < depth; ++z) {
            for (int y = 0; y < height; ++y) {
                for (int x = 0; x < width; ++x) {
                    Vector3Int position = new Vector3Int(x, y, z);
                    float distance = Vector3.Distance(cubePosition, position);

                    if (distance <= miningRadius) {
                        float terrainValue = Mathf.Lerp(0.05f, 0.001f, distance / miningRadius);

                        if (place) {
                            heightMap[position.x, position.y, position.z] -= terrainValue;

                            if (heightMap[position.x, position.y, position.z] < 0) {
                                heightMap[position.x, position.y, position.z] = 0;
                            }
                        }
                        else {
                            heightMap[position.x, position.y, position.z] += terrainValue;

                            if (heightMap[position.x, position.y, position.z] > 1) {
                                heightMap[position.x, position.y, position.z] = 1;
                            }
                        }

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


