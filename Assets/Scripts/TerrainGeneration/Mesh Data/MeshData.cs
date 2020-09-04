using System;
using System.Collections.Generic;
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

	private Mesh mesh;
	private int meshSize;

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

	public Mesh BuildMesh() {
		mesh = new Mesh();

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

	public void RegenerateCubes(HashSet<Vector3Int> cubePositionsToRemarch) {

		foreach (Vector3Int cubePosition in cubePositionsToRemarch) {

			Cube cube = new Cube(cubePosition, terrainColor);
			ConstructCubeCorners(cube);
			cube.GenerateCubeMeshData(terrainSmoothing, surfaceLevel);

			lock (cubes) {
				int previousNumElements = cubes[cubePosition.x, cubePosition.y, cubePosition.z].numElements;
				cubes[cubePosition.x, cubePosition.y, cubePosition.z] = cube;
				// Update the count of mesh elements to reflect new terrain configuration.
				// Could have less or more vertices.
				meshSize += cube.numElements - previousNumElements;
			}
		}

		cubePositionsToRemarch.Clear();
    }

	private void ConstructCubes() {
		meshSize = 0;

		// March cubes through the entire volume.
		for (int y = 0; y < heightInCubes; ++y) {
			for (int x = 0; x < widthInCubes; ++x) {
				for (int z = 0; z < depthInCubes; ++z) {
					// Generate a cube at the given (x, y, z) position.
					Vector3Int cubePosition = new Vector3Int(x, y, z);

					// Construct cube
					Cube cube = new Cube(cubePosition, terrainColor);
					ConstructCubeCorners(cube);
					cube.GenerateCubeMeshData(terrainSmoothing, surfaceLevel);
					meshSize += cube.numElements;

					cubes[x, y, z] = cube;
				}
			}
		}
	}

	private void ConstructCubeCorners(Cube cube) {
		// Get 8 cube corners.
		for (int i = 0; i < 8; ++i) {
			Vector3Int cubeCorner = cube.normalizedCubePosition + MarchingCubesConfiguration.cornerTable[i];
			cube.cubeCornerNoiseValues[i] = heightMap[cubeCorner.x, cubeCorner.y, cubeCorner.z];
		}
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
				cube.triangleIndices[i] = currentMeshIndex;
				++currentMeshIndex;
			}
		}
	}
}