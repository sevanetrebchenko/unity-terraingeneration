using UnityEngine;

public class MeshData {
	// PUBLIC
	public float[,,] heightMap;

	// PRIVATE
	private Cube[,,] cubes;
	private Color terrainColor;
	private float surfaceLevel;
	private bool terrainSmoothing;

	private int widthInCubes;
	private int heightInCubes;
	private int depthInCubes;

	public MeshData(int widthInCubes, int heightInCubes, int depthInCubes, float[,,] heightMap, Color terrainColor, float surfaceLevel, bool terrainSmoothing) {
		this.widthInCubes = widthInCubes;
		this.heightInCubes = heightInCubes;
		this.depthInCubes = depthInCubes;
		this.heightMap = heightMap;
		this.terrainColor = terrainColor;
		this.surfaceLevel = surfaceLevel;
		this.terrainSmoothing = terrainSmoothing;

		cubes = new Cube[widthInCubes, heightInCubes, depthInCubes];
		ConstructCubes();
	}

	// Build the mesh up until the provided number of cubes. If the number of cubes is -1, build the entire mesh.
	public Mesh BuildMesh() {
		Mesh mesh = new Mesh();
		int meshSize = CalculateMeshTriangleSize();

		// Allocate triangle, index, and color buffers for mesh.
		Vector3[] meshVertices = new Vector3[meshSize];
		int[] meshTriangleIndices = new int[meshSize];
		Color[] meshColors = new Color[meshSize];
		TransferCubeData(meshVertices, meshTriangleIndices, meshColors);

		// Construct mesh from elements.
		mesh.vertices = meshVertices;
		mesh.triangles = meshTriangleIndices;
		mesh.colors = meshColors;
		mesh.RecalculateNormals();
		return mesh;
	}

	private void ConstructCubes() {
		// March cubes through the entire volume.
		for (int y = 0; y < heightInCubes; ++y) {
			for (int x = 0; x < widthInCubes; ++x) {
				for (int z = 0; z < depthInCubes; ++z) {
					// Generate a cube at the given (x, y, z) position.
					Vector3Int cubePosition = new Vector3Int(x, y, z);
					Cube cube = new Cube(heightMap, cubePosition, terrainColor);
					cube.GenerateCubeMeshData(terrainSmoothing, surfaceLevel);

					cubes[x, y, z] = cube;
				}
			}
		}
	}

	private int CalculateMeshTriangleSize() {
		int meshSize = 0;

		foreach(Cube cube in cubes) {
			meshSize += cube.numElements;
        }

		return meshSize;
	}

	private void TransferCubeData(Vector3[] meshVertices, int[] meshTriangles, Color[] meshColors) {
		// Transfer mesh data.
		int currentMeshIndex = 0;
		foreach(Cube cube in cubes) {
			// Copy all vertices and set triangle indices.
			for (int i = 0; i < cube.numElements; ++i) {
				// Set vertex and triangle index in mesh array.
				meshVertices[currentMeshIndex] = cube.vertices[i];
				meshTriangles[currentMeshIndex] = currentMeshIndex;
				meshColors[currentMeshIndex] = cube.colors[i];

				// Update triangle index of cube relative to all other cubes.
				cube.SetTriangleIndex(i, currentMeshIndex);
				++currentMeshIndex;
			}
		}
	}
}