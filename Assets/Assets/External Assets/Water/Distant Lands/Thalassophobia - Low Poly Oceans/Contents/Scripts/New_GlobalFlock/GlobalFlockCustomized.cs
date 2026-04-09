using UnityEngine;
using System.Collections.Generic;

namespace DistantLands
{
    public class GlobalFlockCustomized : GlobalFlock
    {
        [Header("Route Points")]
        public List<Transform> routePoints = new List<Transform>();
        public float pointReachDistance = 2f;
        public bool spawnAtPoint0 = true;
        public bool loopRoute = true;

        [Header("Default Route Motion")]
        public WaypointBehaviorSettings defaultMotion = new WaypointBehaviorSettings();

        private int currentPointIndex = 0;
        private int previousPointIndex = 0;

        private Vector3 smoothedDynamicOffset = Vector3.zero;

        private float noiseSeedX;
        private float noiseSeedY;
        private float noiseSeedZ;
        private float waveSeedA;
        private float waveSeedB;
        private float waveSeedC;

        new void Start()
        {
            allFish = new List<GameObject>();

            noiseSeedX = Random.Range(0f, 1000f);
            noiseSeedY = Random.Range(0f, 1000f);
            noiseSeedZ = Random.Range(0f, 1000f);
            waveSeedA = Random.Range(0f, 1000f);
            waveSeedB = Random.Range(0f, 1000f);
            waveSeedC = Random.Range(0f, 1000f);

            Vector3 spawnCenter = GetSpawnCenter();

            for (int i = 0; i < numFish; i++)
            {
                GameObject fish = Instantiate(
                    fishPrefabs[Random.Range(0, fishPrefabs.Length)],
                    transform.position + Random.insideUnitSphere * wanderSize,
                    Quaternion.identity);

                fish.transform.parent = fishSchool.transform;
                fish.transform.localScale = Vector3.one * (Random.value * .2f + 0.9f);

                Fish_Upgrade fishComponent = fish.GetComponent<Fish_Upgrade>();
                if (fishComponent != null)
                    fishComponent.flock = this;

                allFish.Add(fish);
            }

            InitializeRoute();
        }

        new void Update()
        {
            HandleRoute();
        }

        void InitializeRoute()
        {
            if (routePoints == null || routePoints.Count == 0)
            {
                GlobalFlock.goalPos = transform.position;
                UpdateDebugTarget();
                return;
            }

            previousPointIndex = 0;
            currentPointIndex = routePoints.Count == 1 ? 0 : 1;

            UpdateGoalPosition();
        }

        void HandleRoute()
        {
            if (routePoints == null || routePoints.Count == 0)
                return;

            if (routePoints[currentPointIndex] == null)
                return;

            UpdateGoalPosition();

            Vector3 schoolCenter = GetSchoolCenter();
            Vector3 currentBasePoint = routePoints[currentPointIndex].position;

            if (Vector3.Distance(schoolCenter, currentBasePoint) <= pointReachDistance)
            {
                previousPointIndex = currentPointIndex;
                AdvancePoint();
                UpdateGoalPosition();
            }
        }

        void AdvancePoint()
        {
            if (routePoints == null || routePoints.Count == 0)
            {
                currentPointIndex = 0;
                return;
            }

            if (routePoints.Count == 1)
            {
                currentPointIndex = 0;
                return;
            }

            currentPointIndex++;

            if (currentPointIndex >= routePoints.Count)
            {
                currentPointIndex = loopRoute ? 0 : routePoints.Count - 1;
            }
        }

        void UpdateGoalPosition()
        {
            Vector3 baseGoal = GetCurrentBaseGoal();
            Vector3 routeDir = GetCurrentRouteDirection();

            WaypointBehaviorSettings resolved = ResolveMotionProfile();
            Vector3 dynamicOffset = GetDynamicRouteOffset(routeDir, resolved);

            smoothedDynamicOffset = Vector3.Lerp(
                smoothedDynamicOffset,
                dynamicOffset,
                Time.deltaTime * Mathf.Max(0.01f, resolved.offsetSmoothSpeed)
            );

            GlobalFlock.goalPos = baseGoal + smoothedDynamicOffset + routeDir * resolved.forwardLookDistance;
            UpdateDebugTarget();
        }

