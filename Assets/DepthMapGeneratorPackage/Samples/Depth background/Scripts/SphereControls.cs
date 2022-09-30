using UnityEngine;

namespace martinreintges.DepthMap
{
    public class SphereControls : MonoBehaviour
    {
        // Refs
        public Rigidbody Body;

        // Fields
        public float Speed;

        void Update()
        {
            Vector3 direction = Vector3.zero;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) direction -= Body.transform.right;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) direction += Body.transform.right;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) direction += Body.transform.forward;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) direction -= Body.transform.forward;
            direction *= Speed;
            direction.y = Body.velocity.y;
            Body.velocity = direction;
        }
    }
}