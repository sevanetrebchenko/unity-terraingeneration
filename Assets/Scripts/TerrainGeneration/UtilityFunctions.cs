using UnityEngine;

public class UtilityFunctions  {
    public static int GetIndexFromCoordinate(Vector3Int coordinate, int chunkSize)
    {
        return coordinate.z * chunkSize * chunkSize + coordinate.y * chunkSize + coordinate.x;
    }

    public static int GetIndex(int chunkSize, int numChunksPerSide, Vector3Int chunk, Vector3Int node)
    {
        int x = node.x + chunk.x * chunkSize;
        int y = node.y * chunkSize * chunkSize * numChunksPerSide * numChunksPerSide + chunk.y * chunkSize * chunkSize * chunkSize * numChunksPerSide * numChunksPerSide;
        int z = node.z * chunkSize * numChunksPerSide + chunk.z * chunkSize * chunkSize * numChunksPerSide;

        return x + y + z;
    }
    
    public static int GetIndex(int chunkSize, int numChunksPerSide, int cx, int cy, int cz, int nx, int ny, int nz)
    {
        int x = nx + cx * chunkSize;
        int y = ny * chunkSize * chunkSize * numChunksPerSide * numChunksPerSide + cy * chunkSize * chunkSize * chunkSize * numChunksPerSide * numChunksPerSide;
        int z = nz * chunkSize * numChunksPerSide + cz * chunkSize * chunkSize * numChunksPerSide;

        return x + y + z;
    }
}
