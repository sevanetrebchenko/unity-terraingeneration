using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

[System.Serializable]
public class TerrainGenerator : MonoBehaviour {
    // PUBLIC
    public Transform viewer;

    public int chunkSize;
    public int numVisibleChunks = 1;
    public float surfaceLevel = 0.5f;
    public Color terrainColor = Color.white;
    public float noiseScale = 40.0f;
    [Range(1, 10)]
    public int numNoiseOctaves = 6;
    [Range(0, 1)]
    public float persistence = 0.15f;
    [Range(1, 20)]
    public float lacunarity = 4.0f;
    public int terrainSeed = 0;
    public Vector3 terrainOffset = Vector3.zero;

    // PRIVATE
    private Vector3 oldViewerPosition;
    private const float moveThresholdForChunkUpdate = 50.0f;
    private const float moveThresholdForChunkUpdateSquared = moveThresholdForChunkUpdate * moveThresholdForChunkUpdate;
    private Queue<TerrainThreadData<MeshData>> meshDataThreadInfoQueue;

    private Dictionary<Vector3Int, TerrainChunk> terrainChunks = new Dictionary<Vector3Int, TerrainChunk>();
    private List<TerrainChunk> previousFrameTerrainChunks = new List<TerrainChunk>();

    private void Start() {
        meshDataThreadInfoQueue = new Queue<TerrainThreadData<MeshData>>();
        chunkSize = 15;
        UpdateVisibleChunks();
    }

