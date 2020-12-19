using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Unity.Mathematics;

[Serializable]
public class TerrainGenerator : MonoBehaviour
{
    // PUBLIC
    public Transform viewer;
    public GameObject chunkPrefab;
    public AnimationCurve animationCurve;

    public int numChunksX;
    public int numChunksZ;
    private int numChunksY;
    public DensityValues densityValues;

    // PRIVATE
    private int numNodesPerAxis;
    private int numCubes;
    
    private int totalNumChunks;
    private int totalChunkSize;
    private int totalStackSize;
    private SampledAnimationCurve sampledAnimationCurve;

    private Dictionary<Vector3Int, TerrainChunk> terrainChunks;
    private Dictionary<Vector3, TerrainChunkConnector> terrainChunkConnectors;
    private Dictionary<Vector3Int, NativeArray<float>> terrainStackHeightPlanes; 
    private Dictionary<Vector3Int, NativeArray<float>> terrainStackHeightMaps;

    private void Start()
    {
        numNodesPerAxis = 5;
        numCubes = numNodesPerAxis - 1;
        numChunksY = 3;
        
        totalNumChunks = numChunksX * numChunksY * numChunksZ;
        totalChunkSize = numNodesPerAxis * numNodesPerAxis * numNodesPerAxis;
        totalStackSize = totalChunkSize * numChunksY;
        
        terrainChunks = new Dictionary<Vector3Int, TerrainChunk>();
        terrainChunkConnectors = new Dictionary<Vector3, TerrainChunkConnector>();
        terrainStackHeightPlanes = new Dictionary<Vector3Int, NativeArray<float>>();
        terrainStackHeightMaps = new Dictionary<Vector3Int, NativeArray<float>>();
        sampledAnimationCurve = new SampledAnimationCurve(animationCurve, 10000);
        
        NativeList<JobHandle> jobHandles = new NativeList<JobHandle>(totalNumChunks, Allocator.TempJob);
        
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Generate all chunks.
        for (int z = -numChunksZ / 2; z < numChunksZ / 2; ++z)
        {
            for (int x = -numChunksX / 2; x < numChunksX / 2; ++x)
            {
                Vector3Int chunkPosition = new Vector3Int(x, 0, z);
                jobHandles.Add(Initialize3DChunkHeightMap(chunkPosition, -1.0f)); // Stacks start at all air
            }
        }
        
        JobHandle.CompleteAll(jobHandles);
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        
        
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Generate terrain chunk height maps.
        for (int z = -numChunksZ / 2; z < numChunksZ / 2; ++z)
        {
            for (int x = -numChunksX / 2; x < numChunksX / 2; ++x)
            {
                Vector3Int chunkPosition = new Vector3Int(x, 0, z);
                jobHandles.Add(Generate2DChunkHeightMap(chunkPosition));
            }
        }
        
        JobHandle.CompleteAll(jobHandles);
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Initialize 3D chunk height values.
        for (int z = -numChunksZ / 2; z < numChunksZ / 2; ++z)
        {
            for (int x = -numChunksX / 2; x < numChunksX / 2; ++x)
            {
                Vector3Int chunkPosition = new Vector3Int(x, 0, z);
                jobHandles.Add(Generate3DChunkHeightMap(chunkPosition));
            }
        }
        
        JobHandle.CompleteAll(jobHandles);
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Generate terrain chunks from initialized height maps.
        foreach (KeyValuePair<Vector3Int, NativeArray<float>> terrainStackData in terrainStackHeightMaps)
        {
            Vector3Int stackPosition = terrainStackData.Key;
            NativeArray<float> terrainStack = terrainStackData.Value;

            int yIndex = 0;
            for (int chunkY = -numChunksY / 2; chunkY < numChunksY / 2; ++chunkY)
            {
                Vector3Int chunkPosition = new Vector3Int(stackPosition.x, stackPosition.y + chunkY, stackPosition.z);
                
                // Copy over data into new chunk height map.
                NativeArray<float> chunkHeightMap = new NativeArray<float>(totalChunkSize, Allocator.Persistent);
                for (int x = 0; x < numNodesPerAxis; ++x)
                {
                    for (int z = 0; z < numNodesPerAxis; ++z)
                    {
                        for (int y = 0; y < numNodesPerAxis; ++y)
                        {
                            int localIndex = x + z * numNodesPerAxis + y * numNodesPerAxis * numNodesPerAxis;
                            chunkHeightMap[localIndex] = terrainStack[localIndex + yIndex * totalChunkSize];
                        }
                    }
                }

                ++yIndex;
                
                TerrainChunk chunk = new TerrainChunk(numNodesPerAxis, numCubes, chunkPosition, chunkHeightMap, transform);
                terrainChunks.Add(chunkPosition, chunk);
                jobHandles.Add(GenerateChunkMesh(chunk));
            }
        }
        
        JobHandle.CompleteAll(jobHandles);
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Generate terrain between chunks.
        for (int z = -numChunksZ / 2; z < numChunksZ / 2; ++z)
        {
            for (int x = -numChunksX / 2; x < numChunksX / 2; ++x)
            {
                for (int y = -numChunksY / 2; y < numChunksY / 2; ++y)
                {
                    Vector3Int chunkPosition = new Vector3Int(x, y, z);
        
                    if (terrainChunks.ContainsKey(chunkPosition))
                    {
                        // Generate connectors for chunk sides.
                        Vector3 connectorPositionLR = new Vector3(x - 0.5f, y, z);
                        Vector3 connectorPositionTB = new Vector3(x, y + 0.5f, z);
                        Vector3 connectorPositionFB = new Vector3(x, y, z + 0.5f);
                        
                        // LEFT/RIGHT
                        if (!terrainChunkConnectors.ContainsKey(connectorPositionLR))
                        {
                            Vector3Int otherChunk = new Vector3Int(chunkPosition.x - 1, chunkPosition.y, chunkPosition.z);
                            if (terrainChunks.ContainsKey(chunkPosition) && terrainChunks.ContainsKey(otherChunk))
                            {
                                // Left wall of this chunk, right wall of right chunk
                                TerrainChunk first = terrainChunks[chunkPosition];
                                TerrainChunk second = terrainChunks[otherChunk];
                        
                                // Left wall
                                NativeArray<float> firstHeightMap = new NativeArray<float>(numNodesPerAxis * numNodesPerAxis, Allocator.Persistent);
                                int index = 0;
                                for (int i = 0; i < numNodesPerAxis; ++i) // z
                                {
                                    for (int j = 0; j < numNodesPerAxis; ++j) // y
                                    {
                                        int chunkIndex = 0 + numNodesPerAxis * i + j * numNodesPerAxis * numNodesPerAxis;
                                        firstHeightMap[index++] = first.heightMap[chunkIndex];
                                    }
                                }
                                    
                                // Right wall.
                                NativeArray<float> secondHeightMap = new NativeArray<float>(numNodesPerAxis * numNodesPerAxis, Allocator.Persistent);
                                index = 0;
                                for (int i = 0; i < numNodesPerAxis; ++i) // z
                                {
                                    for (int j = 0; j < numNodesPerAxis; ++j) // y
                                    {
                                        int chunkIndex = numNodesPerAxis - 1 + numNodesPerAxis * i + j * numNodesPerAxis * numNodesPerAxis;
                                        secondHeightMap[index++] = second.heightMap[chunkIndex];
                                    }
                                }
                        
                                
                                TerrainChunkConnector terrainChunkConnector = new TerrainChunkConnector(numNodesPerAxis, new Vector3(x, y, z) * numNodesPerAxis - new Vector3(1.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 0.0f), firstHeightMap, secondHeightMap, transform);
                                terrainChunkConnectors.Add(connectorPositionLR, terrainChunkConnector);
                                jobHandles.Add(terrainChunkConnector.GenerateConnectorMesh());
                            }
                        }
                        
                        // FRONT/BACK
                        if (!terrainChunkConnectors.ContainsKey(connectorPositionFB))
                        {
                            Vector3Int otherChunk = new Vector3Int(chunkPosition.x, chunkPosition.y, chunkPosition.z + 1);
                            if (terrainChunks.ContainsKey(chunkPosition) && terrainChunks.ContainsKey(otherChunk))
                            {
                                // Back wall of this chunk, front wall of other chunk.
                                TerrainChunk first = terrainChunks[chunkPosition];
                                TerrainChunk second = terrainChunks[otherChunk];
                        
                                // Back wall of chunk (relative to global x,y,z orientation, z going away from the camera).
                                NativeArray<float> firstHeightMap = new NativeArray<float>(numNodesPerAxis * numNodesPerAxis, Allocator.Persistent);
                                int index = 0;
                                for (int i = 0; i < numNodesPerAxis; ++i) // x
                                {
                                    for (int j = 0; j < numNodesPerAxis; ++j) // y
                                    {
                                        int chunkIndex = i + numNodesPerAxis * (numNodesPerAxis - 1) + j * numNodesPerAxis * numNodesPerAxis;
                                        firstHeightMap[index++] = first.heightMap[chunkIndex];
                                    }
                                }
                                    
                                // Front wall of chunk (relative to global x,y,z orientation, z going away from the camera).
                                NativeArray<float> secondHeightMap = new NativeArray<float>(numNodesPerAxis * numNodesPerAxis, Allocator.Persistent);
                                index = 0;
                                for (int i = 0; i < numNodesPerAxis; ++i) // x
                                {
                                    for (int j = 0; j < numNodesPerAxis; ++j) // y
                                    {
                                        int chunkIndex = i + j * numNodesPerAxis * numNodesPerAxis;
                                        secondHeightMap[index++] = second.heightMap[chunkIndex];
                                    }
                                }
                                
                                TerrainChunkConnector terrainChunkConnector = new TerrainChunkConnector(numNodesPerAxis, new Vector3(x, y, z) * numNodesPerAxis + new Vector3(0.0f, 0.0f, numNodesPerAxis), new Vector3(0.0f, 90.0f, 0.0f), firstHeightMap, secondHeightMap, transform);
                                terrainChunkConnectors.Add(connectorPositionFB, terrainChunkConnector);
                                jobHandles.Add(terrainChunkConnector.GenerateConnectorMesh());
                            }
                        }
                        
                        // TOP/BOTTOM
                        if (!terrainChunkConnectors.ContainsKey(connectorPositionTB))
                        {
                            Vector3Int otherChunk = new Vector3Int(chunkPosition.x, chunkPosition.y + 1, chunkPosition.z);
                            if (terrainChunks.ContainsKey(chunkPosition) && terrainChunks.ContainsKey(otherChunk))
                            {
                                // Top wall of this chunk, bottom wall of other chunk.
                                TerrainChunk first = terrainChunks[chunkPosition];
                                TerrainChunk second = terrainChunks[otherChunk];
                            
                                // Top wall of chunk (relative to global x,y,z orientation, z going away from the camera).
                                NativeArray<float> firstHeightMap = new NativeArray<float>(numNodesPerAxis * numNodesPerAxis, Allocator.Persistent);
                                int index = 0;
                                for (int i = 0; i < numNodesPerAxis; ++i) // x
                                {
                                    for (int j = 0; j < numNodesPerAxis; ++j) // z
                                    {
                                        int chunkIndex = j + numNodesPerAxis * i + (numNodesPerAxis - 1) * numNodesPerAxis * numNodesPerAxis;
                                        firstHeightMap[index++] = first.heightMap[chunkIndex];
                                    }
                                }
                                    
                                // Bottom wall of chunk (relative to global x,y,z orientation, z going away from the camera).
                                NativeArray<float> secondHeightMap = new NativeArray<float>(numNodesPerAxis * numNodesPerAxis, Allocator.Persistent);
                                index = 0;
                                for (int i = 0; i < numNodesPerAxis; ++i) // x
                                {
                                    for (int j = 0; j < numNodesPerAxis; ++j) // z
                                    {
                                        int chunkIndex = j + numNodesPerAxis * i;
                                        secondHeightMap[index++] = second.heightMap[chunkIndex];
                                    }
                                }
                                
                                TerrainChunkConnector terrainChunkConnector = new TerrainChunkConnector(numNodesPerAxis, new Vector3(x, y + 1, z) * numNodesPerAxis, new Vector3(0.0f, 0.0f, -90.0f), firstHeightMap, secondHeightMap, transform);
                                terrainChunkConnectors.Add(connectorPositionTB, terrainChunkConnector);
                                jobHandles.Add(terrainChunkConnector.GenerateConnectorMesh());
                            }
                        }
                        
                        // Generate connectors for chunk corners.
                        Vector3 connectorPositionCornersAxisY = new Vector3(x - 0.5f, y, z + 0.5f);
                        Vector3 connectorPositionCornersAxisX = new Vector3(x, y + 0.5f, z + 0.5f);
                        Vector3 connectorPositionCornersAxisZ = new Vector3(x - 0.5f, y + 0.5f, z);
                        
                        // 4 CORNERS WITH Y AXIS GOING UP.
                        if (!terrainChunkConnectors.ContainsKey(connectorPositionCornersAxisY))
                        {
                            Vector3Int topLeft = new Vector3Int(chunkPosition.x - 1, chunkPosition.y, chunkPosition.z + 1);
                            Vector3Int topRight = new Vector3Int(chunkPosition.x, chunkPosition.y, chunkPosition.z + 1);
                            Vector3Int bottomLeft = new Vector3Int(chunkPosition.x - 1, chunkPosition.y, chunkPosition.z);
                            Vector3Int bottomRight = new Vector3Int(chunkPosition.x, chunkPosition.y, chunkPosition.z);
                            
                            if (terrainChunks.ContainsKey(topLeft) && terrainChunks.ContainsKey(topRight) && terrainChunks.ContainsKey(bottomLeft) && terrainChunks.ContainsKey(bottomRight))
                            {
                                // Top wall of this chunk, bottom wall of other chunk.
                                TerrainChunk topLeftChunk = terrainChunks[topLeft];
                                TerrainChunk topRightChunk = terrainChunks[topRight];
                                TerrainChunk bottomLeftChunk = terrainChunks[bottomLeft];
                                TerrainChunk bottomRightChunk = terrainChunks[bottomRight];
                            
                                // Bottom right corner of top left chunk
                                NativeArray<float> firstHeightMap = new NativeArray<float>(numNodesPerAxis, Allocator.Persistent);
                                int index = 0;
                                for (int i = 0; i < numNodesPerAxis; ++i) // y
                                {
                                    int chunkIndex = (numNodesPerAxis - 1) + i * numNodesPerAxis * numNodesPerAxis;
                                    firstHeightMap[index++] = topLeftChunk.heightMap[chunkIndex];
                                }
                                    
                                // Bottom left corner of top right chunk
                                NativeArray<float> secondHeightMap = new NativeArray<float>(numNodesPerAxis, Allocator.Persistent);
                                index = 0;
                                for (int i = 0; i < numNodesPerAxis; ++i) // y
                                {
                                    int chunkIndex = i * numNodesPerAxis * numNodesPerAxis;
                                    secondHeightMap[index++] = topRightChunk.heightMap[chunkIndex];
                                }
                                
                                // Top right corner of bottom left chunk
                                NativeArray<float> thirdHeightMap = new NativeArray<float>(numNodesPerAxis, Allocator.Persistent);
                                index = 0;
                                for (int i = 0; i < numNodesPerAxis; ++i) // y
                                {
                                    int chunkIndex = (numNodesPerAxis - 1) + (numNodesPerAxis - 1) * numNodesPerAxis + i * numNodesPerAxis * numNodesPerAxis;
                                    thirdHeightMap[index++] = bottomLeftChunk.heightMap[chunkIndex];
                                }
                                
                                // Top left corner of bottom right chunk
                                NativeArray<float> fourthHeightMap = new NativeArray<float>(numNodesPerAxis, Allocator.Persistent);
                                index = 0;
                                for (int i = 0; i < numNodesPerAxis; ++i) // y
                                {
                                    int chunkIndex = (numNodesPerAxis - 1) * numNodesPerAxis + i * numNodesPerAxis * numNodesPerAxis;
                                    fourthHeightMap[index++] = bottomRightChunk.heightMap[chunkIndex];
                                }
                                
                                TerrainChunkConnector terrainChunkConnector = new TerrainChunkConnector(numNodesPerAxis, new Vector3(x, y, z) * numNodesPerAxis + new Vector3(-1.0f, 0.0f, numNodesPerAxis - 1), new Vector3(0.0f, 0.0f, 0.0f), firstHeightMap, secondHeightMap, thirdHeightMap, fourthHeightMap, transform);
                                terrainChunkConnectors.Add(connectorPositionCornersAxisY, terrainChunkConnector);
                                jobHandles.Add(terrainChunkConnector.GenerateConnectorMesh());
                            }
                        }
                        
                        // 4 CORNERS WITH X AXIS GOING UP.
                        if (!terrainChunkConnectors.ContainsKey(connectorPositionCornersAxisX))
                        {
                            Vector3Int topLeft = new Vector3Int(chunkPosition.x, chunkPosition.y + 1, chunkPosition.z);
                            Vector3Int topRight = new Vector3Int(chunkPosition.x, chunkPosition.y + 1, chunkPosition.z + 1);
                            Vector3Int bottomLeft = new Vector3Int(chunkPosition.x, chunkPosition.y, chunkPosition.z);
                            Vector3Int bottomRight = new Vector3Int(chunkPosition.x, chunkPosition.y, chunkPosition.z + 1);
                            
                            if (terrainChunks.ContainsKey(topLeft) && terrainChunks.ContainsKey(topRight) && terrainChunks.ContainsKey(bottomLeft) && terrainChunks.ContainsKey(bottomRight))
                            {
                                // Top wall of this chunk, bottom wall of other chunk.
                                TerrainChunk topLeftChunk = terrainChunks[topLeft];
                                TerrainChunk topRightChunk = terrainChunks[topRight];
                                TerrainChunk bottomLeftChunk = terrainChunks[bottomLeft];
                                TerrainChunk bottomRightChunk = terrainChunks[bottomRight];
                            
                                // Bottom right corner of top left chunk
                                NativeArray<float> firstHeightMap = new NativeArray<float>(numNodesPerAxis, Allocator.Persistent);
                                int index = 0;
                                for (int i = 0; i < numNodesPerAxis; ++i) // x
                                {
                                    int chunkIndex = i + (numNodesPerAxis - 1) * numNodesPerAxis;
                                    firstHeightMap[index++] = topLeftChunk.heightMap[chunkIndex];
                                }
                                    
                                // Bottom left corner of top right chunk
                                NativeArray<float> secondHeightMap = new NativeArray<float>(numNodesPerAxis, Allocator.Persistent);
                                index = 0;
                                for (int i = 0; i < numNodesPerAxis; ++i) // x
                                {
                                    int chunkIndex = i;
                                    secondHeightMap[index++] = topRightChunk.heightMap[chunkIndex];
                                }
                                
                                // Top right corner of bottom left chunk
                                NativeArray<float> thirdHeightMap = new NativeArray<float>(numNodesPerAxis, Allocator.Persistent);
                                index = 0;
                                for (int i = 0; i < numNodesPerAxis; ++i) // x
                                {
                                    int chunkIndex = i + (numNodesPerAxis - 1) * numNodesPerAxis + (numNodesPerAxis - 1) * numNodesPerAxis * numNodesPerAxis;
                                    thirdHeightMap[index++] = bottomLeftChunk.heightMap[chunkIndex];
                                }
                                
                                // Top left corner of bottom right chunk
                                NativeArray<float> fourthHeightMap = new NativeArray<float>(numNodesPerAxis, Allocator.Persistent);
                                index = 0;
                                for (int i = 0; i < numNodesPerAxis; ++i) // x
                                {
                                    int chunkIndex = i + (numNodesPerAxis - 1) * numNodesPerAxis * numNodesPerAxis;
                                    fourthHeightMap[index++] = bottomRightChunk.heightMap[chunkIndex];
                                }
                                
                                TerrainChunkConnector terrainChunkConnector = new TerrainChunkConnector(numNodesPerAxis, new Vector3(x, y, z) * numNodesPerAxis + new Vector3(-1.0f, numNodesPerAxis, numNodesPerAxis - 1.0f), new Vector3(0.0f, 90.0f,  270.0f), firstHeightMap, secondHeightMap, thirdHeightMap, fourthHeightMap, transform);
                                terrainChunkConnectors.Add(connectorPositionCornersAxisX, terrainChunkConnector);
                                jobHandles.Add(terrainChunkConnector.GenerateConnectorMesh());
                            }
                        }
                        
                        // // 4 CORNERS WITH Z AXIS GOING UP.
                        // if (!terrainChunkConnectors.ContainsKey(connectorPositionCornersAxisZ))
                        // {
                        //     Vector3Int topLeft = new Vector3Int(chunkPosition.x, chunkPosition.y + 1, chunkPosition.z);
                        //     Vector3Int topRight = new Vector3Int(chunkPosition.x - 1, chunkPosition.y + 1, chunkPosition.z);
                        //     Vector3Int bottomLeft = new Vector3Int(chunkPosition.x, chunkPosition.y, chunkPosition.z);
                        //     Vector3Int bottomRight = new Vector3Int(chunkPosition.x - 1, chunkPosition.y, chunkPosition.z);
                        //     
                        //     if (terrainChunks.ContainsKey(topLeft) && terrainChunks.ContainsKey(topRight) && terrainChunks.ContainsKey(bottomLeft) && terrainChunks.ContainsKey(bottomRight))
                        //     {
                        //         // Top wall of this chunk, bottom wall of other chunk.
                        //         TerrainChunk topLeftChunk = terrainChunks[topLeft];
                        //         TerrainChunk topRightChunk = terrainChunks[topRight];
                        //         TerrainChunk bottomLeftChunk = terrainChunks[bottomLeft];
                        //         TerrainChunk bottomRightChunk = terrainChunks[bottomRight];
                        //     
                        //         // Bottom right corner of top left chunk
                        //         NativeArray<float> firstHeightMap = new NativeArray<float>(numNodesPerAxis, Allocator.Persistent);
                        //         int index = 0;
                        //         for (int i = 0; i < numNodesPerAxis; ++i) // z
                        //         {
                        //             int chunkIndex = (numNodesPerAxis - 1) + i * numNodesPerAxis;
                        //             firstHeightMap[index++] = topLeftChunk.heightMap[chunkIndex];
                        //         }
                        //             
                        //         // Bottom left corner of top right chunk
                        //         NativeArray<float> secondHeightMap = new NativeArray<float>(numNodesPerAxis, Allocator.Persistent);
                        //         index = 0;
                        //         for (int i = 0; i < numNodesPerAxis; ++i) // z
                        //         {
                        //             int chunkIndex = i * numNodesPerAxis;
                        //             secondHeightMap[index++] = topRightChunk.heightMap[chunkIndex];
                        //         }
                        //         
                        //         // Top right corner of bottom left chunk
                        //         NativeArray<float> thirdHeightMap = new NativeArray<float>(numNodesPerAxis, Allocator.Persistent);
                        //         index = 0;
                        //         for (int i = 0; i < numNodesPerAxis; ++i) // z
                        //         {
                        //             int chunkIndex = (numNodesPerAxis - 1) + i * numNodesPerAxis + (numNodesPerAxis - 1) * numNodesPerAxis * numNodesPerAxis;
                        //             thirdHeightMap[index++] = bottomLeftChunk.heightMap[chunkIndex];
                        //         }
                        //         
                        //         // Top left corner of bottom right chunk
                        //         NativeArray<float> fourthHeightMap = new NativeArray<float>(numNodesPerAxis, Allocator.Persistent);
                        //         index = 0;
                        //         for (int i = 0; i < numNodesPerAxis; ++i) // z
                        //         {
                        //             int chunkIndex = i * numNodesPerAxis + (numNodesPerAxis - 1) * numNodesPerAxis * numNodesPerAxis;
                        //             fourthHeightMap[index++] = bottomRightChunk.heightMap[chunkIndex];
                        //         }
                        //         
                        //         TerrainChunkConnector terrainChunkConnector = new TerrainChunkConnector(numNodesPerAxis, new Vector3(x, y, z) * numNodesPerAxis + new Vector3(-1.0f,  numNodesPerAxis - 1.0f, 0.0f), new Vector3(0.0f, 90.0f, 90.0f), firstHeightMap, secondHeightMap, thirdHeightMap, fourthHeightMap, transform);
                        //         terrainChunkConnectors.Add(connectorPositionCornersAxisZ, terrainChunkConnector);
                        //         jobHandles.Add(terrainChunkConnector.GenerateConnectorMesh());
                        //     }
                        // }
                    }
                }
            }
        }
        
        JobHandle.CompleteAll(jobHandles);
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Build meshes.
        foreach (KeyValuePair<Vector3Int, TerrainChunk> terrainChunkData in terrainChunks)
        {
            TerrainChunk terrainChunk = terrainChunkData.Value;
            terrainChunk.ConstructMesh();
        }
        
        foreach (KeyValuePair<Vector3, TerrainChunkConnector> terrainConnectorData in terrainChunkConnectors)
        {
            TerrainChunkConnector terrainChunkConnector = terrainConnectorData.Value;
            terrainChunkConnector.ConstructMesh();
        }
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        
        // Cleanup
        for (int z = -numChunksZ / 2; z < numChunksZ / 2; ++z)
        {
            for (int x = -numChunksX / 2; x < numChunksX / 2; ++x)
            {
                for (int y = -numChunksY / 2; y < numChunksY / 2; ++y)
                {
                    Vector3Int chunkPosition = new Vector3Int(x, y, z);
                    if (terrainStackHeightPlanes.ContainsKey(chunkPosition))
                    {
                        terrainStackHeightPlanes[chunkPosition].Dispose();
                        terrainStackHeightPlanes.Remove(chunkPosition);
                    }

                    if (terrainStackHeightMaps.ContainsKey(chunkPosition))
                    {
                        terrainStackHeightMaps[chunkPosition].Dispose();
                        terrainStackHeightMaps.Remove(chunkPosition);
                    }
                }
            }
        }

        jobHandles.Dispose();
    }


