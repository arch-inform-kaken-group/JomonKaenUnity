using UnityEngine;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using Microsoft.MixedReality.Toolkit.SampleGazeData;
using Microsoft.MixedReality.Toolkit;
using System.Linq;

public class EnhancedDataRecorder : MonoBehaviour
{
    [System.Serializable]
    public class GazeData
    {
        public double timestamp;
        public Vector3 headPosition;
        public Vector3 headForward;
        public Vector3 eyeOrigin;
        public Vector3 eyeDirection;
        public Vector3 hitPosition;
        public string targetName;
        public Vector3 localHitPosition;
    }

    [System.Serializable]
    public class TransformData
    {
        public double timestamp;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public Vector3 velocity;
        public Vector3 angularVelocity;
    }

    [System.Serializable]
    public class SessionData
    {
        public string selectedObjectName;
        public List<GazeData> gazeData = new List<GazeData>();
        public List<TransformData> transformData = new List<TransformData>();
    }
    private string selectedObjectName;

    public bool isRecording;
    public SessionData currentSession = new SessionData();
    private Transform lastTransform;
    private Vector3 lastPosition;
    private Quaternion lastRotation;

    private DrawOn3DTexture heatmapSource;
    public MeshFilter meshFilter;
    public float voxelSize = 0.005f;

    [Header("Rotation Keys")]
    [SerializeField] private KeyCode saveKey = KeyCode.BackQuote;

    [Header("Gaussian Settings")]
    [SerializeField] private float gaussianSigma = 0.005f;
    //[SerializeField] private float gaussianRadius = 0.015f;
    private List<Vector3> uniquePositions = new List<Vector3>();
    private Dictionary<Vector3, int> positionFrequency = new Dictionary<Vector3, int>();
    private int maxFrequency;

    private void Start()
    { 
        heatmapSource = GetComponent<DrawOn3DTexture>();
    }

    void Update()
    {
        if (!isRecording) return;

        var eyeTarget = EyeTrackingTarget.LookedAtEyeTarget;
        var gazedObject = eyeTarget != null ? eyeTarget.gameObject : null;

        if (KeyboardMouseObjectController.currentlySelected != null)
        //if (HL2Controls.currentlySelected != null)
        {
            RecordGazeData(gazedObject);
            RecordTransformData(KeyboardMouseObjectController.currentlySelected.transform);
            currentSession.selectedObjectName = KeyboardMouseObjectController.currentlySelected.gameObject.name;
            //RecordTransformData(HL2Controls.currentlySelected.transform);
            //currentSession.selectedObjectName = HL2Controls.currentlySelected.gameObject.name;
            selectedObjectName = currentSession.selectedObjectName;
        }

        if (Input.GetKeyDown(saveKey))
        {
            // Save session
            SaveSession("session.json");
            ExportToCSV("gaze_data.csv", "transform_data.csv");
            ExportAllFormats(KeyboardMouseObjectController.currentlySelected.hitTarget);
            //ExportAllFormats(HL2Controls.currentlySelected.gameObject);
        }
    }

    private void RecordGazeData(GameObject target)
    {
        var eyeProvider = CoreServices.InputSystem?.EyeGazeProvider;
        if (eyeProvider == null) return;

        var gaze = new GazeData
        {
            timestamp = Time.unscaledTimeAsDouble,
            headPosition = CameraCache.Main.transform.position,
            headForward = CameraCache.Main.transform.forward,
            eyeOrigin = eyeProvider.GazeOrigin,
            eyeDirection = eyeProvider.GazeDirection,
            hitPosition = eyeProvider.IsEyeTrackingEnabledAndValid ? eyeProvider.HitPosition : Vector3.zero,
            targetName = target != null ? target.name : "null"
        };

        if (target != null && target.name == currentSession.selectedObjectName)
        {
            // Convert to local space
            gaze.localHitPosition = target.transform.InverseTransformPoint(gaze.hitPosition);
        }
        else
        {
            gaze.localHitPosition = Vector3.zero;
        }

        currentSession.gazeData.Add(gaze);
    }

