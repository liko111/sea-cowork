using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
[RequireComponent(typeof(ParticleSystemRenderer))]
public class ParticleFishGlowPulse : MonoBehaviour
{
    private enum GlowState
    {
        Idle = 0,
        Rising = 1,
        Holding = 2,
        Falling = 3,
        Cooldown = 4
    }

    [Header("Targets")]
    [SerializeField] private List<Collider> triggerColliders = new List<Collider>();
    [SerializeField] private float detectionPadding = 0.02f;

    [Header("Intensity")]
    [SerializeField] private float minIntensity = 1f;
    [SerializeField] private float maxIntensity = 4f;

    [Header("Timing")]
    [SerializeField] private float riseTime = 0.5f;
    [SerializeField] private float emissionTime = 0.35f;
    [SerializeField] private float fallTime = 0.5f;
    [SerializeField] private float cooldownTime = 0.25f;

    [Header("Curves")]
    [SerializeField] private AnimationCurve riseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve fallCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Renderer Streams")]
    [SerializeField] private bool autoEnableCustom1VertexStream = true;

    [Header("Debug")]
    [SerializeField] private bool drawDebugLines = false;
    [SerializeField] private Color debugInsideColor = Color.green;
    [SerializeField] private Color debugOutsideColor = Color.red;

    private ParticleSystem ps;
    private ParticleSystemRenderer psRenderer;
    private ParticleSystem.MainModule mainModule;

    private ParticleSystem.Particle[] particles = Array.Empty<ParticleSystem.Particle>();
    private readonly List<Vector4> custom1 = new List<Vector4>();
    private readonly List<ParticleSystemVertexStream> activeStreams = new List<ParticleSystemVertexStream>();

    private Transform customSimulationSpace;

    private void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        psRenderer = GetComponent<ParticleSystemRenderer>();
        mainModule = ps.main;
        customSimulationSpace = mainModule.customSimulationSpace;

        AllocateParticleBuffer();