    private void OnDrawGizmos()
    {
        foreach (KeyValuePair<Vector3, TerrainChunkConnector> chunkData in terrainChunkConnectors)
        {
            chunkData.Value.OnGizmos();
        }
        
        foreach (KeyValuePair<Vector3Int, TerrainChunk> chunkData in terrainChunks)
        {
            chunkData.Value.OnGizmos();
        }
    }

    private JobHandle Generate2DChunkHeightMap(Vector3Int chunkPosition)
    {
        // Establish the map to write to.
        terrainStackHeightPlanes.Add(chunkPosition, new NativeArray<float>(numNodesPerAxis * numNodesPerAxis, Allocator.TempJob));
        
        PlaneHeightGenerationJob heightGenerationJob = new PlaneHeightGenerationJob
        {
            numNodesPerAxis = numNodesPerAxis,
            numNoiseOctaves = densityValues.numNoiseOctaves,
            noiseScale = densityValues.noiseScale,
            persistence = densityValues.persistence,
            lacunarity = densityValues.lacunarity,
            offset = new float3(chunkPosition.x, chunkPosition.y, chunkPosition.z) * numNodesPerAxis,
            terrainHeightMap = terrainStackHeightPlanes[chunkPosition],
            seededGenerator = new Unity.Mathematics.Random(densityValues.terrainSeed)
        };

        return heightGenerationJob.Schedule();
    }

