using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

public class UpdatePolygonCollider : MonoBehaviour
{
	public SpriteRenderer SpriteRenderer;
	[Button]
	public void UpdatePolygon()
	{
		var collider = GetComponent<PolygonCollider2D>();
		var sprite = SpriteRenderer.sprite;
		
		if (collider != null && sprite != null) {
			// update count
			collider.pathCount = sprite.GetPhysicsShapeCount();
                
			// new paths variable
			List<Vector2> path = new List<Vector2>();

			// loop path count
			for (int i = 0; i < collider.pathCount; i++) {
				// clear
				path.Clear();
				// get shape
				sprite.GetPhysicsShape(i, path);
				// set path
				collider.SetPath(i, path.ToArray());
			}
		}
	}
}