        if (autoEnableCustom1VertexStream)
        {
            EnsureCustom1VertexStream();
        }
    }

    private void OnEnable()
    {
        AllocateParticleBuffer();
    }

    private void OnValidate()
    {
        minIntensity = Mathf.Max(0f, minIntensity);
        maxIntensity = Mathf.Max(minIntensity, maxIntensity);

        riseTime = Mathf.Max(0f, riseTime);
        emissionTime = Mathf.Max(0f, emissionTime);
        fallTime = Mathf.Max(0f, fallTime);
        cooldownTime = Mathf.Max(0f, cooldownTime);

        detectionPadding = Mathf.Max(0f, detectionPadding);
    }

    private void LateUpdate()
    {
        if (ps == null)
        {
            return;
        }

        int liveCount = ps.particleCount;
        if (liveCount <= 0)
        {
            return;
        }

        if (particles.Length < liveCount)
        {
            AllocateParticleBuffer();
        }

        int count = ps.GetParticles(particles);
        if (count <= 0)
        {
            return;
        }

        SyncCustomDataCount(count);
        ps.GetCustomParticleData(custom1, ParticleSystemCustomData.Custom1);
        SyncCustomDataCount(count);

        float dt = Time.deltaTime;

        for (int i = 0; i < count; i++)
        {
            Vector4 data = custom1[i];

            float currentIntensity = data.x;
            GlowState state = (GlowState)Mathf.RoundToInt(data.y);
            float stateTimer = data.z;

            Vector3 worldPos = GetParticleWorldPosition(particles[i].position);
            bool touched = IsInsideAnyTriggerCollider(worldPos);

            switch (state)
            {
                case GlowState.Idle:
                    {
                        currentIntensity = minIntensity;
                        stateTimer = 0f;

                        if (touched)
                        {
                            state = GlowState.Rising;
                            stateTimer = 0f;
                        }

                        break;
                    }

                case GlowState.Rising:
                    {
                        stateTimer += dt;

                        float t = riseTime <= 0f ? 1f : Mathf.Clamp01(stateTimer / riseTime);
                        float curved = riseCurve != null ? riseCurve.Evaluate(t) : t;
                        currentIntensity = Mathf.LerpUnclamped(minIntensity, maxIntensity, curved);

                        if (t >= 1f)
                        {
                            state = GlowState.Holding;
                            stateTimer = 0f;
                            currentIntensity = maxIntensity;
                        }

                        break;
                    }

                case GlowState.Holding:
                    {
                        stateTimer += dt;
                        currentIntensity = maxIntensity;

                        if (stateTimer >= emissionTime)
                        {
                            state = GlowState.Falling;
                            stateTimer = 0f;
                        }

                        break;
                    }

                case GlowState.Falling:
                    {
                        stateTimer += dt;

                        float t = fallTime <= 0f ? 1f : Mathf.Clamp01(stateTimer / fallTime);
                        float curved = fallCurve != null ? fallCurve.Evaluate(t) : t;
                        currentIntensity = Mathf.LerpUnclamped(maxIntensity, minIntensity, curved);

                        if (t >= 1f)
                        {
                            state = GlowState.Cooldown;
                            stateTimer = 0f;
                            currentIntensity = minIntensity;
                        }

                        break;
                    }

                case GlowState.Cooldown:
                    {
                        stateTimer += dt;
                        currentIntensity = minIntensity;

                        if (stateTimer >= cooldownTime)
                        {
                            state = GlowState.Idle;
                            stateTimer = 0f;
                        }

                        break;
                    }

                default:
                    {
                        state = GlowState.Idle;
                        stateTimer = 0f;
                        currentIntensity = minIntensity;
                        break;
                    }
            }

            data.x = currentIntensity;
            data.y = (float)state;
            data.z = stateTimer;
            data.w = touched ? 1f : 0f;

            custom1[i] = data;

            if (drawDebugLines)
            {
                Debug.DrawLine(
                    worldPos,
                    worldPos + Vector3.up * 0.05f,
                    touched ? debugInsideColor : debugOutsideColor,
                    0f,
                    false
                );
            }
        }

        ps.SetCustomParticleData(custom1, ParticleSystemCustomData.Custom1);
    }

    private void AllocateParticleBuffer()
    {
        int capacity = Mathf.Max(4, ps != null ? ps.main.maxParticles : 4);

        if (particles.Length != capacity)
        {
            particles = new ParticleSystem.Particle[capacity];
        }
    }

    private void SyncCustomDataCount(int count)
    {
        if (custom1.Count < count)
        {
            int toAdd = count - custom1.Count;
            for (int i = 0; i < toAdd; i++)
            {
                custom1.Add(new Vector4(minIntensity, (float)GlowState.Idle, 0f, 0f));
            }
        }
        else if (custom1.Count > count)
        {
            custom1.RemoveRange(count, custom1.Count - count);
        }
    }

    private Vector3 GetParticleWorldPosition(Vector3 particlePosition)
    {
        switch (mainModule.simulationSpace)
        {
            case ParticleSystemSimulationSpace.World:
                return particlePosition;

            case ParticleSystemSimulationSpace.Local:
                return transform.TransformPoint(particlePosition);

            case ParticleSystemSimulationSpace.Custom:
                if (customSimulationSpace != null)
                {
                    return customSimulationSpace.TransformPoint(particlePosition);
                }
                return transform.TransformPoint(particlePosition);

            default:
                return transform.TransformPoint(particlePosition);
        }
    }

    private bool IsInsideAnyTriggerCollider(Vector3 worldPosition)
    {
        if (triggerColliders == null || triggerColliders.Count == 0)
        {
            return false;
        }

        float paddingSqr = detectionPadding * detectionPadding;

        for (int i = 0; i < triggerColliders.Count; i++)
        {
            Collider col = triggerColliders[i];
            if (col == null || !col.enabled || !col.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 closest = col.ClosestPoint(worldPosition);
            float sqrDistance = (closest - worldPosition).sqrMagnitude;

            if (sqrDistance <= paddingSqr)
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureCustom1VertexStream()
    {
        if (psRenderer == null)
        {
            return;
        }

        activeStreams.Clear();
        psRenderer.GetActiveVertexStreams(activeStreams);

        bool hasCustom1 = activeStreams.Contains(ParticleSystemVertexStream.Custom1XYZW);
        if (!hasCustom1)
        {
            activeStreams.Add(ParticleSystemVertexStream.Custom1XYZW);
            psRenderer.SetActiveVertexStreams(activeStreams);
        }
    }

    public void SetTriggerColliders(List<Collider> colliders)
    {
        triggerColliders = colliders;
    }

    public void AddTriggerCollider(Collider col)
    {
        if (col == null)
        {
            return;
        }

        if (triggerColliders == null)
        {
            triggerColliders = new List<Collider>();
        }

        if (!triggerColliders.Contains(col))
        {
            triggerColliders.Add(col);
        }
    }

    public void RemoveTriggerCollider(Collider col)
    {
        if (triggerColliders == null || col == null)
        {
            return;
        }

        triggerColliders.Remove(col);
    }

    [ContextMenu("Reset All Live Fish To Min Intensity")]
    public void ResetAllLiveFishToMinIntensity()
    {
        if (ps == null)
        {
            ps = GetComponent<ParticleSystem>();
        }

        int count = ps.GetParticles(particles);
        if (count <= 0)
        {
            return;
        }

        SyncCustomDataCount(count);
        ps.GetCustomParticleData(custom1, ParticleSystemCustomData.Custom1);
        SyncCustomDataCount(count);

        for (int i = 0; i < count; i++)
        {
            custom1[i] = new Vector4(minIntensity, (float)GlowState.Idle, 0f, 0f);
        }

        ps.SetCustomParticleData(custom1, ParticleSystemCustomData.Custom1);
    }
}