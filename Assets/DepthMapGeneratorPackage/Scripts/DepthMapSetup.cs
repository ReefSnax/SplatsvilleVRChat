using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace martinreintges.DepthMap
{
    public class DepthMapSetup : MonoBehaviour
    {
        #region static
#if UNITY_EDITOR
        [MenuItem("Window/DepthMap/Create DepthCamera")]
        public static void CreateDepthCamera()
        {
            var mainContainer = Selection.activeObject as GameObject;
            var originalCam = mainContainer.GetComponent<Camera>();

            var depthRenderer = GetDepthCamera(mainContainer.transform);

            if (depthRenderer == null)
            {
                var depthCam = Instantiate(originalCam, mainContainer.transform);
                depthCam.transform.SetParent(mainContainer.transform, true);
                depthCam.transform.localPosition = Vector3.zero;
                depthCam.transform.localRotation = Quaternion.identity;
                depthCam.gameObject.name = "DepthMapRenderer";
                var components = depthCam.GetComponents<Component>();
                foreach(var component in components)
                {
                    if(!(component is Camera))
                    {
                        DestroyImmediate(component);
                    }
                }
                depthRenderer = depthCam.gameObject.AddComponent<DepthMapRenderer>();
                depthRenderer.DepthCamera = depthCam;
                depthRenderer.RenderCamera = originalCam;
            }
            depthRenderer.SetupCamera();
        }

        [MenuItem("Window/DepthMap/Create DepthCamera", true)]
        public static bool CreateDepthCameraValidation()
        {
            var go = Selection.activeObject as GameObject;
            return go != null && go.GetComponent<Camera>() != null;
        }

        private static DepthMapRenderer GetDepthCamera(Transform main)
        {
            return main.GetComponentInChildren<DepthMapRenderer>();
        }



        [MenuItem("Window/DepthMap/Create DepthEffect Plane")]
        public static void CreateDepthBackground()
        {
            var mainContainer = Selection.activeObject as GameObject;
            var backgroundCam = mainContainer.GetComponent<Camera>();
            var setup = mainContainer.GetComponent<DepthMapSetup>();
            if (setup == null)
            {
                setup = mainContainer.AddComponent<DepthMapSetup>();
            }
            setup.FlatCamera = backgroundCam;
            setup.CreateEffectPlane();
        }

        [MenuItem("Window/DepthMap/Create DepthEffect Plane", true)]
        public static bool CreateDepthBackgroundValidation()
        {
            var go = Selection.activeObject as GameObject;
            return go != null && go.GetComponent<Camera>() != null;
        }
#endif
        #endregion

        // Refs
        public Camera FlatCamera;
        public Transform BackgroundQuad;

        public void Start()
        {
            if(FlatCamera == null)
            {
                return;
            }

            FlatCamera.depthTextureMode = DepthTextureMode.Depth;
        }

        public void CreateEffectPlane()
        {
            if(FlatCamera == null)
            {
                return;
            }
            var zPosition = FlatCamera.nearClipPlane + 0.001f;
            BackgroundQuad = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
            BackgroundQuad.SetParent(FlatCamera.transform);
            BackgroundQuad.localPosition = new Vector3(0, 0, zPosition);
            BackgroundQuad.localRotation = Quaternion.identity;

            var zero = FlatCamera.ViewportToWorldPoint(new Vector3(0, 0, zPosition));
            var top = FlatCamera.ViewportToWorldPoint(new Vector3(0, 1, zPosition));
            var right = FlatCamera.ViewportToWorldPoint(new Vector3(1, 0, zPosition));
            BackgroundQuad.localScale = new Vector3(Vector3.Distance(right, zero), Vector3.Distance(top, zero), 1);
            BackgroundQuad.gameObject.name = "DepthMapEffectPlane";

            DestroyImmediate(BackgroundQuad.GetComponent<Collider>());
            var renderer = BackgroundQuad.GetComponent<MeshRenderer>();
            renderer.receiveShadows = false;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}