    private void RecordTransformData(Transform targetTransform)
    {
        if (targetTransform == null) return;

        // Calculate velocity
        Vector3 currentPosition = targetTransform.position;
        Vector3 velocity = (currentPosition - lastPosition) / Time.deltaTime;

        // Calculate angular velocity using Euler angles with proper wrapping
        Vector3 currentEuler = targetTransform.rotation.eulerAngles;
        Vector3 lastEuler = lastRotation.eulerAngles;

        Vector3 deltaEuler = new Vector3(
            Mathf.DeltaAngle(lastEuler.x, currentEuler.x),
            Mathf.DeltaAngle(lastEuler.y, currentEuler.y),
            Mathf.DeltaAngle(lastEuler.z, currentEuler.z)
        );

        Vector3 angularVelocity = deltaEuler / Time.deltaTime;

        var transformData = new TransformData
        {
            timestamp = Time.unscaledTimeAsDouble,
            position = currentPosition,
            rotation = targetTransform.rotation,
            scale = targetTransform.localScale,
            velocity = velocity,
            angularVelocity = angularVelocity
        };

        currentSession.transformData.Add(transformData);

        lastPosition = currentPosition;
        lastRotation = targetTransform.rotation;
    }

    public void SaveSession(string fileName)
    {
        string json = JsonUtility.ToJson(currentSession);

        var dir = Path.Combine(Application.persistentDataPath, selectedObjectName);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(Path.Combine(dir, fileName), json);
        Debug.Log("JSON AT:"+ Path.Combine(dir, fileName).ToString());
    }

    public void ExportToCSV(string gazeFileName, string transformFileName)
    {
        StringBuilder gaze_csv = new StringBuilder();
        StringBuilder transform_csv = new StringBuilder();

        // Gaze Data Header
        gaze_csv.AppendLine("Timestamp,HeadX,HeadY,HeadZ,HeadFwdX,HeadFwdY,HeadFwdZ,EyeOriginX,EyeOriginY,EyeOriginZ,EyeDirX,EyeDirY,EyeDirZ,HitX,HitY,HitZ,TargetName");

        foreach (var gaze in currentSession.gazeData)
        {
            gaze_csv.AppendLine($"{gaze.timestamp:F6}," +
                           $"{gaze.headPosition.x:F4},{gaze.headPosition.y:F4},{gaze.headPosition.z:F4}," +
                           $"{gaze.headForward.x:F4},{gaze.headForward.y:F4},{gaze.headForward.z:F4}," +
                           $"{gaze.eyeOrigin.x:F4},{gaze.eyeOrigin.y:F4},{gaze.eyeOrigin.z:F4}," +
                           $"{gaze.eyeDirection.x:F4},{gaze.eyeDirection.y:F4},{gaze.eyeDirection.z:F4}," +
                           $"{gaze.hitPosition.x:F4},{gaze.hitPosition.y:F4},{gaze.hitPosition.z:F4}," +
                           $"{gaze.targetName}");
        }

        // Transform Data Header
        transform_csv.AppendLine("\nTransformTimestamp,PosX,PosY,PosZ,RotX,RotY,RotZ,RotW,ScaleX,ScaleY,ScaleZ,VelX,VelY,VelZ,AngVelX,AngVelY,AngVelZ");

        foreach (var trans in currentSession.transformData)
        {
            transform_csv.AppendLine($"{trans.timestamp:F6}," +
                            $"{trans.position.x:F4},{trans.position.y:F4},{trans.position.z:F4}," +
                            $"{trans.rotation.x:F4},{trans.rotation.y:F4},{trans.rotation.z:F4},{trans.rotation.w:F4}," +
                            $"{trans.scale.x:F4},{trans.scale.y:F4},{trans.scale.z:F4}," +
                            $"{trans.velocity.x:F4},{trans.velocity.y:F4},{trans.velocity.z:F4}," +
                            $"{trans.angularVelocity.x:F4},{trans.angularVelocity.y:F4},{trans.angularVelocity.z:F4}");
        }

        var dir = Path.Combine(Application.persistentDataPath, selectedObjectName);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(Path.Combine(dir, gazeFileName), gaze_csv.ToString());
        Debug.Log("GAZE CSV AT:" + Path.Combine(dir, gazeFileName).ToString());

        File.WriteAllText(Path.Combine(dir, transformFileName), transform_csv.ToString());
        Debug.Log("TRANSFORM CSV AT:" + Path.Combine(dir, transformFileName).ToString());
    }


    public void ExportAllFormats(GameObject target)
    {
        //ExportHeatmapTexture();
        Export3DModel();
        ExportPointCloud(target);
        //ExportVoxelGrid();
    }

    private void ExportHeatmapTexture()
    {
        Texture2D heatmap = heatmapSource.MyDrawTexture;
        byte[] pngData = heatmap.EncodeToPNG();

        var dir = Path.Combine(Application.persistentDataPath, selectedObjectName);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllBytes(Path.Combine(dir, "heatmap.png"), pngData);
        Debug.Log("PNG AT:" + Path.Combine(dir, "heatmap.png").ToString());
    }

