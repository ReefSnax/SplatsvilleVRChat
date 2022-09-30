using UnityEngine;

namespace martinreintges.DepthMap
{
    public class PortalEffect : MonoBehaviour
    {
        // Enum
        public enum EffectType
        {
            BackAndForth,
            Continuously
        }

        // Refs
        public Material PortalMaterial;
        public RenderTexture Wolrd1;
        public RenderTexture Wolrd2;
        public Transform[] MoveObjects;

        // Fields
        public EffectType Effect;
        public float EffectSpeed = 1;

        void Start()
        {

        }
        
        void Update()
        {
            foreach (var trans in MoveObjects)
            {
                trans.localPosition = new Vector3(0, 10, -75) + Vector3.right * 5 * Mathf.Sin(Time.time);
            }

            if (Effect == EffectType.BackAndForth)
            {
                UpdateEffect1();
            }
            else
            {
                UpdateEffect2();
            }

            if(Input.GetKeyDown(KeyCode.Alpha1))
            {
                Effect = EffectType.BackAndForth;
            }
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                Effect = EffectType.Continuously;
            }
        }

        private void UpdateEffect1()
        {            
            PortalMaterial.SetFloat("_EffectDistance", Mathf.Sin(Time.time * EffectSpeed * 0.5f) * 0.5f + 0.6f);
        }

        private void UpdateEffect2()
        {
            var effectTime = Time.time * EffectSpeed * 0.25f % 2;
            if (effectTime < 1)
            {
                var timer = effectTime;
                PortalMaterial.SetTexture("_MainTex", Wolrd1);
                PortalMaterial.SetTexture("_SecondaryTex", Wolrd2);
                PortalMaterial.SetFloat("_EffectDistance", 1 - timer);
            }
            else
            {
                var timer = effectTime - 1;
                PortalMaterial.SetTexture("_MainTex", Wolrd2);
                PortalMaterial.SetTexture("_SecondaryTex", Wolrd1);
                PortalMaterial.SetFloat("_EffectDistance", 1 - timer);
            }
        }
    }
}