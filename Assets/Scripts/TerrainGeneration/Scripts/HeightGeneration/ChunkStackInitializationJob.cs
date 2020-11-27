using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct ChunkStackInitializationJob : IJob
{
    [ReadOnly] public int chunkSize;
    [ReadOnly] public int3 chunkStackDimensions;
    [ReadOnly] public float heightValue;
    [WriteOnly] public NativeArray<float> heightMap;
    
    public void Execute()
    {
        for (int x = 0; x < chunkStackDimensions.x; ++x)
        {
            for (int z = 0; z < chunkStackDimensions.z; ++z)
            {
                for (int y = 0; y < chunkStackDimensions.y; ++y)
                {
                    heightMap[x + z * chunkSize + y * chunkSize * chunkSize] = heightValue;
                }
            }
        }
    }
}
