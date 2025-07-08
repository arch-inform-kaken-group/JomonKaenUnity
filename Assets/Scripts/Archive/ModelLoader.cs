//using GLTFast;
//using GLTFast.Materials;
//using GLTFast.Schema;
//using UnityEngine;
//using UnityEngine.AddressableAssets; // Required for Addressables
//using UnityEngine.ResourceManagement.AsyncOperations; // Required for AsyncOperationHandle
//using System.Threading.Tasks;
//using System.Collections.Generic; // For managing loaded handles
//using System; // For Action

//// Required for MRTK's shader properties (make sure MRTK is installed)
//#if MRTK_APPLY_DEFAULT_SHADER_ON_GLTF_IMPORT
//using Microsoft.MixedReality.Toolkit.Utilities.Editor;
//using Microsoft.MixedReality.Toolkit.Utilities.Shaders;
//#endif

//// Custom Material Generator for glTFast to use MRTK Standard Shader
//public class MrtkGlbMaterialGenerator : IMaterialGenerator
//{
//    private Shader mrtkStandardShader;
//    private Shader fallbackStandardShader;

//    public MrtkGlbMaterialGenerator()
//    {
//        // Attempt to find the MRTK Standard shader
//        mrtkStandardShader = Shader.Find("Mixed Reality Toolkit/Standard");
//        if (mrtkStandardShader == null)
//        {
//            Debug.LogWarning("MRTK Standard shader not found. Falling back to Unity's Standard shader for glTFast materials.");
//            fallbackStandardShader = Shader.Find("Standard"); // Fallback
//            if (fallbackStandardShader == null)
//            {
//                Debug.LogError("Unity Standard shader not found either. Materials will be unlit pink!");
//            }
//        }
//    }

//    public virtual UnityEngine.Material GenerateMaterial(
//    GLTFast.Schema.MaterialBase gltfMaterial,
//        IGltfReadable gltf,
//        bool transparent,
//        IMaterialGenerator originalGenerator
//        )
//    {
//        Shader targetShader = mrtkStandardShader;
//        if (targetShader == null)
//        {
//            targetShader = fallbackStandardShader;
//        }

//        if (targetShader == null)
//        {
//            // If no shader found, return a default pink material
//            return new UnityEngine.Material(Shader.Find("Hidden/InternalErrorShader"));
//        }

//        var material = new UnityEngine.Material(targetShader);

//        // --- Map GLTF PBR properties to MRTK Standard Shader properties ---
//        // Base Color / Albedo
//        if (gltfMaterial.pbrMetallicRoughness != null)
//        {
//            material.SetColor("_Color", gltfMaterial.pbrMetallicRoughness.BaseColor.gamma); // Convert to gamma space

//            if (gltfMaterial.pbrMetallicRoughness.BaseColorTexture.index >= 0)
//            {
//                var baseColorTexture = gltf.GetTexture(gltfMaterial.pbrMetallicRoughness.BaseColorTexture.index);
//                if (baseColorTexture != null)
//                {
//                    material.SetTexture("_MainTex", baseColorTexture);
//                    material.EnableKeyword("_ALBEDO_MAP"); // Enable Albedo map feature
//                }
//            }

//            // Metallic & Roughness (assuming combined texture or separate properties)
//            // MRTK Standard shader typically uses _Metallic and _Smoothness
//            material.SetFloat("_Metallic", gltfMaterial.pbrMetallicRoughness.MetallicFactor);
//            // Roughness in glTF is inverse of smoothness in Unity's Standard/MRTK Standard
//            material.SetFloat("_Smoothness", 1.0f - gltfMaterial.pbrMetallicRoughness.RoughnessFactor);
//            material.EnableKeyword("_METALLIC_MAP"); // Enable metallic/smoothness map features if using
//        }

//        // Normal Map
//        if (gltfMaterial.NormalTexture != null && gltfMaterial.NormalTexture.index >= 0)
//        {
//            var normalTexture = gltf.GetTexture(gltfMaterial.NormalTexture.index);
//            if (normalTexture != null)
//            {
//                material.SetTexture("_NormalMap", normalTexture);
//                material.EnableKeyword("_NORMAL_MAP"); // Enable normal map feature
//            }
//        }

//        // Occlusion Map
//        if (gltfMaterial.OcclusionTexture != null && gltfMaterial.OcclusionTexture.index >= 0)
//        {
//            var occlusionTexture = gltf.GetTexture(gltfMaterial.OcclusionTexture.index);
//            if (occlusionTexture != null)
//            {
//                material.SetTexture("_OcclusionMap", occlusionTexture);
//                material.EnableKeyword("_OCCLUSION_MAP"); // Enable occlusion map feature
//            }
//        }

