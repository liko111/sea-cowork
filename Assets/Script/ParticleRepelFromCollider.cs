using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ParticleRepelFromCollider : MonoBehaviour
{
    [Header("Repulsor")]
    public Collider repulsor;

    [Header("Push")]
    [Tooltip("Acceleration applied to particles (units/sec^2).")]
    public float pushAcceleration = 20f;

    [Tooltip("If > 0, push strength fades out to 0 at this distance from the collider surface.")]
    public float falloffDistance = 0.25f;

    [Tooltip("Clamp particle speed after push (0 = no clamp).")]
    public float maxSpeed = 0f;

    [Tooltip("Use Enter for one-shot impulse, Inside for continuous pushing.")]
    public ParticleSystemTriggerEventType triggerEvent = ParticleSystemTriggerEventType.Enter;

    ParticleSystem ps;
    ParticleSystem.MainModule main;
    readonly List<ParticleSystem.Particle> particles = new List<ParticleSystem.Particle>();

    void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        main = ps.main;
    }

    void OnParticleTrigger()
    {
        if (repulsor == null) return;

        int count = ps.GetTriggerParticles(triggerEvent, particles);
        if (count <= 0) return;

        bool simWorld = main.simulationSpace == ParticleSystemSimulationSpace.World;
        Vector3 repulsorCenter = repulsor.bounds.center;

        float dt = Time.deltaTime;

        for (int i = 0; i < count; i++)
        {
            var p = particles[i];

            Vector3 pWorldPos = simWorld ? p.position : ps.transform.TransformPoint(p.position);
            Vector3 closest = repulsor.ClosestPoint(pWorldPos);

            Vector3 dir = pWorldPos - closest;
            float dist = dir.magnitude;

            if (dist < 1e-5f)
            {
                dir = pWorldPos - repulsorCenter;
                dist = dir.magnitude;
            }

            dir = (dist > 1e-5f) ? (dir / dist) : Random.onUnitSphere;

            float accel = pushAcceleration;

            if (falloffDistance > 0f)
            {
                float t = Mathf.Clamp01(1f - dist / falloffDistance);
                accel *= t;
            }

            Vector3 vWorld = simWorld ? p.velocity : ps.transform.TransformDirection(p.velocity);
            vWorld += dir * (accel * dt);

            if (maxSpeed > 0f)
            {
                float s = vWorld.magnitude;
                if (s > maxSpeed) vWorld = vWorld / s * maxSpeed;
            }

            p.velocity = simWorld ? vWorld : ps.transform.InverseTransformDirection(vWorld);
            particles[i] = p;
        }

        ps.SetTriggerParticles(triggerEvent, particles);
    }
}