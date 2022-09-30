using UnityEngine;
using UnityEditor;

namespace martinreintges.DepthMap
{
    [CustomEditor(typeof(DepthMapRenderer))]
    public class DepthMapRendererEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var renderer = (Selection.activeObject as GameObject).GetComponent<DepthMapRenderer>();
            DrawRenderTextureGUI(renderer);
            DrawDepthMapGUI(renderer);
            DrawWarnings(renderer);
        }

        private void DrawRenderTextureGUI(DepthMapRenderer depthMapRenderer)
        {
            if (depthMapRenderer.RenderCamera != null && depthMapRenderer.RenderCamera.targetTexture != null)
            {
                if (GUILayout.Button("Save RenderTexture as PNG image"))
                {
                    DepthMapRenderer.SaveToFile(depthMapRenderer.RenderCamera.targetTexture);
                }
            }
        }

        private void DrawDepthMapGUI(DepthMapRenderer depthMapRenderer)
        {
            if (depthMapRenderer.DepthMap == null)
            {
                if (!depthMapRenderer.CreateTextureOnStart && GUILayout.Button("Create DepthMap asset"))
                {
                    var depthMap = DepthMapRenderer.CreateDepthMapAsset();
                    depthMapRenderer.DepthMap = depthMap;
                    if (depthMapRenderer.DepthCamera != null)
                    {
                        depthMapRenderer.DepthCamera.targetTexture = depthMap;
                    }
                    else
                    {
                        Debug.LogWarning("Selected DepthMapRenderer has no camera assigned to it.");
                    }
                }
            }
            else
            {
                if (GUILayout.Button("Save DepthMap to PNG file"))
                {
                    DepthMapRenderer.SaveToFile(depthMapRenderer.DepthMap);
                }
            }
        }

        private void DrawWarnings(DepthMapRenderer depthMapRenderer)
        {
            var warnings = "";
            if (depthMapRenderer.DepthCamera == null)
            {
                warnings += "Selected DepthMapRenderer has no camera assigned to it.\n";
            }
            else
            {
                if (depthMapRenderer.DepthCamera.nearClipPlane < 0.1f)
                {
                    warnings += "The cameras near-clipping-plane value is very small.\n";
                }
                if (depthMapRenderer.DepthCamera.farClipPlane > 200f)
                {
                    warnings += "The cameras far-clipping-plane value is very big.\n";
                }
            }
            if (depthMapRenderer.RenderCamera == null)
            {
                warnings += "No RenderCamera set.\n";
            }

            if (!string.IsNullOrEmpty(warnings))
            {
                EditorGUILayout.HelpBox(warnings, MessageType.Warning);
            }
        }
    }
}