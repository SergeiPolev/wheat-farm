public static class LayerManager
{
    public static int DiggerLayer = 6;
    public static int ObstacleLayer = 7;
    public static int CollectiblesLayer = 9;
    public static int PlayerLayer = 10;
    public static int PlayerProjectileLayer = 11;
    public static int EnemyLayer = 12;
    public static int EnemyProjectileLayer = 13;
    public static int BlockVisualLayer = 14;
    public static int BlockLayer = 3;

    public static int ObstaclesLayerMask = 1 << ObstacleLayer;
    public static int CollectiblesLayerMask = 1 << CollectiblesLayer;
    public static int PlayerLayerMask = 1 << PlayerLayer;
    public static int EnemiesLayerMask = 1 << EnemyLayer;
    public static int AllEnemiesLayerMask = EnemiesLayerMask;
    public static int ProjectileLayerMask = 1 << PlayerProjectileLayer;
    public static int EnemyProjectileLayerMask = 1 << EnemyProjectileLayer;
    public static int DiggerLayerMask = 1 << DiggerLayer;
    public static int BlockLayerMask = 1 << BlockLayer;
    public static int AllObstaclesLayerMask = ObstaclesLayerMask;

    public static bool SameLayer(int originLayer, int checkLayer)
    {
        return originLayer == checkLayer;
    }
}