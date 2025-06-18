using UnityEngine;

public class Ground : MonoBehaviour
{
	[SerializeField] private MeshRenderer meshRenderer;
	[SerializeField] private MeshFilter filter;
	
	private Mesh mesh;
	private Vector3[] verts;
	private Color32[] vertColors;
	private Color32[] targetColors;

	public void Init()
	{
		mesh = filter.mesh;
		verts = mesh.vertices;
		vertColors = new Color32[mesh.vertices.Length];
		targetColors = new Color32[mesh.vertices.Length];
	}

	public void UpdateGround(float lerpSpeed = 1)
	{
		for (int i = 0; i < verts.Length; i++)
		{
			vertColors[i] = Color32.Lerp(vertColors[i], targetColors[i], Time.deltaTime * lerpSpeed * 4f);
		}

		mesh.colors32 = vertColors;
	}

	public void ApplyPaintInSight(Color paintColor, Vector3 targetPos, Vector3 originPos, float radius, float smallR = 2f, float maxAngle = 15f, float coloringSpeed = 2f)
	{
		originPos.y = targetPos.y;
		Vector3 center = transform.InverseTransformPoint(targetPos);
		Vector3 origin = transform.InverseTransformPoint(originPos);
		Vector3 mainDir = center - origin;
		
		float outerR = transform.InverseTransformVector((radius + .2f) * Vector3.right).magnitude;
		float smallRadius = transform.InverseTransformVector((smallR + .2f) * Vector3.right).magnitude;
		float maxRSqr = outerR * outerR;
		float minRSqr = smallRadius * smallRadius;

		var coloredCount = 0;

		for (int i = 0; i < verts.Length; i++)
		{
			Vector3 delta = verts[i] - origin;
			float dsqr = delta.sqrMagnitude;

			if (minRSqr >= dsqr)
			{
				Color color = targetColors[i];

				float vertColor = paintColor.a == 0 ? 0 : color.r + Time.deltaTime * coloringSpeed;
				targetColors[i] = new Color(paintColor.r, paintColor.g, paintColor.b, vertColor);
			}
			else if (dsqr <= maxRSqr)
			{
				var angle = Vector3.Angle(mainDir, delta);
				if (angle <= maxAngle + 2)
				{
					Color color = targetColors[i];

					float vertColor = paintColor.a == 0 ? 0 : color.a + Time.deltaTime * coloringSpeed;
					targetColors[i] = new Color(paintColor.r, paintColor.g, paintColor.b, vertColor);
				}
			}
		}

	}

	public void CompleteGround()
	{
		for (int i = 0; i < verts.Length; i++)
		{
			vertColors[i] = new Color32(255, 255, 255, 255);
			targetColors[i] = new Color32(255, 255, 255, 255);
		}

		mesh.colors32 = vertColors;
	}
}