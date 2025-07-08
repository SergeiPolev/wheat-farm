using UnityEngine;

public class CropPainter : MonoBehaviour
{
    public Color Color;
    public CropRenderer CropRenderer;

    private int _currentCrop;

    private static float Radius => 2f;

    void Update()
    {
        if (Input.GetMouseButton(0))
        {
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            Bounds bounds = CropRenderer.GetBounds;
            if (bounds.IntersectRay(ray, out float distance))
            {
                var cropPos = CropRenderer.transform.position;
                var maxPoint = cropPos + new Vector3(
                    CropRenderer.FieldData.FieldSize.x * CropRenderer.FieldData.Margin.x,
                    0,
                    CropRenderer.FieldData.FieldSize.y * CropRenderer.FieldData.Margin.z
                    );
                var point = ray.GetPoint(distance);

                var inverseLerpVec = new Vector2(
                    Mathf.InverseLerp(cropPos.z, maxPoint.z, point.z),
                    Mathf.InverseLerp(cropPos.x, maxPoint.x, point.x)
                    );

                var posInArray = new Vector2Int(
                    Mathf.FloorToInt(CropRenderer.FieldData.FieldSize.x * inverseLerpVec.x),
                    Mathf.FloorToInt(CropRenderer.FieldData.FieldSize.y * inverseLerpVec.y));

                var convertedRadius = Radius / CropRenderer.FieldData.Margin.x;
                var sqrRadius = convertedRadius * convertedRadius;
                for (var y = -convertedRadius; y <= convertedRadius; y++)
                {
                    for (var x = -convertedRadius; x <= convertedRadius; x++)
                    {
                        var px = Mathf.FloorToInt(posInArray.x + x);
                        var py = Mathf.FloorToInt(posInArray.y + y);
                        if (px >= 0 
                            && py >= 0 
                            && px < CropRenderer.FieldData.FieldSize.x 
                            && py < CropRenderer.FieldData.FieldSize.y 
                            && x * x + y * y <= sqrRadius)
                        {
                            if (_currentCrop == 5)
                            {
                                CutCrops(new Vector2Int(px, py));
                            }
                            else
                            {
                                PaintCrops(new Vector2Int(px, py));
                            }
                        }
                    }
                }
                
                CropRenderer.FieldData.OnChanges();
            }
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            _currentCrop = 1;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            _currentCrop = 2;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            _currentCrop = 3;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            _currentCrop = 4;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            _currentCrop = 5;
        }
    }

    private void CutCrops(Vector2Int posInArray)
    {
        int linearIndex = posInArray.y * CropRenderer.FieldData.FieldSize.y + posInArray.x;
        var props = CropRenderer.FieldData.GetMeshProperties[linearIndex];

        props.color = Color.white;
        props.cropState.x = 0;
        props.cropState.y = 0;

        CropRenderer.FieldData.GetMeshProperties[linearIndex] = props;
    }

    private void PaintCrops(Vector2Int posInArray)
    {
        int linearIndex = posInArray.y * CropRenderer.FieldData.FieldSize.y + posInArray.x;
        var props = CropRenderer.FieldData.GetMeshProperties[linearIndex];

        props.color = Color;
        props.cropState.x = _currentCrop;
        props.cropState.y = Mathf.Min(1, props.cropState.y + 1f * Time.deltaTime);

        CropRenderer.FieldData.GetMeshProperties[linearIndex] = props;
    }
}