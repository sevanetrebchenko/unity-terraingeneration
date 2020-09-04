using System.Collections.Generic;
using UnityEngine;

public class TerrainChunk {
    public TerrainGenerator terrainGenerator;

    // Chunk mesh.
    private GameObject gameObject;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;

    private MeshData meshData;
    private HashSet<Vector3Int> remarchCubePositionList;

    private Vector3 chunkPosition;
    private Bounds chunkBounds;

    private int chunkSize;

    private float[,,] heightMap;
    private bool meshDataReceived = false;

    // Constructor takes in a position in the number of chunks away from (0, 0, 0), which gets scaled to world position.
    public TerrainChunk(TerrainGenerator terrainGenerator, Vector3 normalizedChunkPosition, int chunkSize, Transform parentTransform) {
        this.terrainGenerator = terrainGenerator;
        chunkPosition = normalizedChunkPosition * chunkSize;
        this.chunkSize = chunkSize;
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
        remarchCubePositionList = new HashSet<Vector3Int>();
        terrainGenerator.RequestMesh(OnTerrainMeshReceived, chunkPosition, true);
    }

    public void UpdateTerrainChunk(Vector3 viewerPosition, float maxViewDistance) {
        // Calculate the closest point on the chunk bounding cube to the position of the viewer.
        float distanceToNearestPoint = Mathf.Sqrt(chunkBounds.SqrDistance(viewerPosition));

        // The chunk is visible if the closest point on this chunk is within the maximum viewer view distance.
        bool chunkIsVisible = distanceToNearestPoint <= maxViewDistance;

        SetVisible(chunkIsVisible);
    }

    public void DebugDraw(float surfaceLevel) {
        if (meshDataReceived) {
            for (int z = 0; z < chunkSize; ++z) {
                for (int y = 0; y < chunkSize; ++y) {
                    for (int x = 0; x < chunkSize; ++x) {

                        if (heightMap[x, y, z] >= surfaceLevel) {
                            Gizmos.color = Color.white;
                        }
                        else {
                            Gizmos.color = Color.black;
                        }

                        Gizmos.DrawSphere(chunkPosition + new Vector3Int(x, y, z), 0.2f);

                    }
                }
            }
        }
    }

    public void SetLayer(int layer) {
        gameObject.layer = layer;
    }

    public void InputTriggered(Vector3Int hitCubePosition, bool place, int miningRadius) {

        int numCubesPerSide = chunkSize - 1;

        for (int z = 0; z < numCubesPerSide; ++z) {
            for (int y = 0; y < numCubesPerSide; ++y) {
                for (int x = 0; x < numCubesPerSide; ++x) {
                    Vector3Int currentCubePosition = new Vector3Int(x, y, z);
                    float distance = Vector3.Distance(hitCubePosition, currentCubePosition);

                    // Found a position that is affected by this mining radius.
                    if (distance <= miningRadius) {
                        // Affect terrain value based on parameters.
                        float terrainValueModification = Mathf.Lerp(0.08f, 0.0001f, distance / (float)miningRadius);
                        ref float terrainValue = ref heightMap[currentCubePosition.x, currentCubePosition.y, currentCubePosition.z];

                        if (place) {
                            terrainValue -= terrainValueModification;

                            if (terrainValue < 0) {
                                terrainValue = 0;
                            }
                        }
                        else {
                            terrainValue += terrainValueModification;

                            if (terrainValue > 1) {
                                terrainValue = 1;
                            }
                        }

                        // Add all adjacent blocks (removing duplicates).
                        for (int zAdjacent = -1; zAdjacent < 2; ++zAdjacent) {
                            for (int yAdjacent = -1; yAdjacent < 2; ++yAdjacent) {
                                for (int xAdjacent = -1; xAdjacent < 2; ++xAdjacent) {
                                    Vector3Int adjacentCube = currentCubePosition + new Vector3Int(xAdjacent, yAdjacent, zAdjacent);

                                    if (adjacentCube.x < numCubesPerSide && adjacentCube.x >= 0 && adjacentCube.y < numCubesPerSide && adjacentCube.y >= 0 && adjacentCube.z < numCubesPerSide && adjacentCube.z >= 0) {
                                        remarchCubePositionList.Add(adjacentCube);
                                    }
                                }
                            }
                        }

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
            lock(meshData) {
                Mesh mesh = meshData.BuildMesh();
                meshFilter.mesh = mesh;
                meshCollider.sharedMesh = mesh;
            }
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