//        // Emissive Map & Color
//        if (gltfMaterial.EmissiveTexture != null && gltfMaterial.EmissiveTexture.index >= 0)
//        {
//            var emissiveTexture = gltf.GetTexture(gltfMaterial.EmissiveTexture.index);
//            if (emissiveTexture != null)
//            {
//                material.SetTexture("_EmissiveMap", emissiveTexture);
//                material.EnableKeyword("_EMISSIVE_MAP"); // Enable emissive map feature
//            }
//        }
//        material.SetColor("_EmissiveColor", gltfMaterial.EmissiveFactor.gamma);

//        // Transparency Handling
//        if (transparent)
//        {
//            // For MRTK Standard, typically use "Fade" or "Transparent" mode
//            // Mode 0: Opaque, 1: Cutout, 2: Fade, 3: Transparent, 4: Additive, 5: Custom
//            material.SetInt("_Mode", 2); // Set to Fade
//            material.SetOverrideTag("RenderMode", "Fade");
//            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
//            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
//            material.SetInt("_ZWrite", 0); // Disable ZWrite for transparent materials
//            material.DisableKeyword("_ALPHATEST_ON");
//            material.EnableKeyword("_ALPHABLEND_ON");
//            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
//            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
//        }
//        else
//        {
//            material.SetInt("_Mode", 0); // Opaque
//            material.SetOverrideTag("RenderMode", "Opaque");
//            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
//            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
//            material.SetInt("_ZWrite", 1); // Enable ZWrite for opaque materials
//            material.DisableKeyword("_ALPHATEST_ON");
//            material.DisableKeyword("_ALPHABLEND_ON");
//            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
//            material.renderQueue = -1; // Use default
//        }

//        // Enable Double-Sided Rendering if glTF specifies it
//        if (gltfMaterial.DoubleSided)
//        {
//            material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off); // Off means double-sided
//        }
//        else
//        {
//            material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Back); // Backface culling (default)
//        }

//        // --- MRTK specific shader features ---
//        // Enable features if your GLB model uses them and you want MRTK shader to reflect them
//        // You might need to adjust these based on your specific MRTK version and shader setup.
//#if MRTK_APPLY_DEFAULT_SHADER_ON_GLTF_IMPORT
//        // Example: If your glTF has vertex colors, enable this feature
//        // This is a simplified example, you'd check if vertex colors are present in mesh data
//        // material.EnableKeyword("_VERTEX_COLORS"); 
        
//        // This section might need more sophisticated checks based on what glTF provides
//        // For instance, if glTF uses a metallic-roughness texture, set the appropriate feature
//        // Default MRTK Standard applies PBR via _Metallic and _Smoothness directly.
//        // If glTF specifies a dedicated MetallicRoughness texture, you would map it here.
//        // For simplicity, we've mapped factors above.
        
//        // You can add more MRTK-specific feature toggles here, e.g., if you have specific flags
//        // in your GLB material's extensions.
//#endif

//        return material;
//    }
//}


//public class GlbLoader : MonoBehaviour
//{
//    // The "Address" of your GLB prefab in Addressables.
//    // This should match what you set in the Addressables Groups window.
//    [Tooltip("The Addressable key for your GLB model (e.g., 'MyModel.glb' or a custom address).")]
//    public string glbAddress = "YourModel.glb";

//    [Tooltip("Optional: Parent transform for the loaded GLB model.")]
//    public Transform parentForLoadedModel;

//    [Tooltip("UI Text to display loading status.")]
//    public TextMesh loadingStatusText; // Using TextMesh for simple in-world text feedback

//    private AsyncOperationHandle<GameObject> loadHandle;
//    private List<AsyncOperationHandle> activeHandles = new List<AsyncOperationHandle>();

//    void Start()
//    {
//        if (parentForLoadedModel == null)
//        {
//            parentForLoadedModel = this.transform;
//        }

//        LoadGlbFromAddressables(glbAddress);
//    }

//    void OnDestroy()
//    {
//        // Release all active Addressables handles when this GameObject is destroyed
//        foreach (var handle in activeHandles)
//        {
//            if (handle.IsValid())
//            {
//                Addressables.Release(handle);
//            }
//        }
//        activeHandles.Clear();
//    }

