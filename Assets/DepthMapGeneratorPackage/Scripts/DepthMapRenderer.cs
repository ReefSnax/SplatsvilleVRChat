using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace martinreintges.DepthMap
{
	public class DepthMapRenderer : MonoBehaviour
    {
#region static
#if UNITY_EDITOR
        [MenuItem("DepthMap", menuItem = "Assets/Create/DepthMap", priority = 304, validate = false)]
        public static RenderTexture CreateDepthMapAsset()
        {
            var depthMap = CreateDepthMap();
            var path = "Assets/" + depthMap.name + ".renderTexture";
            AssetDatabase.CreateAsset(depthMap, path);
            return depthMap;
        }
#endif

        public static RenderTexture CreateDepthMap()
        {
            var depthMap = new RenderTexture(Screen.width, Screen.height, 32);
            depthMap.format = RenderTextureFormat.ARGBFloat;
            depthMap.depth = 0;
            depthMap.name = "DepthMap_" + Random.Range(100000, 999999);
            return depthMap;
        }

#if UNITY_EDITOR
        public static void SaveToFile(RenderTexture renderTexture)
        {
            var prevTexture = RenderTexture.active;
            RenderTexture.active = renderTexture;
            Texture2D texture2d = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBAFloat, false);
            texture2d.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            RenderTexture.active = prevTexture;

            var path = AssetDatabase.GetAssetPath(renderTexture);
            if (string.IsNullOrEmpty(path))
            {
                path = System.IO.Path.Combine(Application.dataPath, "DepthMap" + renderTexture.GetHashCode() + ".png");
            }
            else
            {
                path = path.Replace("renderTexture", "png");
            }

            System.IO.File.WriteAllBytes(path, texture2d.EncodeToPNG());
            Debug.Log("Save RenderTexture as png to: " + path);
            AssetDatabase.ImportAsset(path);
            AssetDatabase.Refresh();
        }


        [MenuItem("Window/DepthMap/Save RenderTexture to file")]
        public static void SaveRTToFile()
        {
            RenderTexture rt = Selection.activeObject as RenderTexture;

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            RenderTexture.active = null;

            byte[] bytes;
            bytes = tex.EncodeToPNG();

            string path = AssetDatabase.GetAssetPath(rt) + ".png";
            path = path.Replace(".renderTexture", "");
            System.IO.File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path);
            Debug.Log("Saved to " + path);
        }

        [MenuItem("Window/DepthMap/Save RenderTexture to file", true)]
        public static bool SaveRTToFileValidation()
        {
            return Selection.activeObject is RenderTexture;
        }
#endif
#endregion

        // Refs
        public Camera RenderCamera;
        public Camera DepthCamera;
        public Material[] DepthMaterials;

        // Fields
        public bool CreateTextureOnStart = true;
        public bool RuntimeUpdate = true;
        public string DepthShaderName = "DepthMap/DepthShader";
        public DepthTextureMode DepthMode = DepthTextureMode.Depth;
        public RenderTexture DepthMap = null;

		// Unity
		void Start()
		{
			SetupCamera();
			if (CreateTextureOnStart)
			{
				CheckDepthMap();
            }
            UpdateDepthMapData();

            foreach (var mat in DepthMaterials)
            {
                mat.SetTexture("_DepthTex", DepthMap);
            }
        }

        // HeightMapRenderer
        public void SetupCamera()
        {
            if (DepthCamera == null)
                return;

            DepthCamera.targetTexture = DepthMap;
            DepthCamera.clearFlags = CameraClearFlags.Color;
            DepthCamera.backgroundColor = Color.black;
            DepthCamera.depthTextureMode = DepthMode;
            var shader = Shader.Find(DepthShaderName);
            DepthCamera.SetReplacementShader(shader, "");
            DepthCamera.enabled = RuntimeUpdate;
        }

        private void CheckDepthMap()
		{
			if (DepthMap == null)
			{
                DepthMap = CreateDepthMap();
                if (DepthCamera != null)
                {
                    DepthCamera.targetTexture = DepthMap;
                }
            }
        }

		private void UpdateDepthMapData()
		{
            if (DepthCamera == null)
            {
                return;
            }

            DepthCamera.Render();
		}
	}
}
