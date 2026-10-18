using BlackoutProtocol.Core;
using UnityEngine;

namespace BlackoutProtocol.AI.Sentinel.Testing
{
    /// <summary>Minimal controllable player state used by the Sentinel sandbox scenario.</summary>
    [DisallowMultipleComponent]
    public sealed class SentinelTestPlayerState : MonoBehaviour, IPlayerState
    {
        public Transform Transform => transform;
        public Vector3 Position => transform.position;
        public Vector3 Velocity { get; private set; }
        public float HealthNormalized => 1f;
        public bool IsAlive => isActiveAndEnabled;
        public bool IsReloading => false;
        public bool IsCrouching => false;
        public bool IsRunning => false;
        public Transform AimTarget => null;

        private void OnEnable() => PlayerRegistry.Register(this);
        private void OnDisable() => PlayerRegistry.Unregister(this);

        public void SetPose(Vector3 position, Vector3 velocity)
        {
            transform.position = position;
            Velocity = velocity;
        }
    }
}
