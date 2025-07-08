//using Microsoft.MixedReality.Toolkit.Input;
//using Microsoft.MixedReality.Toolkit.SampleGazeData;
//using Microsoft.MixedReality.Toolkit.Utilities;
//using Microsoft.MixedReality.Toolkit;
//using System.Collections;
//using System.Collections.Generic;
//using System.IO;
//using System.Text;
//using UnityEngine;
//using System.IO.Compression;
//using System;

//public class ModelGazeRecorder : MonoBehaviour
//{
//    [System.Serializable]
//    public class GazeData
//    {
//        public double timestamp;
//        public Vector3 headPosition;
//        public Vector3 headForward;
//        public Vector3 eyeOrigin;
//        public Vector3 eyeDirection;
//        public Vector3 hitPosition;
//        public string targetName;
//        public Vector3 localHitPosition;
//    }

//    [System.Serializable]
//    public class TransformData
//    {
//        public double timestamp;
//        public Vector3 position;
//        public Quaternion rotation;
//        public Vector3 scale;
//        public Vector3 velocity;
//        public Vector3 angularVelocity;
//    }

//    [System.Serializable]
//    public class SessionData
//    {
//        public string selectedObjectName;
//        public List<GazeData> gazeData = new List<GazeData>();
//        public List<TransformData> transformData = new List<TransformData>();
//    }
//    private string selectedObjectName;

//    public bool isRecording;
//    public SessionData currentSession = new SessionData();
//    private Transform lastTransform;
//    private Vector3 lastPosition;
//    private Quaternion lastRotation;

//    private DrawOn3DTexture heatmapSource;
//    public MeshFilter meshFilter;

//    [Header("Voxel Settings")]
//    public float voxelSize = 0.005f;

//    [Header("Rotation Keys")]
//    [SerializeField] private KeyCode saveKey = KeyCode.BackQuote;

//    [Header("Gaussian Settings")]
//    [SerializeField] private float gaussianSigma = 0.005f;
//    //[SerializeField] private float gaussianRadius = 0.015f;
//    private List<Vector3> uniquePositions = new List<Vector3>();
//    private Dictionary<Vector3, int> positionFrequency = new Dictionary<Vector3, int>();
//    private int maxFrequency;

//    [Header("View BLocker")]
//    [SerializeField] private GameObject viewBlocker;

//    [Header("Audio Recording Settings")]
//    public int audioSampleRate = 44100;
//    public bool compressAudio = true;

//    private AudioClip recordedAudio;
//    private bool isRecordingAudio = false;
//    private string audioFilePath = string.Empty;
//    private AudioSource audioSource;
//    private float chunkStartTime = 0f;
//    private int chunkIndex = 0;
//    private List<string> savedFiles = new List<string>();

//    private void Start()
//    {
//        heatmapSource = GetComponent<DrawOn3DTexture>();

//        // Initialize audio source
//        GameObject audioObj = new GameObject("AudioRecorder");
//        audioSource = audioObj.AddComponent<AudioSource>();
//        DontDestroyOnLoad(audioObj);
//    }

//    void Update()
//    {
//        if (!isRecording) return;

//        var eyeTarget = EyeTrackingTarget.LookedAtEyeTarget;
//        var gazedObject = eyeTarget != null ? eyeTarget.gameObject : null;

//        if (ModelController.currentModel != null)
//        {
//            RecordGazeData(gazedObject);
//            RecordTransformData(ModelController.currentModel.transform);
//            currentSession.selectedObjectName = ModelController.currentModel.name;
//            selectedObjectName = currentSession.selectedObjectName;
//        }

//        if (Input.GetKeyDown(saveKey))
//        {
//            SaveAllData();
//        }

//        // Auto-save chunks every ~60 seconds
//        if (isRecordingAudio && Time.time - chunkStartTime >= 59f)
//        {
//            string dir = Path.Combine(Application.persistentDataPath, selectedObjectName);
//            if (!Directory.Exists(dir))
//            {
//                Directory.CreateDirectory(dir);
//            }

