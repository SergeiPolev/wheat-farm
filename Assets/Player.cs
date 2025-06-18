using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField] private Rigidbody RB;
    [SerializeField] private float _speed = 2f;
    
    private static readonly int InteractionPosition = Shader.PropertyToID("_Interaction_Position");

    public void Move(Vector3 direction)
    {
        RB.linearVelocity = (direction) * _speed;
        Shader.SetGlobalVector(InteractionPosition, transform.position);
    }
}