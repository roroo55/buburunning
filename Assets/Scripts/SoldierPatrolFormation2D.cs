using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public class SoldierPatrolFormation2D : MonoBehaviour
{
    public float moveSpeed = 2.4f;
    public float soldierSpacing = 1.6f;
    public float startDistance;

    readonly List<Transform> routePoints = new List<Transform>();
    readonly List<PatrollingSoldier2D> soldiers = new List<PatrollingSoldier2D>();
    float[] segmentLengths;
    float routeLength;
    float distanceTravelled;
    bool ready;

    public void ConfigureFromHierarchy(Transform soldiersRoot, Transform routeRoot)
    {
        if (ready || soldiersRoot == null || routeRoot == null)
        {
            return;
        }

        routePoints.Clear();
        for (int i = 0; i < routeRoot.childCount; i++)
        {
            routePoints.Add(routeRoot.GetChild(i));
        }

        routePoints.Sort((a, b) => ExtractPointIndex(a.name).CompareTo(ExtractPointIndex(b.name)));

        soldiers.Clear();
        soldiers.AddRange(
            soldiersRoot.GetComponentsInChildren<PatrollingSoldier2D>(true)
                .OrderBy(soldier => SoldierOrder(soldier.name)));

        if (routePoints.Count < 2 || soldiers.Count == 0)
        {
            Debug.LogWarning("Soldier patrol formation needs route points and soldiers.", this);
            return;
        }

        segmentLengths = new float[routePoints.Count];
        routeLength = 0f;
        for (int i = 0; i < routePoints.Count; i++)
        {
            int next = (i + 1) % routePoints.Count;
            segmentLengths[i] = Vector2.Distance(routePoints[i].position, routePoints[next].position);
            routeLength += segmentLengths[i];
        }

        if (routeLength <= 0.001f)
        {
            Debug.LogWarning("Soldier patrol route has no usable length.", this);
            return;
        }

        foreach (PatrollingSoldier2D soldier in soldiers)
        {
            soldier.UseFormationMovement();
        }

        moveSpeed = soldiers[0].patrolSpeed;
        soldierSpacing = Mathf.Max(0.1f, soldiers[0].formationSpacing);
        startDistance = soldiers[0].formationStartDistance;
        distanceTravelled = Mathf.Repeat(startDistance, routeLength);
        ready = true;
        ApplyFormationPositions();
    }

    void Update()
    {
        if (!ready)
        {
            return;
        }

        distanceTravelled = Mathf.Repeat(distanceTravelled + moveSpeed * Time.deltaTime, routeLength);
        ApplyFormationPositions();
    }

    void ApplyFormationPositions()
    {
        for (int i = 0; i < soldiers.Count; i++)
        {
            float soldierDistance = Mathf.Repeat(distanceTravelled - soldierSpacing * i, routeLength);
            Vector2 direction;
            Vector3 position = EvaluateRoute(soldierDistance, out direction);
            soldiers[i].SetFormationPosition(position, direction);
        }
    }

    Vector3 EvaluateRoute(float distance, out Vector2 direction)
    {
        for (int i = 0; i < routePoints.Count; i++)
        {
            float length = segmentLengths[i];
            if (distance <= length || i == routePoints.Count - 1)
            {
                int next = (i + 1) % routePoints.Count;
                Vector3 from = routePoints[i].position;
                Vector3 to = routePoints[next].position;
                direction = ((Vector2)(to - from)).normalized;
                float t = length > 0.001f ? Mathf.Clamp01(distance / length) : 0f;
                return Vector3.Lerp(from, to, t);
            }

            distance -= length;
        }

        direction = Vector2.right;
        return routePoints[0].position;
    }

    static int ExtractPointIndex(string pointName)
    {
        string digits = new string(pointName.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out int index) ? index : int.MaxValue;
    }

    static int SoldierOrder(string soldierName)
    {
        if (!soldierName.Contains("("))
        {
            return 0;
        }

        string digits = new string(soldierName.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out int index) ? index : int.MaxValue;
    }

    void OnDrawGizmosSelected()
    {
        Transform route = transform.Find("Route");
        if (route == null || route.childCount < 2)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.75f, 0.1f, 1f);
        for (int i = 0; i < route.childCount; i++)
        {
            Transform from = route.GetChild(i);
            Transform to = route.GetChild((i + 1) % route.childCount);
            Gizmos.DrawLine(from.position, to.position);
            Gizmos.DrawWireSphere(from.position, 0.12f);
        }
    }
}
