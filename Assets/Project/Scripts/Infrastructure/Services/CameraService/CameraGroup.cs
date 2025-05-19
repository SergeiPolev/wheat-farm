using Unity.Cinemachine;
using UnityEngine;

public enum BoundsBorder
{
    None,
    Top,
    Bottom,
    Left,
    Right,
}

public class CameraGroup : MonoBehaviour
{
    [SerializeField] private CinemachineCamera _virtualCamera;
    [SerializeField] private CinemachineSplineDolly _splineDolly;

    public CinemachineCamera VirtualCamera => _virtualCamera;
    public CinemachineSplineDolly SplineDolly => _splineDolly;

    private Vector2 _minPos;
    private Vector2 _maxPos;
    private Camera _camera;

    public bool IsInBounds(Vector3 position, out BoundsBorder bounds)
    {
        if (position.x < _minPos.x)
        {
            bounds = BoundsBorder.Left;
            return false;
        }
        
        if (position.x > _maxPos.x)
        {
            bounds = BoundsBorder.Right;
            return false;
        }
        
        if (position.y < _minPos.y)
        {
            bounds = BoundsBorder.Bottom;
            return false;
        }
        
        if (position.y > _maxPos.y)
        {
            bounds = BoundsBorder.Top;
            return false;
        }
        
        bounds = BoundsBorder.Top;
        
        return true;
    }
    
    private void OnEnable()
    {
        _camera = Camera.main;
    }

    private void Update()
    {
        _minPos = _camera.ViewportToWorldPoint(Vector3.zero);
        _maxPos = _camera.ViewportToWorldPoint(Vector3.one);
    }
}