using UnityEngine;

namespace DistantLands
{
    [System.Serializable]
    public class WaypointBehaviorSettings
    {
        [Tooltip("Noise amplitude in local route space: X = side, Y = up, Z = forward.")]
        public Vector3 noiseAmplitude = new Vector3(1.2f, 2.0f, 0.8f);

        [Tooltip("Noise speed in local route space: X = side, Y = up, Z = forward.")]
        public Vector3 noiseSpeed = new Vector3(0.25f, 0.35f, 0.20f);

        [Tooltip("Extra side-to-side wave amplitude.")]
        public float sideWaveAmplitude = 0.6f;
        public float sideWaveSpeed = 0.8f;

        [Tooltip("Extra up/down wave amplitude.")]
        public float verticalWaveAmplitude = 1.25f;
        public float verticalWaveSpeed = 1.0f;

        [Tooltip("Extra forward/back wave amplitude along the route direction.")]
        public float forwardWaveAmplitude = 0.25f;
        public float forwardWaveSpeed = 0.6f;

        [Tooltip("How quickly the route offset smooths toward the new motion.")]
        public float offsetSmoothSpeed = 2.5f;

        [Tooltip("Push the shared target a bit ahead along the route direction.")]
        public float forwardLookDistance = 0.5f;

        public WaypointBehaviorSettings() { }

        public WaypointBehaviorSettings(WaypointBehaviorSettings other)
        {
            noiseAmplitude = other.noiseAmplitude;
            noiseSpeed = other.noiseSpeed;
            sideWaveAmplitude = other.sideWaveAmplitude;
            sideWaveSpeed = other.sideWaveSpeed;
            verticalWaveAmplitude = other.verticalWaveAmplitude;
            verticalWaveSpeed = other.verticalWaveSpeed;
            forwardWaveAmplitude = other.forwardWaveAmplitude;
            forwardWaveSpeed = other.forwardWaveSpeed;
            offsetSmoothSpeed = other.offsetSmoothSpeed;
            forwardLookDistance = other.forwardLookDistance;
        }
    }

    public class FishRoutePointBehavior : MonoBehaviour
    {
        [Header("Enable / Blend")]
        public bool enableBehavior = true;

        [Range(0f, 1f)]
        [Tooltip("0 = use only the flock's default motion. 1 = fully use this point's custom motion.")]
        public float behaviorStrength = 1f;

        [Min(0f)]
        [Tooltip("How far before this point the special behavior starts blending in.")]
        public float behaviorStartDistance = 8f;

        [Min(0.01f)]
        [Tooltip("Higher values make the effect wait longer and ramp harder near the point.")]
        public float behaviorBlendPower = 1f;

        [Header("Behavior At This Point")]
        public WaypointBehaviorSettings behavior = new WaypointBehaviorSettings();

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, behaviorStartDistance);
        }
    }
}
