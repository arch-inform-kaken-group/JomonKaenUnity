//using UnityEngine;
//using UnityEditor;

//namespace Microsoft.MixedReality.Toolkit.SampleGazeData
//{
//    public class HeatmapMaterialSetup : MonoBehaviour
//    {
//        [MenuItem("MRTK/Setup Heatmap Material")]

//        private void Start()
//        {
//            CreateHeatmapMaterial();
//        }

//        static void CreateHeatmapMaterial()
//        {
//            // Create heatmap color gradient texture
//            Texture2D heatmapTexture = new Texture2D(256, 1, TextureFormat.RGBA32, false);
//            for (int i = 0; i < heatmapTexture.width; i++)
//            {
//                float t = i / (float)(heatmapTexture.width - 1);

//                // Define a blue-to-red heat gradient
//                Color color;
//                if (t < 0.33f)
//                {
//                    // Blue to cyan
//                    float subT = t / 0.33f;
//                    color = Color.Lerp(new Color(0, 0, 1, 0.5f), new Color(0, 1, 1, 0.6f), subT);
//                }
//                else if (t < 0.66f)
//                {
//                    // Cyan to yellow
//                    float subT = (t - 0.33f) / 0.33f;
//                    color = Color.Lerp(new Color(0, 1, 1, 0.6f), new Color(1, 1, 0, 0.8f), subT);
//                }
//                else
//                {
//                    // Yellow to red
//                    float subT = (t - 0.66f) / 0.34f;
//                    color = Color.Lerp(new Color(1, 1, 0, 0.8f), new Color(1, 0, 0, 1.0f), subT);
//                }

//                heatmapTexture.SetPixel(i, 0, color);
//            }
//            heatmapTexture.Apply();

//            // Save texture to asset
//            AssetDatabase.CreateAsset(heatmapTexture, "Assets/MRTK/HeatmapLookupTable.asset");

//            // Create overlay material with the shader
//            Shader overlayShader = Shader.Find("Custom/HeatmapOverlay");
//            if (overlayShader == null)
//            {
//                Debug.LogError("Could not find Custom/HeatmapOverlay shader. Make sure it's compiled and included in the project.");
//                return;
//            }

//            Material overlayMaterial = new Material(overlayShader);
//            overlayMaterial.SetTexture("_MainTex", null); // Will be replaced at runtime
//            overlayMaterial.SetFloat("_Alpha", 0.7f);

//            // Save material to asset
//            AssetDatabase.CreateAsset(overlayMaterial, "Assets/MRTK/HeatmapOverlayMaterial.asset");

//            Debug.Log("Created heatmap material and lookup texture in Assets/MRTK folder");
//        }
//    }
//}