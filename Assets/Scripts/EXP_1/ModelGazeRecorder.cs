using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.SampleGazeData;
using Microsoft.MixedReality.Toolkit.Utilities;
using Microsoft.MixedReality.Toolkit;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using System.IO.Compression;
using System;
using UnityEngine.Profiling;
using TMPro;

/// <summary>
/// Main class to record gaze, transform, and audio data during a user session.
/// Also supports exporting session data in various formats (JSON, CSV, OBJ, WAV).
/// </summary>
public class ModelGazeRecorder : MonoBehaviour
{
    #region Serializable Data Models

    /// <summary>
    /// Stores eye and head tracking data per frame.
    /// </summary>
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

    /// <summary>
    /// Stores transform changes for tracked object over time.
    /// </summary>
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

    /// <summary>
    /// Full session data including both gaze and transform recordings.
    /// </summary>
    [System.Serializable]
    public class SessionData
    {
        public string selectedObjectName;
        public List<GazeData> gazeData = new List<GazeData>();
        public List<TransformData> transformData = new List<TransformData>();
    }

    #endregion

    #region Public Variables & Settings

    [Header("Voxel Settings")]
    public float voxelSize = 0.005f;

    [Header("Rotation Keys")]
    [SerializeField] private KeyCode saveKey = KeyCode.BackQuote;

    //[Header("Gaussian Settings")]
    //[SerializeField] private float gaussianSigma = 0.005f;

    [Header("View Blocker")]
    [SerializeField] private GameObject viewBlocker;

    [Header("Audio Recording Settings")]
    public int audioSampleRate = 44100;

    [Header("Heatmap / Mesh Settings")]
    public MeshFilter meshFilter;

    #endregion

    #region Private Fields

    private string selectedObjectName;
    public bool isRecording;
    public SessionData currentSession = new SessionData();
    private Transform lastTransform;
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private DrawOn3DTexture heatmapSource;

    public string sessionPath;
    private string dir;
    private double startingTime;

    private List<Vector3> uniquePositions = new List<Vector3>();
    private Dictionary<Vector3, int> positionFrequency = new Dictionary<Vector3, int>();
    private int maxFrequency;

    // Audio recording fields
    private AudioClip recordedAudio;
    private bool isRecordingAudio = false;
    private string audioFilePath = string.Empty;
    private AudioSource audioSource;
    private float chunkStartTime = 0f;
    private int chunkIndex = 0;
    private List<string> savedFiles = new List<string>();

    #endregion

    #region Unity Lifecycle Methods

    private void Start()
    {
        heatmapSource = GetComponent<DrawOn3DTexture>();

        // Initialize audio source
        GameObject audioObj = new GameObject("AudioRecorder");
        audioSource = audioObj.AddComponent<AudioSource>();
        DontDestroyOnLoad(audioObj);
    }

    private void Update()
    {
        if (!isRecording) return;

        var eyeTarget = EyeTrackingTarget.LookedAtEyeTarget;
        var gazedObject = eyeTarget != null ? eyeTarget.gameObject : null;

        if (ModelController.currentModel != null)
        {
            RecordGazeData(gazedObject);
            //RecordTransformData(ModelController.currentModel.transform);
            currentSession.selectedObjectName = ModelController.currentModel.name;
            selectedObjectName = currentSession.selectedObjectName;
            dir = Path.Combine(Application.persistentDataPath, sessionPath, selectedObjectName);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }

        if (Input.GetKeyDown(saveKey))
        {
            SaveAllData();
        }

        // Auto-save chunks every ~60 seconds
        if (isRecordingAudio && Time.time - chunkStartTime >= 59f)
        {
            StopAudioRecording(); // Save current chunk
            StartCoroutine(RestartAudioRecordingAfterDelay());
        }
    }

    #endregion

    #region Control Methods

    /// <summary>
    /// Sets the recording state on or off.
    /// Enables/disables audio recording accordingly.
    /// </summary>
    public void SetIsRecording(bool val)
    {
        isRecording = val;
        viewBlocker.SetActive(!val);

        startingTime = Time.unscaledTimeAsDouble;

        if (val && !isRecordingAudio)
        {
            chunkIndex = 0; // Reset chunk index
            savedFiles.Clear(); // Clear file list
            StartAudioRecording();
        }
        else if (!val && isRecordingAudio)
        {
            StopAudioRecording();
            SaveFileList(dir); // Save file list
        }
    }

    #endregion

