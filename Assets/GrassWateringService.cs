using System;
using UnityEngine;

public class GrassWateringService : MonoBehaviour
{
	public GameObject _player;

	private Collider[] _grass;
	private Collider[] _waterable;
	private Grass[] _grassComps;

	private Color _paintColor;
	private Camera _camera;
	private Plane _plane;

	private float _currentRadius;
	private float _dampingRadius;

	private int _foundGrasses;

	private bool _gameEnded;

	public event Action OnDraw;


	public void OnEnable()
	{
		_grass = new Collider[25];
		_waterable = new Collider[10];
		_camera = Camera.main;
		_plane = new Plane(Vector3.up, 0);
		UpdateRadius();
	}

	private void UpdateRadius()
	{
		float upgradeValue = 4f;
		_currentRadius = upgradeValue;
		_dampingRadius = 0.8f * upgradeValue;
	}

	public void Update()
	{
		CheckGrasses();
		
		if (Input.GetKeyDown(KeyCode.Alpha1))
		{
			ColorUtility.TryParseHtmlString("#db6161", out var color);
			_paintColor = color;
		}
		else if (Input.GetKeyDown(KeyCode.Alpha2))
		{
			ColorUtility.TryParseHtmlString("#5D7A8A", out var color);
			_paintColor = color;
		}
		else if (Input.GetKeyDown(KeyCode.Alpha3))
		{
			ColorUtility.TryParseHtmlString("#FFD3B6", out var color);
			_paintColor = color;
		}
		else if (Input.GetKeyDown(KeyCode.Alpha4))
		{
			_paintColor = new Color(0, 0, 0, 0);
		}
	}

	private void CheckGrasses()
	{
		var isPlayerOnGrass = IsOnGrass(out var playerHit, _player.transform.position);
		var isAimOnGrass = IsOnGrass(out var hit, GetWateringPoint());

		if (isAimOnGrass)
		{
			// Watering
			
			PaintGrasses(hit);
		}
		else
		{
			// Not watering
		}
		
		if (isPlayerOnGrass)
		{
			UpdatePlayerPosition(playerHit);
		}
	}

	private void PaintGrasses(RaycastHit hit)
	{
		_foundGrasses = Physics.OverlapSphereNonAlloc(hit.point, _currentRadius, _grass, LayerManager.GrassLayerMask);
		_grassComps = new Grass[_foundGrasses];
		bool needColoring = false;

		for (int index = 0; index < _foundGrasses; index++)
		{
			var item = _grass[index];
			if (item.TryGetComponent(out Grass grass))
			{
				_grassComps[index] = grass;
				
				//if (!grass.IsColored(hit.point, _currentRadius - _dampingRadius))
				if (!grass.IsColored(hit.point, 1f))
				{
					needColoring = true;
				}
			}
		}

		// If grass is not colored under pointer
		if (needColoring)
		{
			ColorGrass(hit);

			/*var waterablesCount =
				Physics.OverlapSphereNonAlloc(hit.point, _currentRadius, _waterable, GameConfig.WATERABLE_LAYER);

			for (int i = 0; i < waterablesCount; i++)
			{
				if (_waterable[i].TryGetComponent(out Waterable waterable))
				{
					waterable.Activate();
				}
			}*/
		}
		else
		{
			// Not coloring
		}
	}

	private void UpdatePlayerPosition(RaycastHit raycastHit)
	{
		Collider[] grasses = new Collider[25];
		var grassCount =
			Physics.OverlapSphereNonAlloc(raycastHit.point, 5f, grasses, LayerManager.GrassLayerMask);

		for (int i = 0; i < grassCount; i++)
		{
			if (grasses[i].TryGetComponent(out Grass grass))
			{
				grass.UpdatePlayerPosition(_player.transform.position + Vector3.down * 2.2f);
				grass.UpdateGrass();
			}
		}
	}

	private Vector3 GetWateringPoint()
	{
		Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
		Vector3 wateringOffset = _player.transform.position;
		if (_plane.Raycast(ray, out float hit))
		{
			wateringOffset = ray.GetPoint(hit);
		}
		
		var point = new Vector3(
			wateringOffset.x,
			wateringOffset.y, 
			wateringOffset.z);

		return point;
	}

	private bool IsOnGrass(out RaycastHit hit, Vector3 position)
	{
		bool hasHit = Physics.Raycast(position,
			Vector3.down,
			out hit,
			10f,
			LayerManager.GrassLayerMask);
		return hasHit;
	}

	private void ColorGrass(RaycastHit hitInfo)
	{
		var wateringSpeed = 6f;
		
		foreach (var grass in _grassComps)
		{
			//grass.ApplyPaint(_paintColor, hitInfo.point, _currentRadius - _dampingRadius, _currentRadius, wateringSpeed);
			grass.ApplyPaintInSight(_paintColor,
				hitInfo.point,
				_player.transform.position,
				_currentRadius,
				_currentRadius - _dampingRadius,
				30,
				wateringSpeed);
			
			grass.UpdateGrass();
		}
		
		OnDraw?.Invoke();
	}
}