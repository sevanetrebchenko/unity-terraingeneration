using UnityEngine;

public static class Noise {
    // Returns a grid of values between 0 and 1
    public static float[] GenerateNoiseMap(int chunkSize, int mapSeed, float noiseScale, int numNoiseOctaves, float persistence, float lacunarity, Vector3 manualOffset) {
        float[] noiseMap = new float[chunkSize * chunkSize * chunkSize];

        System.Random seededGenerator = new System.Random(mapSeed);
        Vector3[] octaveOffsets = new Vector3[numNoiseOctaves];
        for (int i = 0; i < numNoiseOctaves; ++i) {
            float offsetX = seededGenerator.Next(-100000, 100000) + manualOffset.x;
            float offsetY = seededGenerator.Next(-100000, 100000) + manualOffset.y;
            float offsetZ = seededGenerator.Next(-100000, 100000) + manualOffset.z;

            octaveOffsets[i] = new Vector3(offsetX, offsetY, offsetZ);
        }

        if (noiseScale <= 0) {
            noiseScale = 0.0001f;
        }

        // Generate perlin noise values in the map.
        for (int ny = 0; ny < chunkSize; ++ny) {
            for (int nx = 0; nx < chunkSize; ++nx) {
                for (int nz = 0; nz < chunkSize; ++nz)
                {
                    int x = nx;
                    int y = ny * chunkSize * chunkSize;
                    int z = nz * chunkSize;
                    
                    float amplitude = 1.0f;
                    float frequency = 1.0f;
                    float noiseHeight = 0.0f;

                    for (int i = 0; i < numNoiseOctaves; ++i) {
                        float sampleX = (float)(x + octaveOffsets[i].x) / noiseScale * frequency;
                        float sampleY = (float)(y + octaveOffsets[i].y) / noiseScale * frequency;
                        float sampleZ = (float)(z + octaveOffsets[i].z) / noiseScale * frequency;

                        // Perlin noise gets the same value each time if the arguments passed are integer values.
                        float perlinValue = PerlinNoise.Noise(sampleX, sampleY, sampleZ) + 0.5f;
                        noiseHeight += perlinValue * amplitude;

                        amplitude *= persistence; // Persistence should be between 0 and 1 - amplitude decreases with each octave.
                        frequency *= lacunarity;  // Lacunarity should be greater than 1 - frequency increases with each octave.
                    }

                    noiseMap[x + y + z] = noiseHeight;
                }
            }
        }

        return noiseMap;
    }
}
