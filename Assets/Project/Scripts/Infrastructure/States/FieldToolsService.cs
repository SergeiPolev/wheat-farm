using Services;
using UnityEngine;

namespace Infrastructure
{
    public class FieldToolsService : IService, ITick
    {
        public Color Color = Color.yellow;

        private int _currentCrop;
        private Plane _plane;
        
        private GetCropPointsService _getCropPointsService;
        private GlobalBlackboard _globalBlackboard;
        
        private static float Radius => 2f;

        public void OnLevelEnter()
        {
            _getCropPointsService = AllServices.Container.Single<GetCropPointsService>();
            _globalBlackboard = AllServices.Container.Single<GlobalBlackboard>();
            _plane = new Plane(Vector3.up, Vector3.zero);
        }

        public void Tick()
        {
            if (Input.GetMouseButton(0))
            {
                var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (_plane.Raycast(ray, out float distance))
                {
                    var point = ray.GetPoint(distance);
                    var fields = _getCropPointsService.GetCropRenderersInRadius(point, Radius);
                    for (int i = 0; i < fields.Count; i++)
                    {
                        var fieldData = fields[i];
                        Bounds bounds = fieldData.GetBounds;
                        var cropPos = fieldData.transform.position;
                        var maxPoint = cropPos + new Vector3(
                            fieldData.FieldSize.x * fieldData.Margin.x,
                            0,
                            fieldData.FieldSize.y * fieldData.Margin.z
                        );

                        var inverseLerpVec = new Vector2(
                            Mathf.InverseLerp(cropPos.z, maxPoint.z, point.z),
                            Mathf.InverseLerp(cropPos.x, maxPoint.x, point.x)
                        );

                        var posInArray = new Vector2Int(
                            Mathf.FloorToInt(fieldData.FieldSize.x * inverseLerpVec.x),
                            Mathf.FloorToInt(fieldData.FieldSize.y * inverseLerpVec.y));

                        var convertedRadius = Radius / fieldData.Margin.x;
                        var sqrRadius = convertedRadius * convertedRadius;
                        for (var y = -convertedRadius; y <= convertedRadius; y++)
                        {
                            for (var x = -convertedRadius; x <= convertedRadius; x++)
                            {
                                var px = Mathf.FloorToInt(posInArray.x + x);
                                var py = Mathf.FloorToInt(posInArray.y + y);
                                if (px >= 0
                                    && py >= 0
                                    && px < fieldData.FieldSize.x
                                    && py < fieldData.FieldSize.y
                                    && x * x + y * y <= sqrRadius)
                                {
                                    _globalBlackboard.Player.ToolHandler.CurrentTool.UseAt(fieldData, new Vector2Int(px, py));
                                }
                            }
                        }
                        
                        fieldData.OnChanges();
                    }
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

        public void CutCrops(CropFieldData cropRenderer, Vector2Int posInArray)
        {
            int linearIndex = posInArray.y * cropRenderer.FieldSize.y + posInArray.x;
            var props = cropRenderer.GetMeshProperties[linearIndex];

            props.color = Color.white;
            props.cropState.x = 0;
            props.cropState.y = 0;

            cropRenderer.GetMeshProperties[linearIndex] = props;
        }

        public void PlantCrops(CropFieldData cropRenderer, Vector2Int posInArray)
        {
            int linearIndex = posInArray.y * cropRenderer.FieldSize.y + posInArray.x;
            var props = cropRenderer.GetMeshProperties[linearIndex];

            props.color = Color;
            props.cropState.x = _currentCrop;

            cropRenderer.GetMeshProperties[linearIndex] = props;
        }

        public void WaterCrops(CropFieldData cropRenderer, Vector2Int posInArray)
        {
            int linearIndex = posInArray.y * cropRenderer.FieldSize.y + posInArray.x;
            var props = cropRenderer.GetMeshProperties[linearIndex];

            if (props.cropState.x > 0)
            {
                props.cropState.y = Mathf.Min(1, props.cropState.y + 1f * Time.deltaTime);
                cropRenderer.GetMeshProperties[linearIndex] = props;
            }
        }
    }
}