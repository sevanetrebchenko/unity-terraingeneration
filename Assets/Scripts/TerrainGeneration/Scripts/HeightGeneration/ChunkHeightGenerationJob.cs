using Unity.Collections;
using Unity.Jobs;

public struct ChunkHeightGenerationJob : IJob
{
    public int startingHeight;
    public int stackHeight;
    public float heightMultiplier;
    public int numNodesPerAxis;

    [WriteOnly] public NativeArray<float> terrainStackHeightMap;
    
    [ReadOnly] public SampledAnimationCurve sampledAnimationCurve;
    [ReadOnly] public NativeArray<float> terrainHeightMapPlane;

    public void Execute()
    {
        for (int x = 0; x < numNodesPerAxis; ++x)
        {
            for (int z = 0; z < numNodesPerAxis; ++z)
            {
                int index = x + z * numNodesPerAxis;
                float noiseValue = terrainHeightMapPlane[index];
                int noiseHeight = (int) (noiseValue * sampledAnimationCurve.Evaluate(noiseValue) * heightMultiplier);

                int start = stackHeight - 1;
                int end = startingHeight + noiseHeight;

                // Clamp so no negative values appear.
                if (end < 0)
                {
                    end = 0;
                }
                
                // Go from the very top of the chunk to the terrain height at that value.
                for (int y = start; y > end; --y)
                {
                    terrainStackHeightMap[x + y * numNodesPerAxis * numNodesPerAxis + z * numNodesPerAxis] = 1;
                }
            }
        }
    }
}
