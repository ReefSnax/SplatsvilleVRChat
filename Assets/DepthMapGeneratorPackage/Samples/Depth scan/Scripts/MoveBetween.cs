using UnityEngine;

namespace martinreintges.DepthMap
{
    public class MoveBetween : MonoBehaviour
    {
        // Refs
        public Transform LookAt;
        public Transform MoveObject;
        public Material ScanMaterial;

        // Fields
        public float Speed;
        public float Movement = 2;

        void Update()
        {
            MoveObject.localPosition = Vector3.right * Movement * Mathf.Sin(Time.time);
            MoveObject.LookAt(LookAt);
            if (ScanMaterial != null)
            {
                ScanMaterial.SetFloat("_EffectDistance", (Time.time * Speed) % 2 - 0.5f);
            }
        }
    }
}
