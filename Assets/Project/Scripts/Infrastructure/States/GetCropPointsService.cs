using System.Collections.Generic;
using Services;
using UnityEngine;

namespace Infrastructure
{
    public class GetCropPointsService : IService
    {
        private CropFieldData[] FieldDatas;
        
        public void OnLevelInit()
        {
            FieldDatas = GameObject.FindObjectsByType<CropFieldData>(FindObjectsSortMode.None);
        }

        public List<CropFieldData> GetCropRenderersInRadius(Vector3 position, float radius)
        {
            var renderers = new List<CropFieldData>();

            Bounds intersect = new Bounds(position, radius * 2 * Vector3.one + Vector3.up * 100f);

            DrawBoundsLines(intersect);
            
            foreach (var item in FieldDatas)
            {
                if (item.GetBounds.Intersects(intersect))
                {
                    renderers.Add(item);
                }
            }
            
            return renderers;
        }
        
        void DrawBoundsLines(Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Color gizmoColor = Color.yellow;
            
            // Bottom rectangle
            Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), gizmoColor);
            Debug.DrawLine(new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), gizmoColor);
            Debug.DrawLine(new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z), gizmoColor);
            Debug.DrawLine(new Vector3(min.x, min.y, max.z), new Vector3(min.x, min.y, min.z), gizmoColor);

            // Top rectangle
            Debug.DrawLine(new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), gizmoColor);
            Debug.DrawLine(new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z), gizmoColor);
            Debug.DrawLine(new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z), gizmoColor);
            Debug.DrawLine(new Vector3(min.x, max.y, max.z), new Vector3(min.x, max.y, min.z), gizmoColor);

            // Vertical lines
            Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), gizmoColor);
            Debug.DrawLine(new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), gizmoColor);
            Debug.DrawLine(new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), gizmoColor);
            Debug.DrawLine(new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z), gizmoColor);
        }
    }
}