    #region Audio Recording Methods

    private IEnumerator RestartAudioRecordingAfterDelay()
    {
        yield return new WaitForSeconds(0.1f); // Small delay
        StartAudioRecording();
    }

    private void StartAudioRecording()
    {
        recordedAudio = Microphone.Start(null, false, 60, audioSampleRate); // loop = false
        isRecordingAudio = true;
        chunkStartTime = Time.time;
        Debug.Log("Started audio recording (60s chunk).");
    }

    private void StopAudioRecording()
    {
        if (!isRecordingAudio) return;
        Microphone.End(null);
        SaveAudioData(dir);
        isRecordingAudio = false;
        Debug.Log("Stopped audio recording and saved final chunk.");
    }

    private byte[] ConvertAudioClipToWAV(AudioClip clip)
    {
        if (clip == null || clip.samples == 0)
            return null;

        int channels = clip.channels;
        int sampleCount = clip.samples;
        int bitsPerSample = 16;
        int byteRate = clip.frequency * channels * (bitsPerSample / 8);
        int dataSize = sampleCount * channels * (bitsPerSample / 8);

        // Create WAV header
        byte[] header = new byte[44];
        Buffer.BlockCopy(Encoding.UTF8.GetBytes("RIFF"), 0, header, 0, 4);
        BitConverter.GetBytes((int)(dataSize + 36)).CopyTo(header, 4);
        Buffer.BlockCopy(Encoding.UTF8.GetBytes("WAVE"), 0, header, 8, 4);
        Buffer.BlockCopy(Encoding.UTF8.GetBytes("fmt "), 0, header, 12, 4);
        BitConverter.GetBytes((int)16).CopyTo(header, 16);
        BitConverter.GetBytes((short)1).CopyTo(header, 20);
        BitConverter.GetBytes((short)channels).CopyTo(header, 22);
        BitConverter.GetBytes(clip.frequency).CopyTo(header, 24);
        BitConverter.GetBytes(byteRate).CopyTo(header, 28);
        BitConverter.GetBytes((short)(channels * (bitsPerSample / 8))).CopyTo(header, 32);
        BitConverter.GetBytes((short)bitsPerSample).CopyTo(header, 34);
        Buffer.BlockCopy(Encoding.UTF8.GetBytes("data"), 0, header, 36, 4);
        BitConverter.GetBytes((int)dataSize).CopyTo(header, 40);

        // Extract samples and convert to short PCM
        float[] samples = new float[sampleCount * channels];
        clip.GetData(samples, 0);
        byte[] data = new byte[dataSize];

        for (int i = 0; i < samples.Length; i++)
        {
            short val = (short)Mathf.Clamp(samples[i] * short.MaxValue, short.MinValue, short.MaxValue);
            Buffer.BlockCopy(BitConverter.GetBytes(val), 0, data, i * 2, 2);
        }

        byte[] wavBytes = new byte[header.Length + data.Length];
        Buffer.BlockCopy(header, 0, wavBytes, 0, header.Length);
        Buffer.BlockCopy(data, 0, wavBytes, header.Length, data.Length);
        return wavBytes;
    }

    private void SaveAudioData(string folderPath)
    {
        if (recordedAudio == null)
        {
            Debug.LogWarning("No audio recorded to save.");
            return;
        }

        byte[] wavData = ConvertAudioClipToWAV(recordedAudio);
        string audioFileName = $"session_audio_{chunkIndex}.wav";
        string fullPath = Path.Combine(folderPath, audioFileName);
        File.WriteAllBytes(fullPath, wavData);
        Debug.Log($"Audio chunk saved: {fullPath}");
        savedFiles.Add(audioFileName);
        chunkIndex++;
    }

    private void SaveFileList(string folderPath)
    {
        // IN CMD run: ffmpeg -f concat -safe 0 -i filelist.txt -c copy output.wav
        StringBuilder sb = new StringBuilder();
        foreach (string file in savedFiles)
        {
            sb.AppendLine("file '" + file + "'");
        }

        string listFilePath = Path.Combine(folderPath, "filelist.txt");
        File.WriteAllText(listFilePath, sb.ToString());
        Debug.Log("File list saved to: " + listFilePath);
    }

    #endregion

    #region Gaze & Transform Recording

