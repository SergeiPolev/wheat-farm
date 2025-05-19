using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class GizmosExtension
{
    public static void DrawGizmosCircle(Vector3 pos, Vector3 normal, float radius, int numSegments)
    {
        // I think of normal as conceptually in the Y direction.  We find the
        // "forward" and "right" axes relative to normal and I think of them 
        // as the X and Z axes, though they aren't in any particular direction.
        // All that matters is that they're perpendicular to each other and on
        // the plane defined by pos and normal.
        Vector3 temp = (normal.x < normal.z) ? new Vector3(1f, 0f, 0f) : new Vector3(0f, 0f, 1f);
        Vector3 forward = Vector3.Cross(normal, temp).normalized;
        Vector3 right = Vector3.Cross(forward, normal).normalized;

        Vector3 prevPt = pos + (forward * radius);
        float angleStep = (Mathf.PI * 2f) / numSegments;
        for (int i = 0; i < numSegments; i++)
        {
            // Get the angle for the end of this segment.  If it's the last segment,
            // use the angle of the first point so the last segment meets up with
            // the first point exactly (regardless of floating point imprecision).
            float angle = (i == numSegments - 1) ? 0f : (i + 1) * angleStep;

            // Get the segment end point in local space, i.e. pretend as if the
            // normal was (0, 1, 0), forward was (0, 0, 1), right was (1, 0, 0),
            // and pos was (0, 0, 0).
            Vector3 nextPtLocal = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;

            // Transform from local to world coords.  nextPtLocal's x,z are distances
            // along its axes, so we want those as the distances along our right and
            // forward axes.
            Vector3 nextPt = pos + (right * nextPtLocal.x) + (forward * nextPtLocal.z);

            Gizmos.DrawLine(prevPt, nextPt);

            prevPt = nextPt;
        }
    }
}