    private void Update() {
        // Dispense completed generated meshes.
        if (meshDataThreadInfoQueue.Count > 0) {
            for (int i = 0; i < meshDataThreadInfoQueue.Count; i++) {
                TerrainThreadData<MeshData> threadInfo = meshDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }

        // Update viewer position and regenerate chunks if necessary.
        Vector3 viewerPosition = new Vector3(viewer.position.x, viewer.position.y, viewer.position.z);

        // Only update if the viewer has moved past a certain location (prevents updates every frame).
        if ((oldViewerPosition - viewerPosition).sqrMagnitude > moveThresholdForChunkUpdateSquared) {
            oldViewerPosition = viewerPosition;
            UpdateVisibleChunks();
        }
    }

    public void ReceiveClick(Transform objectTransform, Vector3 hitPoint, bool place) {
        Vector3 relativeObjectPosition = objectTransform.position - transform.position;
        Vector3Int normalizedObjectPosition = new Vector3Int(Mathf.RoundToInt(relativeObjectPosition.x), Mathf.RoundToInt(relativeObjectPosition.y), Mathf.RoundToInt(relativeObjectPosition.z)) / (chunkSize - 1);

        Vector3 relativeHitLocation = hitPoint - transform.position - objectTransform.position;
        Vector3Int normalizedHitPosition = new Vector3Int(Mathf.RoundToInt(relativeHitLocation.x), Mathf.RoundToInt(relativeHitLocation.y), Mathf.RoundToInt(relativeHitLocation.z));

        if (terrainChunks.ContainsKey(normalizedObjectPosition)) {
            TerrainChunk hitChunk = terrainChunks[normalizedObjectPosition];
            hitChunk.InputTriggered(normalizedHitPosition, place);
            hitChunk.Regenerate();
        }
    }

    // Generate a default chunk using chunk position.
    public void RequestMesh(System.Action<MeshData> callback, Vector3 chunkCenter, bool terrainSmoothing) {
        ThreadStart threadStart = delegate {
            GenerateMesh(callback, chunkCenter, terrainSmoothing);
        };

        new Thread(threadStart).Start();
    }

    // Generate a default chunk using a pre-made height map.
    public void RequestMesh(System.Action<MeshData> callback, Vector3 chunkCenter, float[,,] heightMap, bool terrainSmoothing) {
        ThreadStart threadStart = delegate {
            GenerateMesh(callback, chunkCenter, heightMap, terrainSmoothing);
        };

        new Thread(threadStart).Start();
    }

    // Generate a modified chunk (terrain edits).
    public void RequestMesh(System.Action<MeshData> callback, MeshData meshData, List<Vector3Int> positionsToRemarch, float[,,] heightMap) {
        ThreadStart threadStart = delegate {
            GenerateMesh(callback, meshData, positionsToRemarch, heightMap);
        };

        new Thread(threadStart).Start();
    }


    private void GenerateMesh(System.Action<MeshData> callback, Vector3 chunkCenter, bool terrainSmoothing) {
        // Generate components.
        float[,,] noiseMap = Noise.GenerateNoiseMap(chunkSize, chunkSize, chunkSize, terrainSeed, noiseScale, numNoiseOctaves, persistence, lacunarity, chunkCenter + terrainOffset);
        MeshData generatedMesh = MarchingCubesMeshGenerator.GenerateTerrainMesh(noiseMap, surfaceLevel, terrainSmoothing, terrainColor);

        // Emplace in queue.
        lock (meshDataThreadInfoQueue) {
            meshDataThreadInfoQueue.Enqueue(new TerrainThreadData<MeshData>(callback, generatedMesh));
        }
    }

    private void GenerateMesh(System.Action<MeshData> callback, Vector3 chunkCenter, float[,,] heightMap, bool terrainSmoothing) {
        // Generate components.
        MeshData generatedMesh = MarchingCubesMeshGenerator.GenerateTerrainMesh(heightMap, surfaceLevel, terrainSmoothing, terrainColor);

        // Emplace in queue.
        lock (meshDataThreadInfoQueue) {
            meshDataThreadInfoQueue.Enqueue(new TerrainThreadData<MeshData>(callback, generatedMesh));
        }
    }

    private void GenerateMesh(System.Action<MeshData> callback, MeshData meshData, List<Vector3Int> cubePositionsToRemarch, float[,,] heightMap) {
        MarchingCubesMeshGenerator.RegenerateTerrainMesh(meshData, cubePositionsToRemarch, heightMap);

        // Emplace in queue.
        lock (meshDataThreadInfoQueue) {
            meshDataThreadInfoQueue.Enqueue(new TerrainThreadData<MeshData>(callback, meshData));
        }
    }

    private struct TerrainThreadData<T> {
        public readonly System.Action<T> callback;
        public readonly T parameter;

        public TerrainThreadData(System.Action<T> callback, T parameter) {
            this.callback = callback;
            this.parameter = parameter;
        }
    }

    private void UpdateVisibleChunks() {
        // Set previous chunks to be invisible.
        for (int i = 0; i < previousFrameTerrainChunks.Count; i++) {
            previousFrameTerrainChunks[i].SetVisible(false);
        }
        previousFrameTerrainChunks.Clear();

        // Get the current viewer position.
        int currentChunkCoordX = Mathf.RoundToInt(viewer.position.x / chunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewer.position.x / chunkSize);
        int currentChunkCoordZ = Mathf.RoundToInt(viewer.position.x / chunkSize);

        for (int yOffset = -numVisibleChunks; yOffset <= numVisibleChunks; yOffset++) {
            for (int xOffset = -numVisibleChunks; xOffset <= numVisibleChunks; xOffset++) {
                for (int zOffset = -numVisibleChunks; zOffset <= numVisibleChunks; zOffset++) {
                    Vector3Int viewedChunkCoord = new Vector3Int(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset, currentChunkCoordZ + zOffset);

                    // Terrain chunk exists already.
                    if (terrainChunks.ContainsKey(viewedChunkCoord)) {
                        // Update terrain chunk to recalculate visibility.
                        terrainChunks[viewedChunkCoord].UpdateTerrainChunk(viewer.position, 50);

                        // If the terrain chunk is visible, add it to the previousFrameTerrain chunks.
                        if (terrainChunks[viewedChunkCoord].IsVisible()) {
                            terrainChunks[viewedChunkCoord].Regenerate();
                            previousFrameTerrainChunks.Add(terrainChunks[viewedChunkCoord]);
                        }
                    }
                    // Terrain chunk does not exist, generate a new one.
                    else {
                        // Terrain chunk does not need to be updated as it is always going to be visible upon creation.
                        TerrainChunk chunk = new TerrainChunk(this, viewedChunkCoord, chunkSize - 1, transform);
                        chunk.SetLayer(gameObject.layer);
                        terrainChunks.Add(viewedChunkCoord, chunk);
                    }
                }
            }
        }
    }
}
