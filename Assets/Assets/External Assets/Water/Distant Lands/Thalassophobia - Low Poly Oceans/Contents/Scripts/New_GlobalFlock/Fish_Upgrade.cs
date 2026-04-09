using UnityEngine;

namespace DistantLands
{
    public class Fish_Upgrade : MonoBehaviour
    {
        [Header("Flock Link")]
        [HideInInspector] public GlobalFlock flock;
        [Min(0)] public int performance = 2;

        [Header("Speed")]
        public float averageSpeed = 3f;
        public Vector2 speedRandomRange = new Vector2(0.85f, 1.15f);
        public float acceleration = 2.5f;
        public float turnSpeed = 4f;

        [Header("Size")]
        public Vector3 baseScale = Vector3.one;
        public float sizeMultiplier = 1f;
        public Vector2 randomSizeRange = new Vector2(0.9f, 1.1f);

        [Header("Facing")]
        [Tooltip("Use this if the fish model is facing the wrong axis in the prefab. Example: Y = 90 or 180.")]
        public Vector3 faceDirectionEulerOffset = Vector3.zero;

        [Header("Spread / Formation")]
        [Tooltip("How far this fish checks for neighbors.")]
        public float neighborDistance = 4.5f;

        [Tooltip("How close another fish can get before this fish pushes away.")]
        public float separationDistance = 1.4f;

        [Tooltip("Main spread control. Increase this to keep the school more open all the time.")]
        public float formationSpread = 2.5f;

        [Tooltip("Shapes the spread volume: X = side, Y = vertical, Z = forward.")]
        public Vector3 formationAxisSpread = new Vector3(1.4f, 1.8f, 1.2f);

        [Tooltip("How often this fish picks a new personal formation offset around the flock target.")]
        public float formationRepickInterval = 2.5f;

        [Tooltip("Pull toward this fish's personal target position around the flock target.")]
        public float targetWeight = 2.2f;

        [Tooltip("Pull toward the nearby school center.")]
        public float cohesionWeight = 0.7f;

        [Tooltip("Try to face roughly the same direction as nearby fish.")]
        public float alignmentWeight = 1.2f;

        [Tooltip("Push away from nearby fish.")]
        public float separationWeight = 2.8f;

        [Tooltip("Extra force pulling back toward the flock target when the fish drifts too far.")]
        public float boundaryWeight = 3.2f;

        [Header("Personal Motion")]
        [Tooltip("Smooth personal motion around the school. X = side, Y = vertical, Z = forward.")]
        public Vector3 personalNoiseAmplitude = new Vector3(0.6f, 1.0f, 0.5f);

        [Tooltip("Speed of the personal motion. X = side, Y = vertical, Z = forward.")]
        public Vector3 personalNoiseSpeed = new Vector3(0.35f, 0.45f, 0.30f);

        [Tooltip("Manual vertical bias added to this fish.")]
        public float verticalBias = 0f;

        [Tooltip("Random extra vertical bias per fish instance.")]
        public float verticalBiasRandomRange = 0.75f;

        [Header("Boundary")]
        [Range(0f, 1f)]
        [Tooltip("Start steering back when the fish reaches this percent of wanderSize.")]
        public float boundarySoftZone = 0.8f;

        private float speed;
        private float desiredSpeed;
        private Vector3 moveDirection;
        private Vector3 personalAnchorOffset;
        private Vector3 noiseSeeds;
        private float nextAnchorChangeTime;
        private float instanceVerticalBias;

        void Start()
        {
            if (baseScale == Vector3.zero)
                baseScale = transform.localScale;

            float randomScale = Random.Range(randomSizeRange.x, randomSizeRange.y);
            transform.localScale = baseScale * sizeMultiplier * randomScale;

            speed = Random.Range(speedRandomRange.x, speedRandomRange.y) * averageSpeed;
            desiredSpeed = speed;

            moveDirection = transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Random.onUnitSphere.normalized;

            if (moveDirection.sqrMagnitude < 0.0001f)
                moveDirection = Vector3.forward;

            noiseSeeds = new Vector3(
                Random.Range(0f, 1000f),
                Random.Range(0f, 1000f),
                Random.Range(0f, 1000f)
            );

            instanceVerticalBias = verticalBias + Random.Range(-verticalBiasRandomRange, verticalBiasRandomRange);

            PickNewFormationOffset(true);
        }

        void Update()
        {
            if (Time.time >= nextAnchorChangeTime)
                PickNewFormationOffset(false);

            if (ShouldUpdateSteeringThisFrame())
                UpdateSteering();

            UpdateMovement();
            UpdateVisualFacing();
        }

        bool ShouldUpdateSteeringThisFrame()
        {
            if (performance <= 0)
                return true;

            return Random.Range(0, performance + 1) == 0;
        }