    private void Export3DModel()
    {
        Mesh mesh = meshFilter.sharedMesh;
        string objContent = MeshToString(mesh);

        var dir = Path.Combine(Application.persistentDataPath, selectedObjectName);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(Path.Combine(dir, "heatmap.obj"), objContent);
        Debug.Log("OBJ AT:" + Path.Combine(dir, "heatmap.obj").ToString());
    }

    private Vector3 GetAdjustedPosition(GazeData gaze)
    {
        // Use local position if available and valid
        if (gaze.targetName == currentSession.selectedObjectName &&
            gaze.localHitPosition != Vector3.zero)
        {
            return gaze.localHitPosition;
        }
        return gaze.hitPosition;
    }

    //private void ExportPointCloud()
    //{
    //    positionFrequency.Clear();
    //    maxFrequency = 0;

    //    // First pass: build frequency dictionary
    //    foreach (var gaze in currentSession.gazeData)
    //    {
    //        if (gaze.hitPosition == Vector3.zero) continue;

    //        Vector3 pos = GetAdjustedPosition(gaze);

    //        if (positionFrequency.ContainsKey(pos))
    //            positionFrequency[pos]++;
    //        else
    //            positionFrequency[pos] = 1;

    //        if (positionFrequency[pos] > maxFrequency)
    //            maxFrequency = positionFrequency[pos];
    //    }

    //    // Second pass: write data with intensity
    //    StringBuilder sb = new StringBuilder();
    //    sb.AppendLine("x,y,z,intensity");

    //    foreach (var gaze in currentSession.gazeData)
    //    {
    //        if (gaze.hitPosition == Vector3.zero) continue;

    //        Vector3 pos = GetAdjustedPosition(gaze);
    //        float intensity = CalculateHeatmapIntensity(pos);

    //        sb.AppendLine($"{pos.x:F4},{pos.y:F4},{pos.z:F4},{intensity:F4}");
    //    }

    //    File.WriteAllText(Path.Combine(Application.persistentDataPath, "pointcloud.csv"), sb.ToString());
    //    Debug.Log("POINTCLOUD AT:" + Path.Combine(Application.persistentDataPath, "pointcloud.csv").ToString());
    //}

    private void ExportPointCloud(GameObject target)
    {
        positionFrequency.Clear();
        uniquePositions.Clear();
        maxFrequency = 0;

        // Get the local bounds of the target GameObject
        Renderer targetRenderer = target.GetComponent<Renderer>();
        if (targetRenderer == null)
        {
            Debug.LogError("Target GameObject does not have a Renderer component.");
            return;
        }
        Bounds localBounds = targetRenderer.localBounds; // Local bounds of the target

        // First pass: build frequency dictionary and unique positions list
        foreach (var gaze in currentSession.gazeData)
        {
            if (gaze.hitPosition == Vector3.zero) continue;

            Vector3 pos = GetAdjustedPosition(gaze);

            // If the position is in world space, convert it to local space
            if (gaze.targetName != currentSession.selectedObjectName ||
                gaze.localHitPosition == Vector3.zero)
            {
                pos = target.transform.InverseTransformPoint(pos); // Convert world to local space
            }

            // Check if the gaze hit is within the local bounds of the target
            if (localBounds.Contains(pos) && gaze.targetName == target.name && gaze.targetName != "null")
            {
                if (positionFrequency.ContainsKey(pos))
                {
                    positionFrequency[pos]++;
                }
                else
                {
                    positionFrequency[pos] = 1;
                    uniquePositions.Add(pos);
                }

                if (positionFrequency[pos] > maxFrequency)
                {
                    maxFrequency = positionFrequency[pos];
                }
            }
        }

        // Second pass: write data with Gaussian-smoothed intensity
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("x,y,z,intensity");

        foreach (var pos in uniquePositions)
        {
            float intensity = CalculateHeatmapIntensity(pos);

            sb.AppendLine($"{pos.x:F4},{pos.y:F4},{pos.z:F4},{intensity:F4}");
        }

        var dir = Path.Combine(Application.persistentDataPath, selectedObjectName);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(Path.Combine(dir, "pointcloud.csv"), sb.ToString());
        Debug.Log("POINTCLOUD AT:" + Path.Combine(dir, "pointcloud.csv").ToString());
    }

    private class VoxelData
    {
        public Vector3 position;
        public float intensitySum;
        public int pointCount;
    }