//            StopAudioRecording(); // Save current chunk
//            chunkIndex++; // Increment chunk index
//            StartCoroutine(RestartAudioRecordingAfterDelay());
//        }
//    }
//    public void SetIsRecording(bool val)
//    {
//        isRecording = val;
//        viewBlocker.SetActive(!val);

//        if (val && !isRecordingAudio)
//        {
//            chunkIndex = 0; // Reset chunk index
//            savedFiles.Clear(); // Clear file list
//            StartAudioRecording();
//        }
//        else if (!val && isRecordingAudio)
//        {
//            string dir = Path.Combine(Application.persistentDataPath, selectedObjectName);
//            if (!Directory.Exists(dir))
//            {
//                Directory.CreateDirectory(dir);
//            }

//            StopAudioRecording();

//            SaveFileList(dir); // Save file list
//        }
//    }

//    private IEnumerator RestartAudioRecordingAfterDelay()
//    {
//        yield return new WaitForSeconds(0.1f); // Small delay
//        StartAudioRecording();
//    }

//    private void StartAudioRecording()
//    {
//        recordedAudio = Microphone.Start(null, false, 60, audioSampleRate); // loop = false
//        isRecordingAudio = true;
//        chunkStartTime = Time.time;
//        Debug.Log("Started audio recording (60s chunk).");
//    }

//    private void StopAudioRecording()
//    {
//        if (!isRecordingAudio) return;

//        Microphone.End(null);
//        SaveAudioData(Path.Combine(Application.persistentDataPath, selectedObjectName));
//        isRecordingAudio = false;
//        Debug.Log("Stopped audio recording and saved final chunk.");
//    }

//    public void SaveAllData()
//    {
//        SaveSession("session.json");
//        ExportToCSV("gaze_data.csv", "transform_data.csv");
//        ExportAllFormats(ModelController.currentModel);
//    }

//    private void RecordGazeData(GameObject target)
//    {
//        var eyeProvider = CoreServices.InputSystem?.EyeGazeProvider;
//        if (eyeProvider == null) return;

//        var gaze = new GazeData
//        {
//            timestamp = Time.unscaledTimeAsDouble,
//            headPosition = CameraCache.Main.transform.position,
//            headForward = CameraCache.Main.transform.forward,
//            eyeOrigin = eyeProvider.GazeOrigin,
//            eyeDirection = eyeProvider.GazeDirection,
//            hitPosition = eyeProvider.IsEyeTrackingEnabledAndValid ? eyeProvider.HitPosition : Vector3.zero,
//            targetName = target != null ? target.name : "null"
//        };

//        if (target != null && target.name == currentSession.selectedObjectName)
//        {
//            // Convert to local space
//            gaze.localHitPosition = target.transform.InverseTransformPoint(gaze.hitPosition);
//        }
//        else
//        {
//            gaze.localHitPosition = Vector3.zero;
//        }

//        currentSession.gazeData.Add(gaze);
//    }

//    private void RecordTransformData(Transform targetTransform)
//    {
//        if (targetTransform == null) return;

//        // Calculate velocity
//        Vector3 currentPosition = targetTransform.position;
//        Vector3 velocity = (currentPosition - lastPosition) / Time.deltaTime;

//        // Calculate angular velocity using Euler angles with proper wrapping
//        Vector3 currentEuler = targetTransform.rotation.eulerAngles;
//        Vector3 lastEuler = lastRotation.eulerAngles;

//        Vector3 deltaEuler = new Vector3(
//            Mathf.DeltaAngle(lastEuler.x, currentEuler.x),
//            Mathf.DeltaAngle(lastEuler.y, currentEuler.y),
//            Mathf.DeltaAngle(lastEuler.z, currentEuler.z)
//        );