    private JobHandle Initialize3DChunkHeightMap(Vector3Int chunkPosition, float heightValue)
    {
        NativeArray<float> chunkHeightMap = new NativeArray<float>(totalStackSize, Allocator.TempJob);
        terrainStackHeightMaps.Add(chunkPosition, chunkHeightMap);
        
        ChunkStackInitializationJob heightInitializationJob = new ChunkStackInitializationJob
        {
            numNodesPerAxis = numNodesPerAxis,
            chunkStackDimensions = new int3(numNodesPerAxis, numNodesPerAxis * numChunksY, numNodesPerAxis),
            heightValue = heightValue,
            heightMap = chunkHeightMap
        };

        return heightInitializationJob.Schedule();
    }

    private JobHandle Generate3DChunkHeightMap(Vector3Int chunkPosition)
    {
        // Establish the map to write to.
        ChunkHeightGenerationJob heightGenerationJob = new ChunkHeightGenerationJob
        {
            startingHeight = (numNodesPerAxis * numChunksY) / 5 * 3, // 4 chunks of air, 6 chunks of caves
            stackHeight = numNodesPerAxis * numChunksY,
            heightMultiplier = densityValues.heightMultiplier,
            numNodesPerAxis = numNodesPerAxis,
            terrainStackHeightMap = terrainStackHeightMaps[chunkPosition],
            terrainHeightMapPlane = terrainStackHeightPlanes[chunkPosition],
            sampledAnimationCurve = sampledAnimationCurve
        };

        return heightGenerationJob.Schedule();
    }
    