    private void ExportVoxelGrid()
    {
        // Scale factor to preserve precision
        const float scaleFactor = 100f; // Adjust based on your scene scale
        float scaledVoxelSize = voxelSize * scaleFactor;

        Dictionary<Vector3Int, VoxelData> voxels = new Dictionary<Vector3Int, VoxelData>();
        float maxVoxelIntensity = 0;

        // First pass: collect all voxel data
        foreach (var gaze in currentSession.gazeData)
        {
            if (gaze.hitPosition == Vector3.zero) continue;

            Vector3 pos = GetAdjustedPosition(gaze);
            float intensity = CalculateHeatmapIntensity(pos);

            // Scale up positions before voxelization
            Vector3 scaledPos = pos * scaleFactor;

            // Calculate voxel indices using floor
            Vector3Int voxelIndex = new Vector3Int(
                Mathf.FloorToInt(scaledPos.x / scaledVoxelSize),
                Mathf.FloorToInt(scaledPos.y / scaledVoxelSize),
                Mathf.FloorToInt(scaledPos.z / scaledVoxelSize)
            );

            // Calculate voxel center in scaled space
            Vector3 voxelCenter = new Vector3(
                (voxelIndex.x + 0.5f) * scaledVoxelSize,
                (voxelIndex.y + 0.5f) * scaledVoxelSize,
                (voxelIndex.z + 0.5f) * scaledVoxelSize
            );

            if (!voxels.ContainsKey(voxelIndex))
            {
                voxels[voxelIndex] = new VoxelData
                {
                    position = voxelCenter / scaleFactor, // Store in original space
                    intensitySum = 0,
                    pointCount = 0
                };
            }

            // Apply Gaussian weighting based on distance from voxel center
            float distance = Vector3.Distance(scaledPos, voxelCenter);
            float weight = Mathf.Exp(-(distance * distance) / (2 * gaussianSigma * gaussianSigma));

            voxels[voxelIndex].intensitySum += intensity * weight;
            voxels[voxelIndex].pointCount++;

            if (voxels[voxelIndex].intensitySum > maxVoxelIntensity)
                maxVoxelIntensity = voxels[voxelIndex].intensitySum;
        }

        // Second pass: normalize and write data
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("x,y,z,intensity");

        foreach (var kvp in voxels)
        {
            float normalizedIntensity = maxVoxelIntensity > 0 ?
                kvp.Value.intensitySum / maxVoxelIntensity : 0;

            sb.AppendLine($"{kvp.Value.position.x:F4}," +
                          $"{kvp.Value.position.y:F4}," +
                          $"{kvp.Value.position.z:F4}," +
                          $"{normalizedIntensity:F4}");
        }

        var dir = Path.Combine(Application.persistentDataPath, selectedObjectName);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(Path.Combine(dir, "voxelgrid.csv"), sb.ToString());
    }

    private float CalculateHeatmapIntensity(Vector3 position)
    {
        //float intensity = 0f;

        //foreach (var uniquePos in uniquePositions)
        //{
        //    float distance = Vector3.Distance(position, uniquePos);

        //    if (distance <= gaussianRadius)
        //    {
        //        float weight = Mathf.Exp(-(distance * distance) / (2 * gaussianSigma * gaussianSigma));
        //        intensity += weight * positionFrequency[uniquePos];
        //    }
        //}

        //float maxPossibleIntensity = positionFrequency.Count > 0 ?
        //    positionFrequency.Values.Max() : 0;

        //return maxPossibleIntensity > 0 ? intensity / maxPossibleIntensity : 0;
        return positionFrequency.ContainsKey(position) ?
            (float)positionFrequency[position] / maxFrequency : 0f;
    }

    private static string MeshToString(Mesh mesh)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("# Exported Heatmap Object\n");

        foreach (Vector3 vertex in mesh.vertices)
        {
            sb.Append($"v {vertex.x:F4} {vertex.y:F4} {vertex.z:F4}\n");
        }

        foreach (Vector3 normal in mesh.normals)
        {
            sb.Append($"vn {normal.x:F4} {normal.y:F4} {normal.z:F4}\n");
        }

        foreach (Vector2 uv in mesh.uv)
        {
            sb.Append($"vt {uv.x:F4} {uv.y:F4}\n");
        }

        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            int[] triangles = mesh.GetTriangles(i);
            for (int j = 0; j < triangles.Length; j += 3)
            {
                sb.Append($"f {triangles[j] + 1}/{triangles[j] + 1}/{triangles[j] + 1} " +
                          $"{triangles[j + 1] + 1}/{triangles[j + 1] + 1}/{triangles[j + 1] + 1} " +
                          $"{triangles[j + 2] + 1}/{triangles[j + 2] + 1}/{triangles[j + 2] + 1}\n");
            }
        }

        return sb.ToString();
    }
}