//    public async void LoadGlbFromAddressables(string address)
//    {
//        Debug.Log($"Attempting to load GLB from Addressables: {address}");
//        UpdateLoadingStatus("Loading GLB...", 0f);

//        try
//        {
//            // Load the Addressable asset (which should be a GLB prefab)
//            loadHandle = Addressables.LoadAssetAsync<GameObject>(address);
//            activeHandles.Add(loadHandle); // Keep track of the handle for releasing

//            while (!loadHandle.IsDone)
//            {
//                UpdateLoadingStatus("Loading GLB...", loadHandle.PercentComplete);
//                await Task.Yield(); // Wait for next frame
//            }

//            if (loadHandle.Status == AsyncOperationStatus.Succeeded)
//            {
//                Debug.Log($"Addressable GLB asset loaded successfully: {address}");
//                UpdateLoadingStatus("Instantiating Model...", 0f);

//                // Get the loaded GameObject (the root of your GLB prefab)
//                GameObject glbPrefab = loadHandle.Result;

//                // Create a new GltfImport instance
//                var gltf = new GltfImport();

//                // Get the path to the loaded asset. For Addressables, this is slightly different
//                // You might need to directly provide the GameObject/Prefab for glTFast to parse
//                // If glTFast cannot parse the GameObject directly, you might need to load it as a TextAsset (for .gltf)
//                // or a byte array (for .glb) from Addressables.
//                // However, glTFast's Instantiate can work directly with a GameObject/Prefab
//                // if it was imported by glTFast initially (even with editor import off, its internal structure might be there).

//                // For glTFast to process a prefab, it generally needs to have loaded it itself,
//                // or you feed it the raw byte data of the GLB.
//                // The simplest way to load GLB via Addressables AND glTFast's material generation
//                // is to load the raw bytes/TextAsset of the GLB and then pass that to glTFast.Load().

//                // --- Loading raw GLB bytes via Addressables ---
//                // Assuming your Addressable is the .glb file itself, not a prefab generated from it.
//                // If you marked the .glb file itself as Addressable:
//                AsyncOperationHandle<TextAsset> glbTextAssetHandle = Addressables.LoadAssetAsync<TextAsset>(address);
//                activeHandles.Add(glbTextAssetHandle);
//                await glbTextAssetHandle.Task;

//                if (glbTextAssetHandle.Status == AsyncOperationStatus.Succeeded)
//                {
//                    byte[] glbBytes = glbTextAssetHandle.Result.bytes; // Get the raw bytes of the GLB file

//                    // Load the GLB bytes with glTFast
//                    // This will then trigger our custom material generator
//                    bool gltfLoadSuccess = await gltf.LoadGltfBinary(glbBytes);

//                    if (gltfLoadSuccess)
//                    {
//                        Debug.Log("GLB bytes parsed successfully by glTFast.");
//                        // Instantiate the loaded model using the custom material generator
//                        // We use the lambda to provide our custom IMaterialGenerator
//                        gltf.Instantiate(parentForLoadedModel, gltfImport => new MrtkGlbMaterialGenerator());
//                        UpdateLoadingStatus("Model loaded!", 1f);
//                    }
//                    else
//                    {
//                        Debug.LogError($"glTFast failed to parse GLB bytes for {address}.");
//                        UpdateLoadingStatus("Error parsing model!", 0f);
//                    }

//                    Addressables.Release(glbTextAssetHandle); // Release the TextAsset handle
//                    activeHandles.Remove(glbTextAssetHandle);
//                }
//                else
//                {
//                    Debug.LogError($"Failed to load GLB raw bytes (TextAsset) from Addressables: {address}. Error: {glbTextAssetHandle.OperationException?.Message}");
//                    UpdateLoadingStatus("Error loading bytes!", 0f);
//                }
//            }
//            else
//            {
//                Debug.LogError($"Failed to load Addressable GLB asset: {address}. Error: {loadHandle.OperationException?.Message}");
//                UpdateLoadingStatus("Error loading asset!", 0f);
//            }
//        }
//        catch (Exception e)
//        {
//            Debug.LogError($"An unexpected error occurred during GLB loading: {e.Message}");
//            UpdateLoadingStatus("An error occurred!", 0f);
//        }
//    }

//    void UpdateLoadingStatus(string message, float progress)
//    {
//        if (loadingStatusText != null)
//        {
//            loadingStatusText.text = $"{message} ({Mathf.RoundToInt(progress * 100)}%)";
//        }
//        Debug.Log($"{message} ({Mathf.RoundToInt(progress * 100)}%)");
//    }
//}