    private JobHandle GenerateChunkMesh(TerrainChunk terrainChunk)
    {
        // Establish the map to write to.
        MeshGenerationJob meshGenerationJob = new MeshGenerationJob
        {
            cornerTable = MarchingCubes.cornerTable,
            edgeTable = MarchingCubes.edgeTable,
            triangleTable = MarchingCubes.triangleTable,
            vertices = terrainChunk.vertices,
            numElements = terrainChunk.numElements,
            terrainHeightMap = terrainChunk.heightMap,
            terrainSurfaceLevel = densityValues.surfaceLevel,
            terrainSmoothing = densityValues.terrainSmoothing,
            axisDimensionsInCubes = new int3(numCubes, numCubes, numCubes),
            numNodesPerAxis = new int3(numNodesPerAxis, numNodesPerAxis, numNodesPerAxis)
        };

        return meshGenerationJob.Schedule();
    }

    private void OnDestroy()
    {
        foreach (KeyValuePair<Vector3Int, TerrainChunk> terrainChunk in terrainChunks)
        {
            terrainChunk.Value.OnDestroy();
        }
        
        foreach (KeyValuePair<Vector3Int, NativeArray<float>> heightMap in terrainStackHeightPlanes)
        {
            heightMap.Value.Dispose();
        }
        
        sampledAnimationCurve.Dispose();
    }
    
}