        WaypointBehaviorSettings ResolveMotionProfile()
        {
            WaypointBehaviorSettings resolved = new WaypointBehaviorSettings(defaultMotion);

            FishRoutePointBehavior pointBehavior = GetCurrentPointBehavior();
            if (pointBehavior == null || !pointBehavior.enableBehavior)
                return resolved;

            float schoolDistance = Vector3.Distance(GetSchoolCenter(), routePoints[currentPointIndex].position);

            float distanceBlend = 0f;
            if (pointBehavior.behaviorStartDistance > pointReachDistance)
            {
                distanceBlend = Mathf.InverseLerp(
                    pointBehavior.behaviorStartDistance,
                    pointReachDistance,
                    schoolDistance
                );
            }
            else
            {
                distanceBlend = schoolDistance <= pointBehavior.behaviorStartDistance ? 1f : 0f;
            }

            distanceBlend = Mathf.Clamp01(distanceBlend);
            distanceBlend = Mathf.Pow(distanceBlend, Mathf.Max(0.01f, pointBehavior.behaviorBlendPower));

            float finalBlend = distanceBlend * Mathf.Clamp01(pointBehavior.behaviorStrength);

            resolved.noiseAmplitude = Vector3.Lerp(defaultMotion.noiseAmplitude, pointBehavior.behavior.noiseAmplitude, finalBlend);
            resolved.noiseSpeed = Vector3.Lerp(defaultMotion.noiseSpeed, pointBehavior.behavior.noiseSpeed, finalBlend);
            resolved.sideWaveAmplitude = Mathf.Lerp(defaultMotion.sideWaveAmplitude, pointBehavior.behavior.sideWaveAmplitude, finalBlend);
            resolved.sideWaveSpeed = Mathf.Lerp(defaultMotion.sideWaveSpeed, pointBehavior.behavior.sideWaveSpeed, finalBlend);
            resolved.verticalWaveAmplitude = Mathf.Lerp(defaultMotion.verticalWaveAmplitude, pointBehavior.behavior.verticalWaveAmplitude, finalBlend);
            resolved.verticalWaveSpeed = Mathf.Lerp(defaultMotion.verticalWaveSpeed, pointBehavior.behavior.verticalWaveSpeed, finalBlend);
            resolved.forwardWaveAmplitude = Mathf.Lerp(defaultMotion.forwardWaveAmplitude, pointBehavior.behavior.forwardWaveAmplitude, finalBlend);
            resolved.forwardWaveSpeed = Mathf.Lerp(defaultMotion.forwardWaveSpeed, pointBehavior.behavior.forwardWaveSpeed, finalBlend);
            resolved.offsetSmoothSpeed = Mathf.Lerp(defaultMotion.offsetSmoothSpeed, pointBehavior.behavior.offsetSmoothSpeed, finalBlend);
            resolved.forwardLookDistance = Mathf.Lerp(defaultMotion.forwardLookDistance, pointBehavior.behavior.forwardLookDistance, finalBlend);

            return resolved;
        }

        FishRoutePointBehavior GetCurrentPointBehavior()
        {
            if (routePoints == null || routePoints.Count == 0)
                return null;

            Transform point = routePoints[currentPointIndex];
            if (point == null)
                return null;

            return point.GetComponent<FishRoutePointBehavior>();
        }

        Vector3 GetCurrentBaseGoal()
        {
            if (routePoints == null || routePoints.Count == 0)
                return transform.position;

            Transform currentPoint = routePoints[currentPointIndex];
            return currentPoint != null ? currentPoint.position : transform.position;
        }

