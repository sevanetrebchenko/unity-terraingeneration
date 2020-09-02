using System.Collections.Generic;
using UnityEngine;

public static class MarchingCubesMeshGenerator {

    public static MeshData GenerateTerrainMesh(float[,,] noiseMap, float surfaceLevel, bool terrainSmoothing, Color terrainColor) {
		// Get noise map dimensions.
		int width = noiseMap.GetLength(0);
		int height = noiseMap.GetLength(1);
		int depth = noiseMap.GetLength(2);

		return new MeshData(width - 1, height - 1, depth - 1, noiseMap, terrainColor, surfaceLevel, terrainSmoothing);
	}

	public static void RegenerateTerrainMesh(MeshData meshData, List<Vector3Int> cubePositionsToRemarch, float[,,] heightMap) {
		meshData.heightMap = heightMap;
		meshData.RegenerateCubes(cubePositionsToRemarch);
    }
}
