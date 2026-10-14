using BlackoutProtocol.Core;
using UnityEngine;

public class ShootingNoiseListener : MonoBehaviour
{
    private void OnEnable() => GameEvents.Noise += OnNoise;
    private void OnDisable() => GameEvents.Noise -= OnNoise;

    private void OnNoise(NoiseEvent noise)
    {
        if (noise.Type == NoiseType.Gunshot)
            Debug.Log($"Gunshot noise at {noise.Position}, loudness {noise.Loudness}");
    }
}