        void UpdateSteering()
        {
            Vector3 flockTargetPosition = GetFlockTargetPosition();
            Vector3 formationTargetPosition = flockTargetPosition + personalAnchorOffset + GetPersonalNoiseOffset() + Vector3.up * instanceVerticalBias;

            Vector3 toFormation = formationTargetPosition - transform.position;
            Vector3 targetForce = SafeNormalize(toFormation) * targetWeight;

            Vector3 neighborCenter = Vector3.zero;
            Vector3 alignment = Vector3.zero;
            Vector3 separation = Vector3.zero;

            int neighborCount = 0;
            float summedNeighborSpeed = 0f;

            if (flock != null && flock.allFish != null)
            {
                for (int i = 0; i < flock.allFish.Count; i++)
                {
                    GameObject other = flock.allFish[i];

                    if (other == null || other == gameObject)
                        continue;

                    Vector3 toOther = other.transform.position - transform.position;
                    float distance = toOther.magnitude;

                    if (distance > neighborDistance)
                        continue;

                    neighborCount++;
                    neighborCenter += other.transform.position;

                    Fish_Upgrade otherUpgrade = other.GetComponent<Fish_Upgrade>();
                    if (otherUpgrade != null)
                    {
                        alignment += otherUpgrade.moveDirection;
                        summedNeighborSpeed += otherUpgrade.speed;
                    }
                    else
                    {
                        alignment += other.transform.forward;
                        summedNeighborSpeed += averageSpeed;
                    }

                    if (distance < separationDistance && distance > 0.0001f)
                    {
                        float pushStrength = 1f - (distance / separationDistance);
                        separation += (-toOther.normalized) * pushStrength;
                    }
                }
            }

            Vector3 cohesion = Vector3.zero;
            Vector3 alignmentForce = Vector3.zero;

            if (neighborCount > 0)
            {
                Vector3 center = neighborCenter / neighborCount;
                cohesion = SafeNormalize(center - transform.position) * cohesionWeight;
                alignmentForce = SafeNormalize(alignment / neighborCount) * alignmentWeight;

                float avgNeighborSpeed = summedNeighborSpeed / neighborCount;
                desiredSpeed = Mathf.Lerp(desiredSpeed, avgNeighborSpeed, 0.5f);
            }
            else
            {
                desiredSpeed = Mathf.Lerp(desiredSpeed, averageSpeed, 0.2f);
            }

            Vector3 boundaryForce = GetBoundaryForce(flockTargetPosition);

            Vector3 desiredDirection =
                targetForce +
                cohesion +
                alignmentForce +
                separation * separationWeight +
                boundaryForce;

            if (desiredDirection.sqrMagnitude > 0.0001f)
            {
                moveDirection = Vector3.Slerp(
                    moveDirection,
                    desiredDirection.normalized,
                    turnSpeed * Time.deltaTime
                ).normalized;
            }

            speed = Mathf.Lerp(speed, desiredSpeed, acceleration * Time.deltaTime);
        }

        void UpdateMovement()
        {
            transform.position += moveDirection * speed * Time.deltaTime;
        }

        void UpdateVisualFacing()
        {
            if (moveDirection.sqrMagnitude < 0.0001f)
                return;

            Quaternion desiredRotation =
                Quaternion.LookRotation(moveDirection, Vector3.up) *
                Quaternion.Euler(faceDirectionEulerOffset);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRotation,
                turnSpeed * Time.deltaTime
            );
        }

        Vector3 GetBoundaryForce(Vector3 flockTargetPosition)
        {
            if (flock == null)
                return Vector3.zero;

            float radius = Mathf.Max(0.01f, flock.wanderSize);
            float softRadius = radius * Mathf.Clamp01(boundarySoftZone);

            Vector3 toTarget = flockTargetPosition - transform.position;
            float distance = toTarget.magnitude;

            if (distance <= softRadius)
                return Vector3.zero;

            float blend = Mathf.InverseLerp(softRadius, radius, distance);

            if (distance > radius)
                blend = 1.5f;

            return SafeNormalize(toTarget) * boundaryWeight * blend;
        }

        Vector3 GetFlockTargetPosition()
        {
            if (flock == null)
                return transform.position;

            if (flock.target != null)
                return flock.target.transform.position;

            return GlobalFlock.goalPos;
        }

        Vector3 GetPersonalNoiseOffset()
        {
            float t = Time.time;

            Vector3 routeDir = moveDirection.sqrMagnitude > 0.0001f ? moveDirection.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, routeDir);

            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.Cross(Vector3.forward, routeDir);

            right.Normalize();
            Vector3 localUp = Vector3.Cross(routeDir, right).normalized;

            float side = Mathf.Lerp(
                -personalNoiseAmplitude.x,
                personalNoiseAmplitude.x,
                Mathf.PerlinNoise(noiseSeeds.x, t * personalNoiseSpeed.x)
            );

            float up = Mathf.Lerp(
                -personalNoiseAmplitude.y,
                personalNoiseAmplitude.y,
                Mathf.PerlinNoise(noiseSeeds.y, t * personalNoiseSpeed.y)
            );

            float forward = Mathf.Lerp(
                -personalNoiseAmplitude.z,
                personalNoiseAmplitude.z,
                Mathf.PerlinNoise(noiseSeeds.z, t * personalNoiseSpeed.z)
            );

            return right * side + localUp * up + routeDir * forward;
        }

        void PickNewFormationOffset(bool immediate)
        {
            Vector3 random = Random.insideUnitSphere;
            random.Scale(formationAxisSpread);
            personalAnchorOffset = random * formationSpread;

            float minInterval = Mathf.Max(0.1f, formationRepickInterval * 0.6f);
            float maxInterval = Mathf.Max(minInterval, formationRepickInterval * 1.4f);
            nextAnchorChangeTime = Time.time + Random.Range(minInterval, maxInterval);

            if (immediate && personalAnchorOffset.sqrMagnitude < 0.0001f)
                personalAnchorOffset = Vector3.up * 0.25f;
        }

        Vector3 SafeNormalize(Vector3 value)
        {
            if (value.sqrMagnitude < 0.0001f)
                return Vector3.zero;

            return value.normalized;
        }
    }
}