        Vector3 GetCurrentRouteDirection()
        {
            if (routePoints == null || routePoints.Count == 0)
                return transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.forward;

            Transform prev = null;
            Transform curr = null;

            if (previousPointIndex >= 0 && previousPointIndex < routePoints.Count)
                prev = routePoints[previousPointIndex];

            if (currentPointIndex >= 0 && currentPointIndex < routePoints.Count)
                curr = routePoints[currentPointIndex];

            if (prev != null && curr != null)
            {
                Vector3 dir = curr.position - prev.position;
                if (dir.sqrMagnitude > 0.0001f)
                    return dir.normalized;
            }

            return transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.forward;
        }

        Vector3 GetDynamicRouteOffset(Vector3 routeDir, WaypointBehaviorSettings settings)
        {
            float t = Time.time;

            Vector3 worldUp = Vector3.up;
            Vector3 right = Vector3.Cross(worldUp, routeDir);

            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.Cross(Vector3.forward, routeDir);

            right.Normalize();
            Vector3 localUp = Vector3.Cross(routeDir, right).normalized;

            float sideNoise = RemapPerlin(
                Mathf.PerlinNoise(noiseSeedX, t * settings.noiseSpeed.x),
                -settings.noiseAmplitude.x,
                settings.noiseAmplitude.x
            );

            float upNoise = RemapPerlin(
                Mathf.PerlinNoise(noiseSeedY, t * settings.noiseSpeed.y),
                -settings.noiseAmplitude.y,
                settings.noiseAmplitude.y
            );

            float forwardNoise = RemapPerlin(
                Mathf.PerlinNoise(noiseSeedZ, t * settings.noiseSpeed.z),
                -settings.noiseAmplitude.z,
                settings.noiseAmplitude.z
            );

            float sideWave = Mathf.Sin(t * settings.sideWaveSpeed + waveSeedA) * settings.sideWaveAmplitude;
            float verticalWave = Mathf.Sin(t * settings.verticalWaveSpeed + waveSeedB) * settings.verticalWaveAmplitude;
            float forwardWave = Mathf.Sin(t * settings.forwardWaveSpeed + waveSeedC) * settings.forwardWaveAmplitude;

            return
                right * (sideNoise + sideWave) +
                localUp * (upNoise + verticalWave) +
                routeDir * (forwardNoise + forwardWave);
        }

        float RemapPerlin(float value, float min, float max)
        {
            return Mathf.Lerp(min, max, value);
        }

        Vector3 GetSpawnCenter()
        {
            if (spawnAtPoint0 && routePoints != null && routePoints.Count > 0 && routePoints[0] != null)
                return routePoints[0].position;

            return transform.position;
        }

        Vector3 GetSchoolCenter()
        {
            if (allFish == null || allFish.Count == 0)
                return transform.position;

            Vector3 sum = Vector3.zero;
            int validCount = 0;

            for (int i = 0; i < allFish.Count; i++)
            {
                if (allFish[i] == null)
                    continue;

                sum += allFish[i].transform.position;
                validCount++;
            }

            return validCount > 0 ? sum / validCount : transform.position;
        }

        void UpdateDebugTarget()
        {
            if (target != null)
                target.transform.position = GlobalFlock.goalPos;
        }

        new void OnDrawGizmos()
        {
            Gizmos.color = Color.blue;

            if (routePoints != null && routePoints.Count > 0)
            {
                for (int i = 0; i < routePoints.Count; i++)
                {
                    if (routePoints[i] == null)
                        continue;

                    Gizmos.DrawWireSphere(routePoints[i].position, 0.35f);

                    int nextIndex = i + 1;
                    if (nextIndex >= routePoints.Count)
                        nextIndex = loopRoute ? 0 : i;

                    if (nextIndex != i && routePoints[nextIndex] != null)
                        Gizmos.DrawLine(routePoints[i].position, routePoints[nextIndex].position);
                }
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, wanderSize);
            }
        }
    }
}
