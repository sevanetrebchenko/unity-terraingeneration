using Unity.Collections;
using UnityEngine;

// Convert AnimationCurve into something usable inside of Unity's Job system.
public struct SampledAnimationCurve : System.IDisposable {
    public NativeArray<float> curveSamples;

    public SampledAnimationCurve(AnimationCurve curve, int samples)  {
        curveSamples = new NativeArray<float>(samples, Allocator.Persistent);
        float timeFrom = curve.keys[0].time;
        float timeTo = curve.keys[curve.keys.Length - 1].time;
        float timeStep = (timeTo - timeFrom) / (float) (samples - 1); // Step between each consecutive samples.

        for (int i = 0; i < samples; i++) {
            curveSamples[i] = curve.Evaluate(timeFrom + (i * timeStep));
        }
    }

    public void Dispose() {
        if (curveSamples.IsCreated)
            curveSamples.Dispose();
    }

    // Takes value between 0 and 1.
    public float Evaluate(float sampleTime) {
        int len = curveSamples.Length - 1;

        // Clamp sample time [0.0, 1.0] inclusive.
        if (sampleTime < 0.0f) {
            sampleTime = 0.0f;
        }
        else if (sampleTime > 1.0f) {
            sampleTime = 1.0f;
        }

        float floatIndex = (sampleTime * len);
        int floorIndex = Mathf.FloorToInt(floatIndex);
        if (floorIndex == len) {
            return curveSamples[len];
        }

        // Two samples to lerp between.
        float lowerValue = curveSamples[floorIndex];
        float higherValue = curveSamples[floorIndex + 1];

        // Get the decimal part of the number.
        float floored = Mathf.Floor(floatIndex);
        float fractionalPart = floatIndex - floored;

        return Mathf.Lerp(lowerValue, higherValue, fractionalPart);
    }
}