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

        private int currentPointIndex = 0;

        new void Start()
        {
            allFish = new List<GameObject>();

            Vector3 spawnCenter = GetSpawnCenter();

            for (int i = 0; i < numFish; i++)
            {
                GameObject fish = Instantiate(
                    fishPrefabs[Random.Range(0, fishPrefabs.Length)],
                    spawnCenter + Random.insideUnitSphere * wanderSize,
                    Quaternion.identity
                );

                if (fishSchool != null)
                    fish.transform.parent = fishSchool.transform;

                fish.transform.localScale = Vector3.one * (Random.value * 0.2f + 0.9f);

                Fish fishComponent = fish.GetComponent<Fish>();
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

            if (routePoints.Count == 1)
                currentPointIndex = 0;
            else
                currentPointIndex = 1;

            SetGoalToCurrentPoint();
            UpdateDebugTarget();
        }

        void HandleRoute()
        {
            if (routePoints == null || routePoints.Count == 0)
                return;

            if (routePoints[currentPointIndex] == null)
                return;

            SetGoalToCurrentPoint();
            UpdateDebugTarget();

            Vector3 schoolCenter = GetSchoolCenter();

            if (Vector3.Distance(schoolCenter, routePoints[currentPointIndex].position) <= pointReachDistance)
            {
                AdvancePoint();
                SetGoalToCurrentPoint();
                UpdateDebugTarget();
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
                if (loopRoute)
                    currentPointIndex = 0;
                else
                    currentPointIndex = routePoints.Count - 1;
            }
        }

        void SetGoalToCurrentPoint()
        {
            if (routePoints == null || routePoints.Count == 0)
            {
                GlobalFlock.goalPos = transform.position;
                return;
            }

            Transform currentPoint = routePoints[currentPointIndex];

            if (currentPoint != null)
                GlobalFlock.goalPos = currentPoint.position;
            else
                GlobalFlock.goalPos = transform.position;
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

            if (validCount == 0)
                return transform.position;

            return sum / validCount;
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