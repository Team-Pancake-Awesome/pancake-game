using UnityEngine;

public class GravityScript : MonoBehaviour
{
    public static Vector3 Gravity = new(0f, -9.81f, 0f);

    private Vector3 velocity = Vector3.zero;
    public Vector3 Velocity => velocity;

    void Update()
    {
        velocity += Gravity * Time.deltaTime;
        transform.position += velocity * Time.deltaTime;   
    }
}