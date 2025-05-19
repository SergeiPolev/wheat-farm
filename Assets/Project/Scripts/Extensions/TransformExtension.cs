using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class TransformExtension
{
    #region Closest Variations

    public static Transform Closest(this Transform from, List<Transform> to)
    {
        float minDistance = float.MaxValue;
        var closest = to[0];

        for (var i = 0; i < to.Count; i++)
        {
            var d = (from.position - to[i].position).sqrMagnitude;
            if (d < minDistance)
            {
                minDistance = d;
                closest = to[i];
            }
        }
            
        return closest;
    }
        
    public static Transform Closest(this Transform from, Component[] to)
    {
        float minDistance = float.MaxValue;
        var closest = to[0];
            
        for (var i = 0; i < to.Length; i++)
        {
            var d = (from.position - to[i].transform.position).sqrMagnitude;
            if (d < minDistance)
            {
                minDistance = d;
                closest = to[i];
            }
        }

        return closest.transform;
    }
        
    public static Transform Closest(this Transform from, List<Component> to)
    {
        float minDistance = float.MaxValue;
        var closest = to[0];
            
        for (var i = 0; i < to.Count; i++)
        {
            var d = (from.position - to[i].transform.position).sqrMagnitude;
            if (d < minDistance)
            {
                minDistance = d;
                closest = to[i];
            }
        }

        return closest.transform;
    }
        
    public static T Closest<T>(this Transform from, IEnumerable<T> targets)where T: Component
    {
        float minDistance = float.MaxValue;
        var closest = targets.First();
            
        foreach (T t in targets)
        {
            var d = (from.position - t.transform.position).sqrMagnitude;
            if (d < minDistance)
            {
                minDistance = d;
                closest = t;
            }
        }
        return closest;
    }
        
    public static T Closest<T>(this Transform from, List<T> to) where T: Component
    {
        float minDistance = float.MaxValue;
        var closest = to[0];
            
        for (var i = 0; i < to.Count; i++)
        {
            var d = (from.position - to[i].transform.position).sqrMagnitude;
            if (d < minDistance)
            {
                minDistance = d;
                closest = to[i];
            }
        }

        return closest;
    }

    #endregion
    
        public static T ClosestOfType<T>(this Transform from, List<Collider> to) where T: Component
        {
            float minDistance = float.MaxValue;
            T component;
            T closestComponent = null;
            
            foreach (var item in to)
            {
                var d = (from.position - item.transform.position).sqrMagnitude;
                if (d < minDistance)
                {
                    if (item.TryGetComponent(out component))
                    {
                        closestComponent = component;
                        minDistance = d;
                    }
                }
            }

            return closestComponent;
        }
}