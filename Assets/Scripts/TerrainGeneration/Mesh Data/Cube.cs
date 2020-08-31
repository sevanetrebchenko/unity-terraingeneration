using UnityEngine;

public class Cube {
    public static int maxNumberVertices = 15; // A cube can have maximum 15 vertices
    public Vector3[] vertices;
    public int[] triangleIndices;
    public Color[] colors;
    public Vector3Int normalizedCubePosition; // Position relative to the parent transform.

    public int numElements;

    // Marching cubes data.
    private float[] cubeCornerNoiseValues;
    private int configurationIndex;
    private Color terrainColor;

    public Cube(float[,,] noiseMap, Vector3Int normalizedCubePosition, Color terrainColor) {
        // Any marching cubes configuration has maximum 5 triangles (15 vertices).
        vertices = new Vector3[maxNumberVertices];
        triangleIndices = new int[maxNumberVertices];
        colors = new Color[maxNumberVertices];
        this.terrainColor = terrainColor;

        this.normalizedCubePosition = normalizedCubePosition;
        numElements = 0;

        ConstructCubeCorners(noiseMap);
    }

    public void GenerateCubeMeshData(bool terrainSmoothing, float surfaceLevel) {
        GetCubeConfiguration(surfaceLevel);

        int edgeIndex = 0;
		// A configuration has maximum 5 triangles in it.
		for (int i = 0; i < 5; ++i) {
			// A configuration element (triangle) consists of 3 points.
			for (int j = 0; j < 3; ++j) {
				int index = MarchingCubesConfiguration.triangleTable[configurationIndex, edgeIndex];

				// Reached the end of this configuration.
				if (index == -1) {
					return;
				}

				Vector3 edgeVertex1 = normalizedCubePosition + MarchingCubesConfiguration.cornerTable[MarchingCubesConfiguration.edgeTable[index, 0]];
				Vector3 edgeVertex2 = normalizedCubePosition + MarchingCubesConfiguration.cornerTable[MarchingCubesConfiguration.edgeTable[index, 1]];

                Vector3 vertexPosition;

                if (terrainSmoothing) {
                    float edgeVertex1Noise = cubeCornerNoiseValues[MarchingCubesConfiguration.edgeTable[index, 0]];
                    float edgeVertex2Noise = cubeCornerNoiseValues[MarchingCubesConfiguration.edgeTable[index, 1]];

                    float diff = edgeVertex2Noise - edgeVertex1Noise;

                    if (diff == 0) {
                        diff = surfaceLevel;
                    }
                    else {
                        diff = (surfaceLevel - edgeVertex1Noise) / diff;
                    }

                    vertexPosition = edgeVertex1 + ((edgeVertex2 - edgeVertex1) * diff);
                }
                else {
                    vertexPosition = (edgeVertex1 + edgeVertex2) / 2.0f;
                }

                vertices[numElements++] = vertexPosition;
                ++edgeIndex;
			}
		}

        // Fill in mesh colors.
        for (int i = 0; i < numElements; ++i) {
            colors[i] = terrainColor;
        }
	}
    
    public void SetTriangleIndex(int arrayIndex, int triangleIndex) {
        triangleIndices[arrayIndex] = triangleIndex;
    }

    private void GetCubeConfiguration(float surfaceLevel) {
        configurationIndex = 0;
        for (int i = 0; i < 8; ++i) {
            if (cubeCornerNoiseValues[i] > surfaceLevel) {
                configurationIndex |= 1 << i;
            }
        }
    }

    private void ConstructCubeCorners(float[,,] noiseMap) {
        cubeCornerNoiseValues = new float[MarchingCubesConfiguration.cornerTable.Length];

        // Get 8 cube corners.
        for (int i = 0; i < 8; ++i) {
            Vector3Int cubeCorner = normalizedCubePosition + MarchingCubesConfiguration.cornerTable[i];
            cubeCornerNoiseValues[i] = noiseMap[cubeCorner.x, cubeCorner.y, cubeCorner.z];
        }
    }

}
