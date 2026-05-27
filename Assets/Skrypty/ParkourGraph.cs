using UnityEngine;
using System.Collections.Generic;

public class ParkourGraph : MonoBehaviour
{
    public List<Vector3> roofPoints = new List<Vector3>();
    public Dictionary<Vector3, List<Vector3>> graph = new Dictionary<Vector3, List<Vector3>>();

    public float maxJumpDistance = 8f;
    public float maxUp = 4f;
    public float maxDown = 6f;

    public void BuildGraph()
    {
        graph.Clear();

        foreach (var a in roofPoints)
        {
            graph[a] = new List<Vector3>();

            foreach (var b in roofPoints)
            {
                if (a == b) continue;

                if (CanJump(a, b))
                    graph[a].Add(b);
            }
        }
    }

    bool CanJump(Vector3 a, Vector3 b)
    {
        float dist = Vector3.Distance(a, b);
        float dy = b.y - a.y;

        return dist < maxJumpDistance &&
               dy < maxUp &&
               dy > -maxDown;
    }
}