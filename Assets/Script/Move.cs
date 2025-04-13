using UnityEngine;

public class Move : MonoBehaviour
{
    public float speed = 5f;

    void Update()
    {
        // WASD for horizontal (X, Z) movement.
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        Vector3 movement = new Vector3(moveX, 0f, moveZ);

        // X key for upward, Z key for downward.
        if (Input.GetKey(KeyCode.X))
            movement.y = 1f;
        else if (Input.GetKey(KeyCode.Z))
            movement.y = -1f;

        transform.Translate(movement * speed * Time.deltaTime, Space.World);
    }
}