//        Vector3 angularVelocity = deltaEuler / Time.deltaTime;

//        var transformData = new TransformData
//        {
//            timestamp = Time.unscaledTimeAsDouble,
//            position = currentPosition,
//            rotation = targetTransform.rotation,
//            scale = targetTransform.localScale,
//            velocity = velocity,
//            angularVelocity = angularVelocity
//        };

//        currentSession.transformData.Add(transformData);

//        lastPosition = currentPosition;
//        lastRotation = targetTransform.rotation;
//    }

//    private byte[] ConvertAudioClipToWAV(AudioClip clip)
//    {
//        // Ensure the clip has valid data
//        if (clip == null || clip.samples == 0)
//            return null;

//        // Calculate data size
//        int channels = clip.channels;
//        int sampleCount = clip.samples;
//        int bitsPerSample = 16;
//        int byteRate = clip.frequency * channels * (bitsPerSample / 8);
//        int dataSize = sampleCount * channels * (bitsPerSample / 8);

//        // Create WAV header
//        byte[] header = new byte[44];
//        Buffer.BlockCopy(Encoding.UTF8.GetBytes("RIFF"), 0, header, 0, 4);
//        BitConverter.GetBytes((int)(dataSize + 36)).CopyTo(header, 4); // ChunkSize
//        Buffer.BlockCopy(Encoding.UTF8.GetBytes("WAVE"), 0, header, 8, 4);
//        Buffer.BlockCopy(Encoding.UTF8.GetBytes("fmt "), 0, header, 12, 4);
//        BitConverter.GetBytes((int)16).CopyTo(header, 16); // Subchunk1Size (PCM)
//        BitConverter.GetBytes((short)1).CopyTo(header, 20); // AudioFormat (PCM)
//        BitConverter.GetBytes((short)channels).CopyTo(header, 22); // Channels
//        BitConverter.GetBytes(clip.frequency).CopyTo(header, 24); // SampleRate
//        BitConverter.GetBytes(byteRate).CopyTo(header, 28); // ByteRate
//        BitConverter.GetBytes((short)(channels * (bitsPerSample / 8))).CopyTo(header, 32); // BlockAlign
//        BitConverter.GetBytes((short)bitsPerSample).CopyTo(header, 34); // BitsPerSample
//        Buffer.BlockCopy(Encoding.UTF8.GetBytes("data"), 0, header, 36, 4);
//        BitConverter.GetBytes((int)dataSize).CopyTo(header, 40); // DataSize

//        // Extract audio samples and convert to 16-bit PCM
//        float[] samples = new float[sampleCount * channels];
//        clip.GetData(samples, 0);

//        byte[] data = new byte[dataSize];
//        for (int i = 0; i < samples.Length; i++)
//        {
//            // Clamp and convert float [-1,1] to short [-32768,32767]
//            short val = (short)Mathf.Clamp(samples[i] * short.MaxValue, short.MinValue, short.MaxValue);
//            Buffer.BlockCopy(BitConverter.GetBytes(val), 0, data, i * 2, 2);
//        }

//        // Combine header and data
//        byte[] wavBytes = new byte[header.Length + data.Length];
//        Buffer.BlockCopy(header, 0, wavBytes, 0, header.Length);
//        Buffer.BlockCopy(data, 0, wavBytes, header.Length, data.Length);

//        return wavBytes;
//    }

//    private void SaveAudioData(string folderPath)
//    {
//        if (recordedAudio == null)
//        {
//            Debug.LogWarning("No audio recorded to save.");
//            return;
//        }

//        byte[] wavData = ConvertAudioClipToWAV(recordedAudio);
//        string audioFileName = $"session_audio_{chunkIndex}.wav";
//        string fullPath = Path.Combine(folderPath, audioFileName);
//        File.WriteAllBytes(fullPath, wavData);
//        Debug.Log($"Audio chunk saved: {fullPath}");

