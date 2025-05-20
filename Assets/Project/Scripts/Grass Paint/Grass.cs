using System;
using NaughtyAttributes;
using UnityEngine;

public class Grass : MonoBehaviour
{
	[SerializeField] private float lerpSpeed = 1.5f;
	[SerializeField] private MeshRenderer meshRenderer;
	[SerializeField] private Ground _ground;
	
	private Mesh mesh;
	private Vector3[] verts;
	private Vector3 _lastColoredVector;
	private Color32[] vertColors;
	private Color32[] targetColors;

	[SerializeField, ReadOnly] private float coloredVerts;

	private static readonly int InteractionPosition = Shader.PropertyToID("_Interaction_Position");
	private static readonly int Height = Shader.PropertyToID("_Height");
	private static readonly int Picture = Shader.PropertyToID("_Picture");
	private static readonly int UVOffset = Shader.PropertyToID("_UV_Offset");
	private static readonly int PictureScale = Shader.PropertyToID("_Picture_Scale");

	public float GetProgress => coloredVerts / verts.Length;

	public event Action<float, Vector3> OnMoreColored;

	private void Awake()
	{
		mesh = meshRenderer.GetComponent<MeshFilter>().mesh;
		verts = mesh.vertices;
		vertColors = new Color32[mesh.vertices.Length];
		targetColors = new Color32[mesh.vertices.Length];

		_ground.Init();
		
		ApplyPaint(Vector3.zero, 0, 0);
		
		UpdateGrass();
	}

	public void UpdateGrass()
	{
		for (int i = 0; i < verts.Length; i++)
		{
			vertColors[i] = Color32.Lerp(vertColors[i], targetColors[i], Time.deltaTime * lerpSpeed);
		}

		mesh.colors32 = vertColors;
		
		_ground.UpdateGround();
	}

	public void UpdatePlayerPosition(Vector3 position)
	{
		meshRenderer.material.SetVector(InteractionPosition, position);
	}

	public bool IsColored(Vector3 position, float innerRadius)
	{
		bool isColored = true;

		Vector3 center = transform.InverseTransformPoint(position);

		float innerR = transform.InverseTransformVector(innerRadius * Vector3.right).magnitude;
		float innerRsqr = innerR * innerR;

		for (int i = 0; i < verts.Length; i++)
		{
			Vector3 delta = verts[i] - center;
			float dsqr = delta.sqrMagnitude;

			if (dsqr <= innerRsqr)
			{
				Color color = targetColors[i];
				if (color.r < .99f)
				{
					isColored = false;
				}
			}
		}

		return isColored;
	}

	public void ApplyPaintInSight(Vector3 targetPos, Vector3 originPos, float radius, float smallR = 2f, float maxAngle = 15f, float coloringSpeed = 2f)
	{
		originPos.y = targetPos.y;
		Vector3 center = transform.InverseTransformPoint(targetPos);
		Vector3 origin = transform.InverseTransformPoint(originPos);
		Vector3 mainDir = center - origin;
		
		float outerR = transform.InverseTransformVector(radius * Vector3.right).magnitude;
		float smallRadius = transform.InverseTransformVector(smallR * Vector3.right).magnitude;
		float maxRSqr = outerR * outerR;
		float minRSqr = smallRadius * smallRadius;

		var coloredCount = 0;

		_ground.ApplyPaintInSight(targetPos + Vector3.up * 2.2f, originPos, radius, smallR, maxAngle, coloringSpeed);
		
		for (int i = 0; i < verts.Length; i++)
		{
			Vector3 delta = verts[i] - origin;
			float dsqr = delta.sqrMagnitude;

			if (minRSqr >= dsqr)
			{
				Color color = targetColors[i];
				targetColors[i].a = 255;

				float vertColor = color.r + Time.deltaTime * coloringSpeed;
				targetColors[i] = new Color(vertColor, vertColor, vertColor, 1);
				_lastColoredVector = verts[i];
			}
			else if (dsqr <= maxRSqr)
			{
				var angle = Vector3.Angle(mainDir, delta);
				if (angle <= maxAngle)
				{
					Color color = targetColors[i];
					targetColors[i].a = 255;

					float vertColor = color.r + Time.deltaTime * coloringSpeed;
					targetColors[i] = new Color(vertColor, vertColor, vertColor, 1);
					_lastColoredVector = verts[i];
				}
			}

			if (targetColors[i].r >= 1f)
			{
				coloredCount++;
			}
		}

		if (coloredVerts < coloredCount)
		{
			var delta = coloredCount - coloredVerts;
			coloredVerts = coloredCount;

			Vector3 point = transform.TransformPoint(_lastColoredVector) 
			                + Vector3.up 
			                * (meshRenderer.material.GetFloat(Height) + 1);
			
			OnMoreColored?.Invoke(delta, point);
		}
		
	}
	public void ApplyPaint(Vector3 position, float innerRadius, float outerRadius, float coloringSpeed = 2f)
	{
		Vector3 center = transform.InverseTransformPoint(position);

		float outerR = transform.InverseTransformVector(outerRadius * Vector3.right).magnitude;
		float innerR = innerRadius * outerR / outerRadius;
		float innerRsqr = innerR * innerR;
		float outerRsqr = outerR * outerR;

		var coloredCount = 0;
		
		_ground.ApplyPaintInSight(position + Vector3.up * 2.2f, position, innerRadius, innerRadius, 0, 1);

		for (int i = 0; i < verts.Length; i++)
		{
			Vector3 delta = verts[i] - center;
			float dsqr = delta.sqrMagnitude;

			if (dsqr <= outerRsqr)
			{
				Color color = targetColors[i];
				targetColors[i].a = 255;

				if (dsqr < innerRsqr)
				{
					if (color.r < 1)
					{
						_lastColoredVector = verts[i];
					}
					
					float vertColor = color.r + Time.deltaTime * coloringSpeed;
					targetColors[i] = new Color(vertColor, vertColor, vertColor, 1);
				}
				else
				{
					float d = Mathf.Sqrt(dsqr);
					float blobColor = color.r + (1 - (d - innerR) / outerR) * Time.deltaTime * coloringSpeed;

					Color newColor = new Color(blobColor, blobColor, blobColor, 1);

					if (newColor.r >= color.r)
					{
						targetColors[i] = newColor;
						_lastColoredVector = verts[i];
					}
				}
			}

			if (targetColors[i].r >= .95f)
			{
				coloredCount++;
			}
		}

		if (coloredVerts < coloredCount)
		{
			var delta = coloredCount - coloredVerts;
			coloredVerts = coloredCount;

			Vector3 point = transform.TransformPoint(_lastColoredVector) 
			                + Vector3.up 
			                * (meshRenderer.material.GetFloat(Height) + 1);
			
			OnMoreColored?.Invoke(delta, point);
		}
	}

	public void SetTexture(Texture2D texture)
	{
		meshRenderer.material.SetTexture(Picture, texture);
	}

	public void SetOffsetAndScale(Vector2 offset, float scale)
	{
		meshRenderer.material.SetVector(UVOffset, offset);
		meshRenderer.material.SetFloat(PictureScale, scale);
	}

	public void CompleteGrass()
	{
		for (int i = 0; i < verts.Length; i++)
		{
			vertColors[i] = new Color32(255, 255, 255, 255);
			targetColors[i] = new Color32(255, 255, 255, 255);
		}
		
		_ground.CompleteGround();
		mesh.colors32 = vertColors;
	}
}