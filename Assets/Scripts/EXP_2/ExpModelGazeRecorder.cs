using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.SampleGazeData;
using Microsoft.MixedReality.Toolkit.Utilities;
using TMPro;
using static UnityEngine.GraphicsBuffer;

public class ExpModelGazeRecorder : MonoBehaviour
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
    public class SessionData
    {
        public string selectedObjectName;
        public List<GazeData> gazeData = new List<GazeData>();
    }

    [Header("View Blocker")]
    [SerializeField] private GameObject viewBlocker;

    [Header("Prompt")]
    [SerializeField] private GameObject promptObject;

    [Header("Heatmap / Mesh Settings")]
    public MeshFilter meshFilter;

    private float recordGazeDuration = 60.0f;
    private float recordVoiceDuration = 60.0f;

    [Header("Audio Recording Settings")]
    public int audioSampleRate = 44100;

    public string sessionPath;
    public bool isRecording;
    public SessionData currentSession = new SessionData();

    private float timer = 0;
    private string saveDir;
    private double startingTime;
    private DrawOn3DTexture heatmapSource;
    private StringBuilder gaze_csv = new StringBuilder();
    private Renderer targetRenderer;
    private Bounds localBounds;
    private StringBuilder pc_sb = new StringBuilder();

    // Audio recording fields
    private AudioClip recordedAudio;
    private bool isRecordingAudio;
    private AudioSource audioSource;
    private float chunkStartTime = 0f;
    private int chunkIndex = 0;
    private List<string> savedFiles = new List<string>();

    // control flow flags
    private bool savedGaze;

    // prompt text
    private string question = "Say most impressive parts of the object and your impression (feeling)";

    void Start()
    {
        heatmapSource = GetComponent<DrawOn3DTexture>();

        GameObject audioObject = new GameObject("AudioRecorder");
        audioSource = audioObject.AddComponent<AudioSource>();
        DontDestroyOnLoad(audioObject);

        promptObject.SetActive(true);
        promptObject.GetComponent<TextMeshPro>().SetText("Say 'Start'");
    }

    void Update()
    {
        /* CHECK IF RECORDING STARTED */
        if (!isRecording || ExpModelController.currentModel == null) return;

        /* GET GAZED OBJECT */
        var eyeTarget = EyeTrackingTarget.LookedAtEyeTarget;
        var gazedObject = eyeTarget != null ? eyeTarget.gameObject : null;

        /* RECORD GAZE DATA */
        if (timer > recordVoiceDuration)
        {
            timer -= Time.deltaTime;

            RecordGazeData(gazedObject);

            promptObject.GetComponent<TextMeshPro>().SetText($"\nVIEWING TIME: {(timer - recordVoiceDuration):F1}");
        }
        /* RECORD VOICE DATA */
        else if (timer > 0)
        {
            timer -= Time.deltaTime;

            promptObject.GetComponent<TextMeshPro>().SetText(question + $"\nTIME: {timer:F1}");

            if (!savedGaze)
            {
                GetComponent<DrawOn3DTexture>().ToggleLiveHeatmap(false);
                //SaveAllData(); // moved to end to prevent lag
                savedGaze = true;
            }
        }
        /* STOP ALL RECORDING & SAVE DATA */
        else
        {
            promptObject.GetComponent<TextMeshPro>().SetText("Say 'Next'");

            SaveAllData();

            StopAudioRecording();
            SaveFileList();
            SetIsRecording(false);

            ExpModelController.ToggleRecorded();
        }

        /* RESTART AUDIO RECORDING EVERY 60 SECONDS */
        if (isRecordingAudio && Time.time - chunkStartTime >= 59.9f)
        {
            StopAudioRecording(); // Save current chunk
            StartCoroutine(RestartAudioRecordingAfterDelay());
        }
    }

    public void SetIsRecording(bool val) 
    {
        /* Reset & clear variables (isRecording, timer, startingTime, savedGaze) */
        isRecording = val;
        timer = recordGazeDuration + recordVoiceDuration; // reset timer
        startingTime = Time.unscaledTimeAsDouble; // record starting time
        savedGaze = !val; // reset recording state

        /* Toggle viewBlocker */
        viewBlocker.SetActive(!val);

        if (val && ExpModelController.currentModel != null)
        {
            /* Set session data */
            currentSession.selectedObjectName = ExpModelController.currentModel.name;

            //gaze_csv = new StringBuilder();
            //gaze_csv.AppendLine("Timestamp,HeadX,HeadY,HeadZ,HeadFwdX,HeadFwdY,HeadFwdZ,EyeOriginX,EyeOriginY,EyeOriginZ,EyeDirX,EyeDirY,EyeDirZ,HitX,HitY,HitZ,TargetName");

            /* Set gaze object information */
            targetRenderer = ExpModelController.currentModel.GetComponent<Renderer>();
            localBounds = targetRenderer.localBounds;

            /* Initialize point cloud data header */
            pc_sb = new StringBuilder();
            pc_sb.AppendLine("x,y,z,timestamp");

            /* Create data directory */
            saveDir = Path.Combine(Application.persistentDataPath, sessionPath, currentSession.selectedObjectName);
            if (!Directory.Exists(saveDir)) 
            { 
                Directory.CreateDirectory(saveDir);
            }
        } else
        {
            /* Show prompt “Say ‘Next’” */
            promptObject.GetComponent<TextMeshPro>().SetText("Say 'Next'");
        }

        // start audio recording here
        if (val && !isRecordingAudio)
        {
            /* Clear recording variables */
            chunkIndex = 0; // Reset chunk index
            savedFiles.Clear(); // Clear file list

            /* Start voice recording */
            StartAudioRecording();
        }
        else if (!val && isRecordingAudio)
        {
            /* Stop voice recording */
            StopAudioRecording();

            /* Export voice recording data */
            SaveFileList();
        }
    }

    /* RESTART AUDIO RECORDING EVERY 60 SECONDS */
    private IEnumerator RestartAudioRecordingAfterDelay()
    {
        yield return new WaitForSeconds(0.1f); // Small delay
        StartAudioRecording();
    }

    private void StartAudioRecording()
    {
        /* RECORD AUDIO FOR 60 SECONDS */
        recordedAudio = Microphone.Start(null, false, 60, audioSampleRate); // loop = false
        isRecordingAudio = true;
        chunkStartTime = Time.time;
        Debug.Log("Started audio recording (60s chunk).");
    }

    private void StopAudioRecording()
    {
        if (!isRecordingAudio) return;
        Microphone.End(null);

        /* SAVE AUDIO CLIP */
        SaveAudioData();
        isRecordingAudio = false;
        Debug.Log("Stopped audio recording and saved final chunk.");
    }

    private byte[] ConvertAudioClipToWAV(AudioClip clip) 
    {
        if (clip == null || clip.samples == 0) return null;

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

    private void SaveAudioData()
    {
        if (recordedAudio == null)
        {
            return;
        }

        byte[] wavData = ConvertAudioClipToWAV(recordedAudio);
        string audioFileName = $"session_audio_{chunkIndex}.wav";
        string fullPath = Path.Combine(saveDir, audioFileName);
        File.WriteAllBytes(fullPath, wavData);
        Debug.Log($"Audio chunk saved: {fullPath}");
        savedFiles.Add(audioFileName);
        chunkIndex++;
    }

    private void SaveFileList()
    {
        // IN CMD run: ffmpeg -f concat -safe 0 -i filelist.txt -c copy output.wav
        StringBuilder sb = new StringBuilder();
        foreach (string file in savedFiles)
        {
            sb.AppendLine("file '" + file + "'");
        }

        string listFilePath = Path.Combine(saveDir, "filelist.txt");
        File.WriteAllText(listFilePath, sb.ToString());
        Debug.Log("File list saved to: " + listFilePath);
    }

    private void RecordGazeData(GameObject target) 
    {
        /* GET EYE GAZE PROVIDER */
        var eyeProvider = CoreServices.InputSystem?.EyeGazeProvider;
        if (eyeProvider == null) return;

        /* CREATE NEW GAZE DATA */
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

        /* CHECK IF GAZE HIT ON SELECTED MODEL */
        if (target != null && target.name == currentSession.selectedObjectName)
        {
            /* CONVERT GAZE HIT FROM WORLD COORDINATE TO LOCAL COORDINATE */
            gaze.localHitPosition = target.transform.InverseTransformPoint(gaze.hitPosition);
            Vector3 pos = gaze.localHitPosition;

            /* CHECK IF GAZE HIT IS ON SELECTED MODEL */
            if (localBounds.Contains(pos) && gaze.targetName == target.name && gaze.targetName != "null")
            {
                /* REVERT TRANSFORMS WHEN IMPORTING MODEL */
                pos = UnapplyUnityTransforms(pos, target.transform.eulerAngles);

                /* ADD GAZE DATA */
                pc_sb.AppendLine($"{pos.x:F6},{pos.y:F6},{pos.z:F6},{gaze.timestamp:F6}");
            }
        }
        else
        {
            gaze.localHitPosition = Vector3.zero;
        }

        //currentSession.gazeData.Add(gaze);

        //gaze_csv.AppendLine($"{gaze.timestamp:F6}," +
        //                   $"{gaze.headPosition.x:F4},{gaze.headPosition.y:F4},{gaze.headPosition.z:F4}," +
        //                   $"{gaze.headForward.x:F4},{gaze.headForward.y:F4},{gaze.headForward.z:F4}," +
        //                   $"{gaze.eyeOrigin.x:F4},{gaze.eyeOrigin.y:F4},{gaze.eyeOrigin.z:F4}," +
        //                   $"{gaze.eyeDirection.x:F4},{gaze.eyeDirection.y:F4},{gaze.eyeDirection.z:F4}," +
        //                   $"{gaze.hitPosition.x:F4},{gaze.hitPosition.y:F4},{gaze.hitPosition.z:F4}," +
        //                   $"{gaze.targetName}");
    }

    private Vector3 UnapplyUnityTransforms(Vector3 originalVector, Vector3 anglesInDegrees)
    {
        /* REVERSE ANY ROTATION ON MODEL */
        Quaternion xRotation = Quaternion.AngleAxis(anglesInDegrees.x, Vector3.right);
        Quaternion yRotation = Quaternion.AngleAxis(anglesInDegrees.y, Vector3.up);
        Quaternion zRotation = Quaternion.AngleAxis(anglesInDegrees.z, Vector3.forward);

        Vector3 rotatedVector = xRotation * originalVector;
        rotatedVector = yRotation * rotatedVector;
        rotatedVector = zRotation * rotatedVector;

        /* NEGATE X TO FLIP THE X-AXIS */
        return new Vector3(-rotatedVector.x, rotatedVector.y, rotatedVector.z);
    }

    public void ResetAll() 
    {
        if (isRecording)
        {
            SetIsRecording(false);
        }
        currentSession = new ExpModelGazeRecorder.SessionData();
        promptObject.GetComponent<TextMeshPro>().SetText("Say 'Start'");
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
        //SaveSession("session.json");
        //SaveGazeData("gaze_data.csv");
        ExportPointCloud(ExpModelController.currentModel);
        Export3DModel(ExpModelController.currentModel);
        Debug.Log("SAVED DATA AT: " + saveDir);
    }

    public void SaveSession(string fileName) 
    {
        string json = JsonUtility.ToJson(currentSession);
        File.WriteAllText(Path.Combine(saveDir, fileName), json);
    }

    public void SaveGazeData(string fileName) 
    {
        //StringBuilder gaze_csv = new StringBuilder();

        //gaze_csv.AppendLine("Timestamp,HeadX,HeadY,HeadZ,HeadFwdX,HeadFwdY,HeadFwdZ,EyeOriginX,EyeOriginY,EyeOriginZ,EyeDirX,EyeDirY,EyeDirZ,HitX,HitY,HitZ,TargetName");
        //foreach (var gaze in currentSession.gazeData)
        //{
        //    gaze_csv.AppendLine($"{gaze.timestamp:F6}," +
        //                   $"{gaze.headPosition.x:F4},{gaze.headPosition.y:F4},{gaze.headPosition.z:F4}," +
        //                   $"{gaze.headForward.x:F4},{gaze.headForward.y:F4},{gaze.headForward.z:F4}," +
        //                   $"{gaze.eyeOrigin.x:F4},{gaze.eyeOrigin.y:F4},{gaze.eyeOrigin.z:F4}," +
        //                   $"{gaze.eyeDirection.x:F4},{gaze.eyeDirection.y:F4},{gaze.eyeDirection.z:F4}," +
        //                   $"{gaze.hitPosition.x:F4},{gaze.hitPosition.y:F4},{gaze.hitPosition.z:F4}," +
        //                   $"{gaze.targetName}");
        //}

        File.WriteAllText(Path.Combine(saveDir, fileName), gaze_csv.ToString());
    }

    public void ExportPointCloud(GameObject target) 
    {
        //Renderer targetRenderer = target.GetComponent<Renderer>();
        //if (targetRenderer == null) return;

        //Bounds localBounds = targetRenderer.localBounds;

        //StringBuilder pc_sb = new StringBuilder();
        //pc_sb.AppendLine("x,y,z,timestamp");

        //foreach (var gaze in currentSession.gazeData) 
        //{ 
        //    if (gaze.localHitPosition == Vector3.zero) continue;
        //    Vector3 pos = gaze.localHitPosition;

        //    if (localBounds.Contains(pos) && gaze.targetName == target.name && gaze.targetName != "null") 
        //    {
        //        pos = UnapplyUnityTransforms(pos, target.transform.eulerAngles);
        //        pc_sb.AppendLine($"{pos.x:F6},{pos.y:F6},{pos.z:F6},{gaze.timestamp:F6}");
        //    }
        //}

        File.WriteAllText(Path.Combine(saveDir, "pointcloud.csv"), pc_sb.ToString());
    }

    public void Export3DModel(GameObject target) 
    { 
        Mesh mesh = meshFilter.sharedMesh;
        string objContent = MeshToString(mesh, target);
        File.WriteAllText(Path.Combine(saveDir, "model.obj"), objContent);
    }

    private string MeshToString(Mesh mesh, GameObject target) 
    { 
        StringBuilder sb = new StringBuilder();
        sb.Append("# Exported Gaze Object\n");

        Mesh tempMesh = Instantiate(mesh);

        Vector3[] transVertices = new Vector3[tempMesh.vertexCount];
        for (int i=0; i < tempMesh.vertices.Length; i++)
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
}