//        savedFiles.Add(audioFileName); // Track WAV chunk
//        chunkIndex++; // Increment for next chunk
//    }

//    private void SaveFileList(string folderPath)
//    {
//        StringBuilder sb = new StringBuilder();
//        sb.AppendLine("Saved Files:");
//        foreach (string file in savedFiles)
//        {
//            sb.AppendLine(file);
//        }

//        string listFilePath = Path.Combine(folderPath, "file_list.txt");
//        File.WriteAllText(listFilePath, sb.ToString());
//        Debug.Log("File list saved to: " + listFilePath);
//    }

//    public void SaveSession(string fileName)
//    {
//        string json = JsonUtility.ToJson(currentSession);
//        var dir = Path.Combine(Application.persistentDataPath, selectedObjectName);
//        if (!Directory.Exists(dir))
//        {
//            Directory.CreateDirectory(dir);
//        }
//        string fullPath = Path.Combine(dir, fileName);
//        File.WriteAllText(fullPath, json);
//        Debug.Log("JSON AT:" + fullPath);
//    }

//    public void ExportToCSV(string gazeFileName, string transformFileName)
//    {
//        StringBuilder gaze_csv = new StringBuilder();
//        StringBuilder transform_csv = new StringBuilder();

//        // Gaze Data Header
//        gaze_csv.AppendLine("Timestamp,HeadX,HeadY,HeadZ,HeadFwdX,HeadFwdY,HeadFwdZ,EyeOriginX,EyeOriginY,EyeOriginZ,EyeDirX,EyeDirY,EyeDirZ,HitX,HitY,HitZ,TargetName");

//        foreach (var gaze in currentSession.gazeData)
//        {
//            gaze_csv.AppendLine($"{gaze.timestamp:F6}," +
//                           $"{gaze.headPosition.x:F4},{gaze.headPosition.y:F4},{gaze.headPosition.z:F4}," +
//                           $"{gaze.headForward.x:F4},{gaze.headForward.y:F4},{gaze.headForward.z:F4}," +
//                           $"{gaze.eyeOrigin.x:F4},{gaze.eyeOrigin.y:F4},{gaze.eyeOrigin.z:F4}," +
//                           $"{gaze.eyeDirection.x:F4},{gaze.eyeDirection.y:F4},{gaze.eyeDirection.z:F4}," +
//                           $"{gaze.hitPosition.x:F4},{gaze.hitPosition.y:F4},{gaze.hitPosition.z:F4}," +
//                           $"{gaze.targetName}");
//        }

//        // Transform Data Header
//        transform_csv.AppendLine("\nTransformTimestamp,PosX,PosY,PosZ,RotX,RotY,RotZ,RotW,ScaleX,ScaleY,ScaleZ,VelX,VelY,VelZ,AngVelX,AngVelY,AngVelZ");

//        foreach (var trans in currentSession.transformData)
//        {
//            transform_csv.AppendLine($"{trans.timestamp:F6}," +
//                            $"{trans.position.x:F4},{trans.position.y:F4},{trans.position.z:F4}," +
//                            $"{trans.rotation.x:F4},{trans.rotation.y:F4},{trans.rotation.z:F4},{trans.rotation.w:F4}," +
//                            $"{trans.scale.x:F4},{trans.scale.y:F4},{trans.scale.z:F4}," +
//                            $"{trans.velocity.x:F4},{trans.velocity.y:F4},{trans.velocity.z:F4}," +
//                            $"{trans.angularVelocity.x:F4},{trans.angularVelocity.y:F4},{trans.angularVelocity.z:F4}");
//        }

//        var dir = Path.Combine(Application.persistentDataPath, selectedObjectName);
//        if (!Directory.Exists(dir))
//        {
//            Directory.CreateDirectory(dir);
//        }

//        File.WriteAllText(Path.Combine(dir, gazeFileName), gaze_csv.ToString());
//        Debug.Log("GAZE CSV AT:" + Path.Combine(dir, gazeFileName).ToString());