    private void RecordGazeData(GameObject target)
    {
        var eyeProvider = CoreServices.InputSystem?.EyeGazeProvider;
        if (eyeProvider == null) return;

        var gaze = new GazeData
        {
            timestamp = Time.unscaledTimeAsDouble - startingTime,
            headPosition = CameraCache.Main.transform.position,
            headForward = CameraCache.Main.transform.forward,
            eyeOrigin = eyeProvider.GazeOrigin,
            eyeDirection = eyeProvider.GazeDirection,
            hitPosition = eyeProvider.IsEyeTrackingEnabledAndValid ? eyeProvider.HitPosition : Vector3.zero,
            targetName = target != null ? target.name : "null"
        };

        if (target != null && target.name == currentSession.selectedObjectName)
        {
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

        Vector3 currentPosition = targetTransform.position;
        Vector3 velocity = (currentPosition - lastPosition) / Time.deltaTime;

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

    #endregion

    #region Session Saving & Exporting

    public void ResetAll()
    {
        if (isRecording)
        {
            SetIsRecording(false);
        }
        currentSession = new ModelGazeRecorder.SessionData();
        uniquePositions.Clear();
        positionFrequency.Clear();
        maxFrequency = 0;
        savedFiles.Clear(); // Clear any saved audio chunk file names
        StopAllCoroutines(); // Ensure any ongoing audio recording coroutines are stopped

        // Optionally, you might want to reset audio recording state explicitly
        isRecordingAudio = false;
        // Ensure Microphone.End is called if it was recording
        if (Microphone.IsRecording(null))
        {
            Microphone.End(null);
        }

        if (heatmapSource != null)
        {
            heatmapSource.ClearDrawing();
        }
    }

    public void SaveAllData()
    {
        SaveSession("session.json");
        ExportToCSV("gaze_data.csv", "transform_data.csv");
        ExportAllFormats(ModelController.currentModel);
    }

    public void SaveSession(string fileName)
    {
        string json = JsonUtility.ToJson(currentSession);
        string fullPath = Path.Combine(dir, fileName);
        File.WriteAllText(fullPath, json);
        Debug.Log("JSON AT:" + fullPath);
    }

    public void ExportToCSV(string gazeFileName, string transformFileName)
    {
        StringBuilder gaze_csv = new StringBuilder();
        StringBuilder transform_csv = new StringBuilder();

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

        //transform_csv.AppendLine("TransformTimestamp,PosX,PosY,PosZ,RotX,RotY,RotZ,RotW,ScaleX,ScaleY,ScaleZ,VelX,VelY,VelZ,AngVelX,AngVelY,AngVelZ");
        //foreach (var trans in currentSession.transformData)
        //{
        //    transform_csv.AppendLine($"{trans.timestamp:F6}," +
        //                    $"{trans.position.x:F4},{trans.position.y:F4},{trans.position.z:F4}," +
        //                    $"{trans.rotation.x:F4},{trans.rotation.y:F4},{trans.rotation.z:F4},{trans.rotation.w:F4}," +
        //                    $"{trans.scale.x:F4},{trans.scale.y:F4},{trans.scale.z:F4}," +
        //                    $"{trans.velocity.x:F4},{trans.velocity.y:F4},{trans.velocity.z:F4}," +
        //                    $"{trans.angularVelocity.x:F4},{trans.angularVelocity.y:F4},{trans.angularVelocity.z:F4}");
        //}

        File.WriteAllText(Path.Combine(dir, gazeFileName), gaze_csv.ToString());
        Debug.Log("GAZE CSV AT:" + Path.Combine(dir, gazeFileName).ToString());

        //File.WriteAllText(Path.Combine(dir, transformFileName), transform_csv.ToString());
        //Debug.Log("TRANSFORM CSV AT:" + Path.Combine(dir, transformFileName).ToString());
    }

    public void ExportAllFormats(GameObject target)
    {
        Export3DModel(target);
        ExportPointCloud(target);
    }

    private void Export3DModel(GameObject target)
    {
        Mesh mesh = meshFilter.sharedMesh;
        string objContent = MeshToString(mesh, target);
        File.WriteAllText(Path.Combine(dir, "model.obj"), objContent);
        Debug.Log("OBJ AT:" + Path.Combine(dir, "model.obj").ToString());
    }

    private Vector3 UnapplyUnityTransforms(Vector3 originalVector, Vector3 anglesInDegrees)
    {
        Quaternion xRotation = Quaternion.AngleAxis(anglesInDegrees.x, Vector3.right);
        Quaternion yRotation = Quaternion.AngleAxis(anglesInDegrees.y, Vector3.up);
        Quaternion zRotation = Quaternion.AngleAxis(anglesInDegrees.z, Vector3.forward);

        Vector3 rotatedVector = xRotation * originalVector;
        rotatedVector = yRotation * rotatedVector;
        rotatedVector = zRotation * rotatedVector;

        return new Vector3(-rotatedVector.x, rotatedVector.y, rotatedVector.z);
    }

    private void ExportPointCloud(GameObject target)
    {
        //positionFrequency.Clear();
        //uniquePositions.Clear();
        //maxFrequency = 0;

        Renderer targetRenderer = target.GetComponent<Renderer>();
        if (targetRenderer == null)
        {
            Debug.LogError("Target GameObject does not have a Renderer component.");
            return;
        }
        Bounds localBounds = targetRenderer.localBounds;

        List<Vector3> positions = new List<Vector3>();
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("x,y,z,timestamp");

        foreach (var gaze in currentSession.gazeData)
        {
            if (gaze.localHitPosition == Vector3.zero) continue;
            Vector3 pos = gaze.localHitPosition;

            if (localBounds.Contains(pos) && gaze.targetName == target.name && gaze.targetName != "null")
            {
                //if (positionFrequency.ContainsKey(pos))
                //{
                //    positionFrequency[pos]++;
                //}
                //else
                //{
                //    positionFrequency[pos] = 1;
                //    uniquePositions.Add(pos);
                //}
                //if (positionFrequency[pos] > maxFrequency)
                //{
                //    maxFrequency = positionFrequency[pos];
                //}
                pos = UnapplyUnityTransforms(pos, target.transform.eulerAngles); // compensate for rotation by Unity when importing
                positions.Add(pos);
                sb.AppendLine($"{pos.x:F6},{pos.y:F6},{pos.z:F6},{gaze.timestamp:F6}");
            }
        }

        //StringBuilder sb = new StringBuilder();
        //sb.AppendLine("x,y,z,intensity");
        //foreach (var pos in uniquePositions)
        //{
        //    float intensity = CalculateHeatmapIntensity(pos);
        //    sb.AppendLine($"{pos.x:F4},{pos.y:F4},{pos.z:F4},{intensity:F4}");
        //}

        File.WriteAllText(Path.Combine(dir, "pointcloud.csv"), sb.ToString());
        Debug.Log("POINTCLOUD AT:" + Path.Combine(dir, "pointcloud.csv").ToString());
    }

    //private Vector3 UnapplyUnityTranforms(Vector3 originalVector, float angleInDegrees)
    //{
    //    // Create a Quaternion for the rotation
    //    Quaternion rotation = Quaternion.AngleAxis(angleInDegrees, Vector3.right);

    //    // Rotate the vector using the Quaternion
    //    Vector3 rotatedVector = rotation * originalVector;

    //    // negate x, to flip x-axis, also to compensate for Unity importing behaviour
    //    return new Vector3(-rotatedVector.x, rotatedVector.y, rotatedVector.z);
    //}
   

    private float CalculateHeatmapIntensity(Vector3 position)
    {
        return positionFrequency.ContainsKey(position) ?
            (float)positionFrequency[position] / maxFrequency : 0f;
    }

    //private static string MeshToString(Mesh mesh)
    //{
    //    StringBuilder sb = new StringBuilder();
    //    sb.Append("# Exported Heatmap Object\n");

    //    foreach (Vector3 vertex in mesh.vertices)
    //    {
    //        sb.Append($"v {-vertex.x:F4} {vertex.y:F4} {vertex.z:F4}\n");
    //    }

    //    foreach (Vector3 normal in mesh.normals)
    //    {
    //        sb.Append($"vn {-normal.x:F4} {normal.y:F4} {normal.z:F4}\n");
    //    }

    //    foreach (Vector2 uv in mesh.uv)
    //    {
    //        sb.Append($"vt {-uv.x:F4} {uv.y:F4}\n");
    //    }

    //    for (int i = 0; i < mesh.subMeshCount; i++)
    //    {
    //        int[] triangles = mesh.GetTriangles(i);
    //        for (int j = 0; j < triangles.Length; j += 3)
    //        {
    //            sb.Append($"f {triangles[j] + 1}/{triangles[j] + 1}/{triangles[j] + 1} " +
    //                      $"{triangles[j + 1] + 1}/{triangles[j + 1] + 1}/{triangles[j + 1] + 1} " +
    //                      $"{triangles[j + 2] + 1}/{triangles[j + 2] + 1}/{triangles[j + 2] + 1}\n");
    //        }
    //    }

    //    return sb.ToString();
    //}

    //public string MeshToString(Mesh mesh, GameObject target)
    //{
    //    StringBuilder sb = new StringBuilder();
    //    sb.Append("# Exported Heatmap Object\n");

    //    // Make a copy of the mesh so we don't modify the original
    //    Mesh tempMesh = Instantiate(mesh);

    //    // Apply transformation to vertices
    //    Vector3[] transformedVertices = new Vector3[tempMesh.vertexCount];
    //    for (int i = 0; i < tempMesh.vertices.Length; i++)
    //    {
    //        transformedVertices[i] = UnapplyUnityTranforms(tempMesh.vertices[i], target.transform.rotation.x);
    //    }
    //    tempMesh.vertices = transformedVertices;

    //    // Recalculate normals based on transformed geometry
    //    tempMesh.RecalculateNormals();

    //    // Write out vertices
    //    foreach (Vector3 vertex in tempMesh.vertices)
    //    {
    //        sb.Append($"v {vertex.x:F4} {vertex.y:F4} {vertex.z:F4}\n");
    //    }

    //    // Write out normals
    //    foreach (Vector3 normal in tempMesh.normals)
    //    {
    //        sb.Append($"vn {normal.x:F4} {normal.y:F4} {normal.z:F4}\n");
    //    }

    //    // Write out UVs
    //    foreach (Vector2 uv in tempMesh.uv)
    //    {
    //        sb.Append($"vt {uv.x:F4} {uv.y:F4}\n");
    //    }

    //    // Write out faces (with winding order flipped)
    //    for (int i = 0; i < tempMesh.subMeshCount; i++)
    //    {
    //        int[] triangles = tempMesh.GetTriangles(i);
    //        for (int j = 0; j < triangles.Length; j += 3)
    //        {
    //            // Swap first and third index to reverse triangle winding
    //            int temp = triangles[j];
    //            triangles[j] = triangles[j + 2];
    //            triangles[j + 2] = temp;

    //            // Output face
    //            sb.Append($"f {triangles[j] + 1}/{triangles[j] + 1}/{triangles[j] + 1} " +
    //                      $"{triangles[j + 1] + 1}/{triangles[j + 1] + 1}/{triangles[j + 1] + 1} " +
    //                      $"{triangles[j + 2] + 1}/{triangles[j + 2] + 1}/{triangles[j + 2] + 1}\n");
    //        }
    //    }

    //    return sb.ToString();
    //}

    private string MeshToString(Mesh mesh, GameObject target)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("# Exported Gaze Object\n");

        Mesh tempMesh = Instantiate(mesh);

        Vector3[] transVertices = new Vector3[tempMesh.vertexCount];
        for (int i = 0; i < tempMesh.vertices.Length; i++)
        {
            transVertices[i] = UnapplyUnityTransforms(tempMesh.vertices[i], target.transform.eulerAngles);
        }
        tempMesh.vertices = transVertices;

        tempMesh.RecalculateNormals();

        foreach (Vector3 vertex in tempMesh.vertices)
        {
            sb.Append($"v {vertex.x:F6} {vertex.y:F6} {vertex.z:F6}\n");
        }

        foreach (Vector3 normal in tempMesh.normals)
        {
            sb.Append($"vn {normal.x:F6} {normal.y:F6} {normal.z:F6}\n");
        }

        foreach (Vector2 uv in tempMesh.uv)
        {
            sb.Append($"vt {uv.x:F6} {uv.y:F6}\n");
        }

        // Write out faces (with winding order flipped)
        for (int i = 0; i < tempMesh.subMeshCount; i++)
        {
            int[] triangles = tempMesh.GetTriangles(i);
            for (int j = 0; j < triangles.Length; j += 3)
            {
                // Swap first and third index to reverse triangle winding
                int temp = triangles[j];
                triangles[j] = triangles[j + 2];
                triangles[j + 2] = temp;

                // Output face
                sb.Append($"f {triangles[j] + 1}/{triangles[j] + 1}/{triangles[j] + 1} " +
                          $"{triangles[j + 1] + 1}/{triangles[j + 1] + 1}/{triangles[j + 1] + 1} " +
                          $"{triangles[j + 2] + 1}/{triangles[j + 2] + 1}/{triangles[j + 2] + 1}\n");
            }
        }

        return sb.ToString();
    }

    #endregion
}