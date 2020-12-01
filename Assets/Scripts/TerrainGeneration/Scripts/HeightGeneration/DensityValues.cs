using System;
using UnityEngine;

[Serializable]
public class DensityValues
{
    [Range(-1.0f, 1.0f)] 
    public float surfaceLevel = 0.0f;
    
    [Range(0.0f, 100.0f)]
    public float noiseScale = 1.0f;
    
    [Range(1, 10)]
    public int numNoiseOctaves = 6;
    
    [Range(0, 1)] 
    public float persistence = 0.15f;
    
    [Range(1, 20)] 
    public float lacunarity = 2.0f;
    
    public uint terrainSeed = 01972394817;
    public bool terrainSmoothing = false;

    [Range(0, 100)] public int heightMultiplier = 64;
}