//        File.WriteAllText(Path.Combine(dir, transformFileName), transform_csv.ToString());
//        Debug.Log("TRANSFORM CSV AT:" + Path.Combine(dir, transformFileName).ToString());
//    }


//    public void ExportAllFormats(GameObject target)
//    {
//        Export3DModel();
//        ExportPointCloud(target);
//    }

//    private void Export3DModel()
//    {
//        Mesh mesh = meshFilter.sharedMesh;
//        string objContent = MeshToString(mesh);

//        var dir = Path.Combine(Application.persistentDataPath, selectedObjectName);
//        if (!Directory.Exists(dir))
//        {
//            Directory.CreateDirectory(dir);
//        }

//        File.WriteAllText(Path.Combine(dir, "heatmap.obj"), objContent);
//        Debug.Log("OBJ AT:" + Path.Combine(dir, "heatmap.obj").ToString());
//    }

//    private Vector3 GetAdjustedPosition(GazeData gaze)
//    {
//        // Use local position if available and valid
//        if (gaze.targetName == currentSession.selectedObjectName &&
//            gaze.localHitPosition != Vector3.zero)
//        {
//            return gaze.localHitPosition;
//        }
//        return gaze.hitPosition;
//    }

//    private void ExportPointCloud(GameObject target)
//    {
//        positionFrequency.Clear();
//        uniquePositions.Clear();
//        maxFrequency = 0;

//        // Get the local bounds of the target GameObject
//        Renderer targetRenderer = target.GetComponent<Renderer>();
//        if (targetRenderer == null)
//        {
//            Debug.LogError("Target GameObject does not have a Renderer component.");
//            return;
//        }
//        Bounds localBounds = targetRenderer.localBounds; // Local bounds of the target

//        // First pass: build frequency dictionary and unique positions list
//        foreach (var gaze in currentSession.gazeData)
//        {
//            if (gaze.hitPosition == Vector3.zero) continue;

//            Vector3 pos = GetAdjustedPosition(gaze);

//            // If the position is in world space, convert it to local space
//            if (gaze.targetName != currentSession.selectedObjectName ||
//                gaze.localHitPosition == Vector3.zero)
//            {
//                pos = target.transform.InverseTransformPoint(pos); // Convert world to local space
//            }

//            // Check if the gaze hit is within the local bounds of the target
//            if (localBounds.Contains(pos) && gaze.targetName == target.name && gaze.targetName != "null")
//            {
//                if (positionFrequency.ContainsKey(pos))
//                {
//                    positionFrequency[pos]++;
//                }
//                else
//                {
//                    positionFrequency[pos] = 1;
//                    uniquePositions.Add(pos);
//                }

//                if (positionFrequency[pos] > maxFrequency)
//                {
//                    maxFrequency = positionFrequency[pos];
//                }
//            }
//        }

//        // Second pass: write data with Normalized intensity
//        StringBuilder sb = new StringBuilder();
//        sb.AppendLine("x,y,z,intensity");

//        foreach (var pos in uniquePositions)
//        {
//            float intensity = CalculateHeatmapIntensity(pos);

//            sb.AppendLine($"{pos.x:F4},{pos.y:F4},{pos.z:F4},{intensity:F4}");
//        }

//        var dir = Path.Combine(Application.persistentDataPath, selectedObjectName);
//        if (!Directory.Exists(dir))
//        {
//            Directory.CreateDirectory(dir);
//        }

//        File.WriteAllText(Path.Combine(dir, "pointcloud.csv"), sb.ToString());
//        Debug.Log("POINTCLOUD AT:" + Path.Combine(dir, "pointcloud.csv").ToString());
//    }

//    private float CalculateHeatmapIntensity(Vector3 position)
//    {
//        return positionFrequency.ContainsKey(position) ?
//            (float)positionFrequency[position] / maxFrequency : 0f;
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