//public class AdvancedHeatmapExporter : MonoBehaviour
//{
//    public EnhancedDataRecorder enhancedDataRecorder;
//    public DrawOn3DTexture heatmapSource;
//    public MeshFilter meshFilter;
//    public float voxelSize = 0.1f;

//    public void ExportAllFormats()
//    {
//        ExportHeatmapTexture();
//        Export3DModel();
//        ExportPointCloud();
//        ExportVoxelGrid();
//    }

//    private void ExportHeatmapTexture()
//    {
//        Texture2D heatmap = heatmapSource.MyDrawTexture;
//        byte[] pngData = heatmap.EncodeToPNG();
//        File.WriteAllBytes(Path.Combine(Application.persistentDataPath, "heatmap.png"), pngData);
//    }

//    private void Export3DModel()
//    {
//        Mesh mesh = meshFilter.sharedMesh;
//        string objContent = MeshToString(mesh);
//        File.WriteAllText(Path.Combine(Application.persistentDataPath, "heatmap.obj"), objContent);
//    }

//    private void ExportPointCloud()
//    {
//        StringBuilder sb = new StringBuilder();
//        sb.AppendLine("x,y,z,intensity");

//        foreach (var gaze in enhancedDataRecorder.currentSession.gazeData)
//        {
//            if (gaze.hitPosition != Vector3.zero)
//            {
//                float intensity = CalculateHeatmapIntensity(gaze.hitPosition);
//                sb.AppendLine($"{gaze.hitPosition.x:F4},{gaze.hitPosition.y:F4},{gaze.hitPosition.z:F4},{intensity:F4}");
//            }
//        }

//        File.WriteAllText(Path.Combine(Application.persistentDataPath, "pointcloud.csv"), sb.ToString());
//    }

//    private void ExportVoxelGrid()
//    {
//        Dictionary<Vector3Int, int> voxelCounts = new Dictionary<Vector3Int, int>();

//        foreach (var gaze in enhancedDataRecorder.currentSession.gazeData)
//        {
//            if (gaze.hitPosition == Vector3.zero) continue;

//            Vector3Int voxel = new Vector3Int(
//                Mathf.FloorToInt(gaze.hitPosition.x / voxelSize),
//                Mathf.FloorToInt(gaze.hitPosition.y / voxelSize),
//                Mathf.FloorToInt(gaze.hitPosition.z / voxelSize)
//            );

//            if (voxelCounts.ContainsKey(voxel)) voxelCounts[voxel]++;
//            else voxelCounts[voxel] = 1;
//        }

//        StringBuilder sb = new StringBuilder();
//        sb.AppendLine("x,y,z,count");
//        foreach (var entry in voxelCounts)
//        {
//            Vector3 worldPos = new Vector3(
//                entry.Key.x * voxelSize,
//                entry.Key.y * voxelSize,
//                entry.Key.z * voxelSize
//            );
//            sb.AppendLine($"{worldPos.x:F2},{worldPos.y:F2},{worldPos.z:F2},{entry.Value}");
//        }

//        File.WriteAllText(Path.Combine(Application.persistentDataPath, "voxelgrid.csv"), sb.ToString());
//    }

//    private float CalculateHeatmapIntensity(Vector3 position)
//    {
//        // Implement your intensity calculation based on gaze duration/frequency
//        return 1.0f;
//    }

//    private static string MeshToString(Mesh mesh)
//    {
//        StringBuilder sb = new StringBuilder();
//        sb.Append("# Exported Heatmap Object\n");

//        foreach (Vector3 vertex in mesh.vertices)
//        {
//            sb.Append($"v {vertex.x:F4} {vertex.y:F4} {vertex.z:F4}\n");
//        }

//        foreach (Vector3 normal in mesh.normals)
//        {
//            sb.Append($"vn {normal.x:F4} {normal.y:F4} {normal.z:F4}\n");
//        }

//        foreach (Vector2 uv in mesh.uv)
//        {
//            sb.Append($"vt {uv.x:F4} {uv.y:F4}\n");
//        }

//        for (int i = 0; i < mesh.subMeshCount; i++)
//        {
//            int[] triangles = mesh.GetTriangles(i);
//            for (int j = 0; j < triangles.Length; j += 3)
//            {
//                sb.Append($"f {triangles[j] + 1}/{triangles[j] + 1}/{triangles[j] + 1} " +
//                          $"{triangles[j + 1] + 1}/{triangles[j + 1] + 1}/{triangles[j + 1] + 1} " +
//                          $"{triangles[j + 2] + 1}/{triangles[j + 2] + 1}/{triangles[j + 2] + 1}\n");
//            }
//        }

//        return sb.ToString();
//    }
//}