#region Version 2 | Cleaned Up Code from Version 1 | LU HOU YANG
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
using UnityEngine.Windows.Speech;

/// <summary>
/// This script is attached on each pottery / dogu derived from the prefab [MODEL_2]
/// The Update() function contains the main data collecting flow
/// It is initiated by calling SetIsRecording(True) from the [PREFIX]ModelController.cs
/// The capabilities of this script are
/// 1) Start / stop data collection
/// 2) Record eye gaze, voice, QNA input
/// 3) Export / save the data to HoloLens 2 local storage
/// 
/// このスクリプトは、プレハブ [MODEL_2] から派生した各土器／土偶にアタッチされます。
/// Update() 関数には主要なデータ収集フローが含まれています。
/// このスクリプトは、[PREFIX]ModelController.cs から SetIsRecording(True) を呼び出すことで開始されます。
/// このスクリプトの機能は以下の通りです：
/// 1) データ収集の開始／停止
/// 2) 視線、音声、Q&A入力の記録
/// 3) データをHoloLens 2のローカルストレージにエクスポート／保存
/// </summary>
public class QNAModelGazeRecorder : MonoBehaviour
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
        public List<QuestionnaireAnswer> questionnaireAnswers = new List<QuestionnaireAnswer>();
    }

    [System.Serializable]
    public class QuestionnaireAnswer
    {
        public double timestamp;
        public string answer;
        public Vector3 estimatedGamePosition;
    }

    [Header("View Blocker")]
    [SerializeField] private GameObject viewBlocker;

    [Header("Prompt")]
    [SerializeField] private GameObject promptObject;

    [Header("Heatmap / Mesh Settings")]
    public MeshFilter meshFilter;

    // Experiment duration control
    private float recordGazeDuration = 60.0f;
    private float recordVoiceDuration = 45.0f;

    // Recording state and data
    public string sessionPath;
    public bool isRecording;
    public SessionData currentSession = new SessionData();

    private float timer = 0;
    private string saveDir;
    private double startingTime;
    private DrawOn3DTexture heatmapSource;
    private Renderer targetRenderer;
    private Bounds localBounds;
    private StringBuilder pc_sb = new StringBuilder();
    private Vector3 localHitPosition;

    // Control flow flags
    private bool savedGaze;

    [Header("Audio Recording Settings")]
    public int audioSampleRate = 44100;

    // Audio recording fields
    private AudioClip recordedAudio;
    private bool isRecordingAudio;
    private AudioSource audioSource;
    private float chunkStartTime = 0f;
    private int chunkIndex = 0;
    private List<string> savedFiles = new List<string>();

    // Audio recording prompt variables
    private Vector3 promptInitialPosition;
    private string question = "「この土器/土偶の全体的あるいは部分的な印象をなるべく具体的な言葉を使って45秒以内で話してください」";
    private float rotationSpeed = 5f;
    private float rotationThresholdDegrees = 1.0f;
    private float followDistance = 1.5f;
    private float moveSpeed = 5f;
    private Vector3 rotationDisplacement = new Vector3(0f, 0f, 0f);
    private float horizontalDisplacement = 0.0f;
    private float verticalDisplacement = 0.15f;
    private GameObject objectWithCollider = null;
    private Quaternion targetRotation; // The rotation we are trying to achieve
    private Vector3 targetPosition; // The position we are trying to achieve

    // QNA variables
    private string[] answerChoices = new string[]
    {
        "面白い・気になる形だ",
        "不思議・意味不明",
        "何も感じない",
        "不気味・不安・怖い",
        "美しい・芸術的だ",
    };
    private bool isPlayingAudio = false;
    private float audioTimer = 1.0f;

    void Start()
    {
        heatmapSource = GetComponent<DrawOn3DTexture>();

        GameObject audioObject = new GameObject("AudioRecorder");
        audioSource = audioObject.AddComponent<AudioSource>();
        DontDestroyOnLoad(audioObject);

        promptObject.SetActive(true);
        promptInitialPosition = promptObject.transform.localPosition;
        promptObject.GetComponent<TextMeshPro>().SetText("「Enter」キーを押してください");

        // audio prompt
        if (objectWithCollider == null)
        {
            Collider coll = GetComponent<Collider>();
            if (coll == null)
            {
                coll = GetComponentInChildren<Collider>();
            }
            if (coll != null)
            {
                objectWithCollider = coll.gameObject;
            }
            else
            {
                Debug.LogWarning("FaceUser: No collider found on this GameObject or its children.");
            }
        }

        targetRotation = promptObject.transform.rotation;
        targetPosition = promptObject.transform.position; // Initialize target position
    }

    // Control Logic for Experiment
    void Update()
    {
        /* CHECK IF RECORDING STARTED */
        if (!isRecording || QNAModelController.currentModel == null) return;

        /* GET GAZED OBJECT */
        var eyeTarget = EyeTrackingTarget.LookedAtEyeTarget;
        var gazedObject = eyeTarget != null ? eyeTarget.gameObject : null;

        /* RECORD GAZE DATA */
        if (timer > recordVoiceDuration)
        {
            timer -= Time.deltaTime;

            // Pass the gazed voxel ID to RecordGazeData
            RecordGazeData(gazedObject);

            promptObject.GetComponent<TextMeshPro>().SetText($"VIEWING TIME: {(timer - recordVoiceDuration):F1}");

            CheckKeyboardInput();
        }
        /* RECORD VOICE DATA */
        else if (timer > 0)
        {
            timer -= Time.deltaTime;

            promptObject.GetComponent<TextMeshPro>().SetText(question + $"TIME: {timer:F1}");
            Vector3 cameraPosition = CameraCache.Main.transform.position;
            Vector3 currentPosition = promptObject.transform.position;
            Quaternion currentRotation = promptObject.transform.rotation;
            Transform mainCameraTransform = CameraCache.Main.transform;
            Vector3 directionToCamera = -(cameraPosition - currentPosition + rotationDisplacement).normalized;
            targetRotation = Quaternion.LookRotation(directionToCamera);

            float rotationStep = rotationSpeed * Time.deltaTime;
            promptObject.transform.rotation = Quaternion.Slerp(currentRotation, targetRotation, rotationStep);

            // Snap rotation if close enough
            if (Quaternion.Angle(currentRotation, targetRotation) < rotationThresholdDegrees)
            {
                promptObject.transform.rotation = targetRotation;
            }
            targetPosition = cameraPosition + mainCameraTransform.forward * followDistance + mainCameraTransform.right * horizontalDisplacement + mainCameraTransform.up * verticalDisplacement;

            // Smoothly move towards the target position
            float moveStep = moveSpeed * Time.deltaTime;
            promptObject.transform.position = Vector3.Lerp(currentPosition, targetPosition, moveStep);

            if (!savedGaze)
            {
                GetComponent<DrawOn3DTexture>().ToggleLiveHeatmap(false);
                QNAModelController.StopSineWave();
                QNAModelController.ToggleQnaPrompt();

                /* Clear recording variables */
                chunkIndex = 0; // Reset chunk index
                savedFiles.Clear(); // Clear file list

                /* Start voice recording */
                StartAudioRecording();

                savedGaze = true;
            }
        }
        else
        {
            promptObject.GetComponent<TextMeshPro>().SetText("");

            SaveAllData();
            StopAudioRecording();
            SaveFileList();
            SetIsRecording(false);

            QNAModelController.ToggleRecorded();
        }
        //}
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

        if (val && QNAModelController.currentModel != null)
        {
            /* Set session data */
            currentSession.selectedObjectName = QNAModelController.currentModel.name;

            /* Set gaze object information */
            targetRenderer = QNAModelController.currentModel.GetComponent<Renderer>();
            // Use currentModel.GetComponent<MeshFilter>().sharedMesh.bounds if the renderer bounds aren't accurate
            localBounds = targetRenderer.localBounds;

            /* Initialize point cloud data header */
            pc_sb = new StringBuilder();
            pc_sb.AppendLine("x,y,z,timestamp,gazedVoxelID");

            /* Create data directory */
            saveDir = Path.Combine(Application.persistentDataPath, sessionPath, currentSession.selectedObjectName);
            if (!Directory.Exists(saveDir))
            {
                Directory.CreateDirectory(saveDir);
            }
        }
        else
        {
            promptObject.GetComponent<TextMeshPro>().SetText("");
            promptObject.transform.localPosition = promptInitialPosition;
        }

        // start audio recording here
        if (val && !isRecordingAudio)
        {

        }
        else if (!val && isRecordingAudio)
        {
            /* Stop voice recording */
            StopAudioRecording();

            /* Export voice recording data */
            SaveFileList();
        }
    }

    // Record Eye Gaze Data
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
            targetName = target != null ? target.name : "null",
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

                localHitPosition = pos;

                /* ADD GAZE DATA */
                pc_sb.AppendLine($"{pos.x:F6},{pos.y:F6},{pos.z:F6},{gaze.timestamp:F6}");
            }
        }
        else
        {
            gaze.localHitPosition = Vector3.zero;
        }
    }

    #region AUDIO DATA
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
    #endregion

    #region UTILS
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
        currentSession = new QNAModelGazeRecorder.SessionData();
        promptObject.GetComponent<TextMeshPro>().SetText("「Enter」キーを押してください");

        if (heatmapSource != null)
        {
            heatmapSource.ClearDrawing();
        }
    }
    #endregion

    #region EXPORT DATA
    public void SaveAllData()
    {
        ExportPointCloud(QNAModelController.currentModel);
        Export3DModel(QNAModelController.currentModel);
        SaveQuestionnaireAnswers(); // Save the answers
        Debug.Log("SAVED DATA AT: " + saveDir);
    }

    public void ExportPointCloud(GameObject target)
    {
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

    #region QNA FUNCTIONS
    private void CheckKeyboardInput()
    {
        if (Input.GetKey(KeyCode.Alpha4) || Input.GetKey(KeyCode.Keypad4))
        {
            SelectAnswerByNumber(1);
        }
        else if (Input.GetKey(KeyCode.Alpha6) || Input.GetKey(KeyCode.Keypad6))
        {
            SelectAnswerByNumber(2);
        }
        else if (Input.GetKey(KeyCode.Alpha8) || Input.GetKey(KeyCode.Keypad8))
        {
            SelectAnswerByNumber(3);
        }
        else if (Input.GetKey(KeyCode.Alpha2) || Input.GetKey(KeyCode.Keypad2))
        {
            SelectAnswerByNumber(4);
        }
        else if (Input.GetKey(KeyCode.Alpha5) || Input.GetKey(KeyCode.Keypad5))
        {
            SelectAnswerByNumber(5);
        }
        else
        {
            if (!isPlayingAudio)
            {
                QNAModelController.StopSineWave();
            }
            else
            {
                audioTimer -= Time.deltaTime;

                if (audioTimer < 0)
                {
                    isPlayingAudio = false;
                    audioTimer = 1.0f;
                }
            }
        }
    }

    private void SelectAnswerByNumber(int number)
    {
        if (number > 0 && number <= answerChoices.Length)
        {
            if (!isPlayingAudio)
            {
                StartCoroutine(QNAModelController.PlayMajorFifthInterval(1.0f));
                isPlayingAudio = true;
            }
            else
            {
                audioTimer -= Time.deltaTime;

                if (audioTimer < 0)
                {
                    isPlayingAudio = false;
                    audioTimer = 1.0f;
                }
            }
            OnQuestionnaireAnswered(answerChoices[number - 1]);
        }
        else
        {
            Debug.LogWarning($"Attempted to select an invalid answer number: {number}");
        }
    }

    private void OnQuestionnaireAnswered(string selectedAnswer)
    {
        currentSession.questionnaireAnswers.Add(new QuestionnaireAnswer
        {
            timestamp = Time.unscaledTimeAsDouble - startingTime,
            answer = selectedAnswer,
            estimatedGamePosition = localHitPosition
        });
    }

    private void SaveQuestionnaireAnswers()
    {
        if (currentSession.questionnaireAnswers.Count == 0) return;

        StringBuilder qa_sb = new StringBuilder();
        qa_sb.AppendLine("estX,estY,estZ,answer,timestamp");

        foreach (var qa in currentSession.questionnaireAnswers)
        {
            qa_sb.AppendLine($"{qa.estimatedGamePosition.x:F6},{qa.estimatedGamePosition.y:F6},{qa.estimatedGamePosition.z:F6},{qa.answer},{qa.timestamp:F6}");
        }

        File.WriteAllText(Path.Combine(saveDir, "qa.csv"), qa_sb.ToString());
    }
    #endregion
}
#endregion

#region Version 1 | LU HOU YANG
//using System;
//using System.IO;
//using System.Text;
//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;
//using Microsoft.MixedReality.Toolkit;
//using Microsoft.MixedReality.Toolkit.Input;
//using Microsoft.MixedReality.Toolkit.SampleGazeData;
//using Microsoft.MixedReality.Toolkit.Utilities;
//using TMPro;
//using UnityEngine.Windows.Speech;

//public class QNAModelGazeRecorder : MonoBehaviour
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
//        //public string gazedVoxelID;
//    }

//    [System.Serializable]
//    public class SessionData
//    {
//        public string selectedObjectName;
//        public List<GazeData> gazeData = new List<GazeData>();
//        public List<QuestionnaireAnswer> questionnaireAnswers = new List<QuestionnaireAnswer>();
//    }

//    [System.Serializable]
//    public class QuestionnaireAnswer
//    {
//        public double timestamp;
//        public string answer;
//        public Vector3 estimatedGamePosition;
//    }

//    [Header("View Blocker")]
//    [SerializeField] private GameObject viewBlocker;

//    [Header("Prompt")]
//    [SerializeField] private GameObject promptObject;

//    [Header("Heatmap / Mesh Settings")]
//    public MeshFilter meshFilter;

//    //[Header("Voxel Gaze Settings")]
//    //[SerializeField] private GameObject questionPopupPrefab;
//    //[SerializeField] private float voxelSize = 30.0f; // Size of your voxels
//    ////private float voxelSize = 30.0f; // Size of your voxels
//    //private int maxGazeEventsPerVoxel = 3; // How many times can a voxel trigger a pop-up in a session?
//    //private float requiredVoxelGazeDuration = 1.25f;

//    private float recordGazeDuration = 60.0f;
//    private float recordVoiceDuration = 45.0f;

//    public string sessionPath;
//    public bool isRecording;
//    public SessionData currentSession = new SessionData();

//    private float timer = 0;
//    private string saveDir;
//    private double startingTime;
//    private DrawOn3DTexture heatmapSource;
//    private Renderer targetRenderer;
//    private Bounds localBounds;
//    private StringBuilder pc_sb = new StringBuilder();

//    //private string currentGazedVoxelID = null; // Stores "x_y_z" string for current voxel
//    //private float currentVoxelGazeTimer = 0f;
//    //private bool isQuestionnaireActive = false; // To prevent multiple pop-ups

//    //// Dictionary to store gaze duration for each voxel
//    //private Dictionary<string, float> voxelDwellTimes = new Dictionary<string, float>();
//    //// Dictionary to track how many times a voxel has triggered a pop-up
//    //private Dictionary<string, int> voxelTriggerCounts = new Dictionary<string, int>();

//    //private Vector3 voxelTriggerPosition;
//    //private bool isLiveHeatmap = false;

//    Vector3 localHitPosition;

//    [Header("Audio Recording Settings")]
//    public int audioSampleRate = 44100;

//    // Audio recording fields
//    private AudioClip recordedAudio;
//    private bool isRecordingAudio;
//    private AudioSource audioSource;
//    private float chunkStartTime = 0f;
//    private int chunkIndex = 0;
//    private List<string> savedFiles = new List<string>();

//    // control flow flags
//    private bool savedGaze;

//    // prompt text
//    private Vector3 promptInitialPosition;
//    private string question = "「この土器/土偶の全体的あるいは部分的な印象をなるべく具体的な言葉を使って45秒以内で話してください」";
//    private float rotationSpeed = 5f; // Degrees per second
//    private float rotationThresholdDegrees = 1.0f; // Smaller threshold for closer snapping
//    private float followDistance = 1.5f;
//    private float moveSpeed = 5f;
//    private Vector3 rotationDisplacement = new Vector3(0f, 0f, 0f);
//    private float horizontalDisplacement = 0.0f; // This will now represent displacement to the right
//    private float verticalDisplacement = 0.15f;
//    private GameObject objectWithCollider = null; // Kept for consistency, not directly used in core logic
//    private Quaternion targetRotation; // The rotation we are trying to achieve
//    private Vector3 targetPosition; // The position we are trying to achieve

//    private bool isPlayingAudio = false;
//    private float audioTimer = 1.0f;

//    void Start()
//    {
//        heatmapSource = GetComponent<DrawOn3DTexture>();

//        GameObject audioObject = new GameObject("AudioRecorder");
//        audioSource = audioObject.AddComponent<AudioSource>();
//        DontDestroyOnLoad(audioObject);

//        promptObject.SetActive(true);
//        promptInitialPosition = promptObject.transform.localPosition;
//        //promptObject.GetComponent<TextMeshPro>().SetText("Say 'Start'");
//        promptObject.GetComponent<TextMeshPro>().SetText("「Enter」キーを押してください");

//        // audio prompt
//        if (objectWithCollider == null)
//        {
//            Collider coll = GetComponent<Collider>();
//            if (coll == null)
//            {
//                coll = GetComponentInChildren<Collider>();
//            }
//            if (coll != null)
//            {
//                objectWithCollider = coll.gameObject;
//            }
//            else
//            {
//                Debug.LogWarning("FaceUser: No collider found on this GameObject or its children.");
//            }
//        }

//        targetRotation = promptObject.transform.rotation;
//        targetPosition = promptObject.transform.position; // Initialize target position
//    }

//    void Update()
//    {
//        /* CHECK IF RECORDING STARTED */
//        if (!isRecording || QNAModelController.currentModel == null) return;

//        /* GET GAZED OBJECT */
//        var eyeTarget = EyeTrackingTarget.LookedAtEyeTarget;
//        var gazedObject = eyeTarget != null ? eyeTarget.gameObject : null;

//        //string newGazedVoxelID = null;
//        //Vector3 localHitPosition = Vector3.zero;
//        //if (gazedObject != null && gazedObject == QNAModelController.currentModel)
//        //{
//        //    var eyeProvider = CoreServices.InputSystem?.EyeGazeProvider;
//        //    if (eyeProvider != null && eyeProvider.IsEyeTrackingEnabledAndValid)
//        //    {
//        //        // Convert world hit position to local hit position relative to the pottery model
//        //        localHitPosition = gazedObject.transform.InverseTransformPoint(eyeProvider.HitPosition);

//        //        // Determine the voxel ID based on local hit position
//        //        newGazedVoxelID = GetVoxelIDFromLocalPosition(localHitPosition, gazedObject.transform.localScale, localBounds);
//        //        Debug.Log(newGazedVoxelID);
//        //    }
//        //}

//        //// Check if the gazed voxel has changed
//        //if (newGazedVoxelID != currentGazedVoxelID)
//        //{
//        //    // Reset timer and update current gazed voxel
//        //    currentGazedVoxelID = newGazedVoxelID;
//        //    currentVoxelGazeTimer = 0f;
//        //}
//        //else if (currentGazedVoxelID != null && !isQuestionnaireActive && !savedGaze)
//        //{
//        //    // Still looking at the same voxel, increment timer
//        //    currentVoxelGazeTimer += Time.deltaTime;

//        //    // Update individual voxel dwell time (optional, for separate analysis)
//        //    if (!voxelDwellTimes.ContainsKey(currentGazedVoxelID))
//        //    {
//        //        voxelDwellTimes[currentGazedVoxelID] = 0f;
//        //    }
//        //    voxelDwellTimes[currentGazedVoxelID] += Time.deltaTime;

//        //    // Check if gaze duration threshold is met for this voxel AND it hasn't triggered too many times
//        //    if (currentVoxelGazeTimer >= requiredVoxelGazeDuration)
//        //    {
//        //        int currentTriggerCount = voxelTriggerCounts.ContainsKey(currentGazedVoxelID) ? voxelTriggerCounts[currentGazedVoxelID] : 0;

//        //        if (currentTriggerCount < maxGazeEventsPerVoxel)
//        //        {
//        //            Debug.Log($"User gazed at voxel {currentGazedVoxelID} for over {requiredVoxelGazeDuration} seconds.");
//        //            voxelTriggerPosition = UnapplyUnityTransforms(localHitPosition, QNAModelController.currentModel.transform.eulerAngles);
//        //            ShowQuestionnaire(QNAModelController.currentModel.name, currentGazedVoxelID);
//        //            isQuestionnaireActive = true;
//        //            voxelTriggerCounts[currentGazedVoxelID] = currentTriggerCount + 1; // Increment trigger count
//        //            // Resetting currentVoxelGazeTimer here to avoid re-triggering immediately
//        //            currentVoxelGazeTimer = 0f;
//        //            // Temporarily disable gaze input or block the view to ensure the user focuses on the questionnaire.
//        //            if (isLiveHeatmap)
//        //            {
//        //                GetComponent<DrawOn3DTexture>().ToggleLiveHeatmap(false);
//        //            }
//        //        }
//        //    }
//        //}

//        //if (!isQuestionnaireActive)
//        //{
//            /* RECORD GAZE DATA */
//            if (timer > recordVoiceDuration)
//            {
//                timer -= Time.deltaTime;

//                // Pass the gazed voxel ID to RecordGazeData
//                //RecordGazeData(gazedObject, newGazedVoxelID);
//                RecordGazeData(gazedObject);

//                promptObject.GetComponent<TextMeshPro>().SetText($"VIEWING TIME: {(timer - recordVoiceDuration):F1}");

//                CheckKeyboardInput();
//            }
//            /* RECORD VOICE DATA */
//            else if (timer > 0)
//            {
//                timer -= Time.deltaTime;

//                promptObject.GetComponent<TextMeshPro>().SetText(question + $"TIME: {timer:F1}");
//                Vector3 cameraPosition = CameraCache.Main.transform.position;
//                Vector3 currentPosition = promptObject.transform.position;
//                Quaternion currentRotation = promptObject.transform.rotation;
//                Transform mainCameraTransform = CameraCache.Main.transform;
//                Vector3 directionToCamera = -(cameraPosition - currentPosition + rotationDisplacement).normalized;
//                targetRotation = Quaternion.LookRotation(directionToCamera);

//                float rotationStep = rotationSpeed * Time.deltaTime;
//                promptObject.transform.rotation = Quaternion.Slerp(currentRotation, targetRotation, rotationStep);

//                // Snap rotation if close enough
//                if (Quaternion.Angle(currentRotation, targetRotation) < rotationThresholdDegrees)
//                {
//                    promptObject.transform.rotation = targetRotation;
//                }
//                targetPosition = cameraPosition + mainCameraTransform.forward * followDistance + mainCameraTransform.right * horizontalDisplacement + mainCameraTransform.up * verticalDisplacement;

//                // Smoothly move towards the target position
//                float moveStep = moveSpeed * Time.deltaTime;
//                promptObject.transform.position = Vector3.Lerp(currentPosition, targetPosition, moveStep);

//                if (!savedGaze)
//                {
//                    GetComponent<DrawOn3DTexture>().ToggleLiveHeatmap(false);
//                    QNAModelController.StopSineWave();
//                    QNAModelController.ToggleQnaPrompt();

//                    /* Clear recording variables */
//                    chunkIndex = 0; // Reset chunk index
//                    savedFiles.Clear(); // Clear file list

//                    /* Start voice recording */
//                    StartAudioRecording();

//                    savedGaze = true;
//                }

//                /* RESTART AUDIO RECORDING EVERY 60 SECONDS */
//                //if (isRecordingAudio && Time.time - chunkStartTime >= 59.9f)
//                //{
//                //    StopAudioRecording(); // Save current chunk
//                //    StartCoroutine(RestartAudioRecordingAfterDelay());
//                //}
//            }
//            else
//            {
//                //promptObject.GetComponent<TextMeshPro>().SetText("Say 'Next'");
//                promptObject.GetComponent<TextMeshPro>().SetText("");

//                SaveAllData();
//                StopAudioRecording();
//                SaveFileList();
//                SetIsRecording(false);

//                QNAModelController.ToggleRecorded();
//            }
//        //}
//    }

//    private string[] answerChoices = new string[]
//    {
//        "面白い・気になる形だ",
//        "不思議・意味不明",
//        "何も感じない",
//        "不気味・不安・怖い",
//        "美しい・芸術的だ",
//    };

//    private void CheckKeyboardInput()
//    {
//        if (Input.GetKey(KeyCode.Alpha4) || Input.GetKey(KeyCode.Keypad4))
//        {
//            SelectAnswerByNumber(1);
//        }
//        else if (Input.GetKey(KeyCode.Alpha6) || Input.GetKey(KeyCode.Keypad6))
//        {
//            SelectAnswerByNumber(2);
//        }
//        else if (Input.GetKey(KeyCode.Alpha8) || Input.GetKey(KeyCode.Keypad8))
//        {
//            SelectAnswerByNumber(3);
//        }
//        else if (Input.GetKey(KeyCode.Alpha2) || Input.GetKey(KeyCode.Keypad2))
//        {
//            SelectAnswerByNumber(4);
//        }
//        else if (Input.GetKey(KeyCode.Alpha5) || Input.GetKey(KeyCode.Keypad5))
//        {
//            SelectAnswerByNumber(5);
//        } else
//        {
//            if (!isPlayingAudio)
//            {
//                QNAModelController.StopSineWave();
//            } else
//            {
//                audioTimer -= Time.deltaTime;

//                if (audioTimer < 0)
//                {
//                    isPlayingAudio = false;
//                    audioTimer = 1.0f;
//                }
//            }
//        }
//    }

//    private void SelectAnswerByNumber(int number)
//    {
//        if (number > 0 && number <= answerChoices.Length)
//        {
//            if (!isPlayingAudio)
//            {
//                StartCoroutine(QNAModelController.PlayMajorFifthInterval(1.0f));
//                isPlayingAudio = true;
//            }
//            else
//            {
//                audioTimer -= Time.deltaTime;

//                if (audioTimer < 0)
//                {
//                    isPlayingAudio = false;
//                    audioTimer = 1.0f;
//                }
//            }
//                OnQuestionnaireAnswered(answerChoices[number - 1]);
//        }
//        else
//        {
//            Debug.LogWarning($"Attempted to select an invalid answer number: {number}");
//        }
//    }

//    //private string GetVoxelIDFromLocalPosition(Vector3 localPos, Vector3 modelScale, Bounds modelLocalBounds)
//    //{
//    //    // Adjust for model scale if your localBounds are pre-scaled or if you want voxel grid to adapt
//    //    // localPos.x /= modelScale.x;
//    //    // localPos.y /= modelScale.y;
//    //    // localPos.z /= modelScale.z;

//    //    // Calculate offset from the min bound of the model's local bounds
//    //    Vector3 offset = localPos - modelLocalBounds.min;

//    //    // Determine voxel indices
//    //    int x = Mathf.FloorToInt(offset.x / voxelSize);
//    //    int y = Mathf.FloorToInt(offset.y / voxelSize);
//    //    int z = Mathf.FloorToInt(offset.z / voxelSize);

//    //    // Clamp indices to be within a reasonable range based on expected model size/ voxel grid
//    //    // This prevents creating huge sparse dictionaries if gaze hits far off points due to raycast issues
//    //    int maxDimX = Mathf.CeilToInt(modelLocalBounds.size.x / voxelSize);
//    //    int maxDimY = Mathf.CeilToInt(modelLocalBounds.size.y / voxelSize);
//    //    int maxDimZ = Mathf.CeilToInt(modelLocalBounds.size.z / voxelSize);

//    //    x = Mathf.Clamp(x, 0, maxDimX - 1);
//    //    y = Mathf.Clamp(y, 0, maxDimY - 1);
//    //    z = Mathf.Clamp(z, 0, maxDimZ - 1);

//    //    return $"{x}_{y}_{z}";
//    //}

//    public void SetIsRecording(bool val)
//    {
//        /* Reset & clear variables (isRecording, timer, startingTime, savedGaze) */
//        isRecording = val;
//        timer = recordGazeDuration + recordVoiceDuration; // reset timer
//        startingTime = Time.unscaledTimeAsDouble; // record starting time
//        savedGaze = !val; // reset recording state
//        //isLiveHeatmap = GetComponent<DrawOn3DTexture>().enabled;

//        /* Toggle viewBlocker */
//        viewBlocker.SetActive(!val);

//        //currentGazedVoxelID = null;
//        //currentVoxelGazeTimer = 0f;
//        //isQuestionnaireActive = false;
//        //voxelDwellTimes.Clear();
//        //voxelTriggerCounts.Clear(); // Clear trigger counts for new session

//        if (val && QNAModelController.currentModel != null)
//        {
//            /* Set session data */
//            currentSession.selectedObjectName = QNAModelController.currentModel.name;

//            /* Set gaze object information */
//            targetRenderer = QNAModelController.currentModel.GetComponent<Renderer>();
//            // Use currentModel.GetComponent<MeshFilter>().sharedMesh.bounds if the renderer bounds aren't accurate
//            localBounds = targetRenderer.localBounds;

//            /* Initialize point cloud data header */
//            pc_sb = new StringBuilder();
//            pc_sb.AppendLine("x,y,z,timestamp,gazedVoxelID");

//            /* Create data directory */
//            saveDir = Path.Combine(Application.persistentDataPath, sessionPath, currentSession.selectedObjectName);
//            if (!Directory.Exists(saveDir))
//            {
//                Directory.CreateDirectory(saveDir);
//            }
//        }
//        else
//        {
//            /* Show prompt “Say ‘Next’” */
//            //promptObject.GetComponent<TextMeshPro>().SetText("Say 'Next'");
//            promptObject.GetComponent<TextMeshPro>().SetText("");
//            promptObject.transform.localPosition = promptInitialPosition;
//        }

//        // start audio recording here
//        if (val && !isRecordingAudio)
//        {
//            ///* Clear recording variables */
//            //chunkIndex = 0; // Reset chunk index
//            //savedFiles.Clear(); // Clear file list

//            ///* Start voice recording */
//            //StartAudioRecording();
//        }
//        else if (!val && isRecordingAudio)
//        {
//            /* Stop voice recording */
//            StopAudioRecording();

//            /* Export voice recording data */
//            SaveFileList();
//        }
//    }

//    // Modified RecordGazeData to accept voxel ID
//    //private void RecordGazeData(GameObject target, string gazedVoxelID)
//    private void RecordGazeData(GameObject target)
//    {
//        /* GET EYE GAZE PROVIDER */
//        var eyeProvider = CoreServices.InputSystem?.EyeGazeProvider;
//        if (eyeProvider == null) return;

//        /* CREATE NEW GAZE DATA */
//        var gaze = new GazeData
//        {
//            timestamp = Time.unscaledTimeAsDouble - startingTime,
//            headPosition = CameraCache.Main.transform.position,
//            headForward = CameraCache.Main.transform.forward,
//            eyeOrigin = eyeProvider.GazeOrigin,
//            eyeDirection = eyeProvider.GazeDirection,
//            hitPosition = eyeProvider.IsEyeTrackingEnabledAndValid ? eyeProvider.HitPosition : Vector3.zero,
//            targetName = target != null ? target.name : "null",
//            //gazedVoxelID = gazedVoxelID
//        };

//        /* CHECK IF GAZE HIT ON SELECTED MODEL */
//        if (target != null && target.name == currentSession.selectedObjectName)
//        {
//            /* CONVERT GAZE HIT FROM WORLD COORDINATE TO LOCAL COORDINATE */
//            gaze.localHitPosition = target.transform.InverseTransformPoint(gaze.hitPosition);
//            Vector3 pos = gaze.localHitPosition;

//            /* CHECK IF GAZE HIT IS ON SELECTED MODEL */
//            if (localBounds.Contains(pos) && gaze.targetName == target.name && gaze.targetName != "null")
//            {
//                /* REVERT TRANSFORMS WHEN IMPORTING MODEL */
//                pos = UnapplyUnityTransforms(pos, target.transform.eulerAngles);

//                localHitPosition = pos;

//                /* ADD GAZE DATA */
//                pc_sb.AppendLine($"{pos.x:F6},{pos.y:F6},{pos.z:F6},{gaze.timestamp:F6}");
//            }
//        }
//        else
//        {
//            gaze.localHitPosition = Vector3.zero;
//        }
//    }

//    /* RESTART AUDIO RECORDING EVERY 60 SECONDS */
//    //private IEnumerator RestartAudioRecordingAfterDelay()
//    //{
//    //    yield return new WaitForSeconds(0.1f); // Small delay
//    //    StartAudioRecording();
//    //}

//    private void StartAudioRecording()
//    {
//        /* RECORD AUDIO FOR 60 SECONDS */
//        recordedAudio = Microphone.Start(null, false, 60, audioSampleRate); // loop = false
//        isRecordingAudio = true;
//        chunkStartTime = Time.time;
//        Debug.Log("Started audio recording (60s chunk).");
//    }

//    private void StopAudioRecording()
//    {
//        if (!isRecordingAudio) return;
//        Microphone.End(null);

//        /* SAVE AUDIO CLIP */
//        SaveAudioData();
//        isRecordingAudio = false;
//        Debug.Log("Stopped audio recording and saved final chunk.");
//    }

//    private byte[] ConvertAudioClipToWAV(AudioClip clip)
//    {
//        if (clip == null || clip.samples == 0) return null;

//        int channels = clip.channels;
//        int sampleCount = clip.samples;
//        int bitsPerSample = 16;
//        int byteRate = clip.frequency * channels * (bitsPerSample / 8);
//        int dataSize = sampleCount * channels * (bitsPerSample / 8);

//        // Create WAV header
//        byte[] header = new byte[44];
//        Buffer.BlockCopy(Encoding.UTF8.GetBytes("RIFF"), 0, header, 0, 4);
//        BitConverter.GetBytes((int)(dataSize + 36)).CopyTo(header, 4);
//        Buffer.BlockCopy(Encoding.UTF8.GetBytes("WAVE"), 0, header, 8, 4);
//        Buffer.BlockCopy(Encoding.UTF8.GetBytes("fmt "), 0, header, 12, 4);
//        BitConverter.GetBytes((int)16).CopyTo(header, 16);
//        BitConverter.GetBytes((short)1).CopyTo(header, 20);
//        BitConverter.GetBytes((short)channels).CopyTo(header, 22);
//        BitConverter.GetBytes(clip.frequency).CopyTo(header, 24);
//        BitConverter.GetBytes(byteRate).CopyTo(header, 28);
//        BitConverter.GetBytes((short)(channels * (bitsPerSample / 8))).CopyTo(header, 32);
//        BitConverter.GetBytes((short)bitsPerSample).CopyTo(header, 34);
//        Buffer.BlockCopy(Encoding.UTF8.GetBytes("data"), 0, header, 36, 4);
//        BitConverter.GetBytes((int)dataSize).CopyTo(header, 40);

//        // Extract samples and convert to short PCM
//        float[] samples = new float[sampleCount * channels];
//        clip.GetData(samples, 0);
//        byte[] data = new byte[dataSize];

//        for (int i = 0; i < samples.Length; i++)
//        {
//            short val = (short)Mathf.Clamp(samples[i] * short.MaxValue, short.MinValue, short.MaxValue);
//            Buffer.BlockCopy(BitConverter.GetBytes(val), 0, data, i * 2, 2);
//        }

//        byte[] wavBytes = new byte[header.Length + data.Length];
//        Buffer.BlockCopy(header, 0, wavBytes, 0, header.Length);
//        Buffer.BlockCopy(data, 0, wavBytes, header.Length, data.Length);
//        return wavBytes;
//    }

//    private void SaveAudioData()
//    {
//        if (recordedAudio == null)
//        {
//            return;
//        }

//        byte[] wavData = ConvertAudioClipToWAV(recordedAudio);
//        string audioFileName = $"session_audio_{chunkIndex}.wav";
//        string fullPath = Path.Combine(saveDir, audioFileName);
//        File.WriteAllBytes(fullPath, wavData);
//        Debug.Log($"Audio chunk saved: {fullPath}");
//        savedFiles.Add(audioFileName);
//        chunkIndex++;
//    }

//    private void SaveFileList()
//    {
//        // IN CMD run: ffmpeg -f concat -safe 0 -i filelist.txt -c copy output.wav
//        StringBuilder sb = new StringBuilder();
//        foreach (string file in savedFiles)
//        {
//            sb.AppendLine("file '" + file + "'");
//        }

//        string listFilePath = Path.Combine(saveDir, "filelist.txt");
//        File.WriteAllText(listFilePath, sb.ToString());
//        Debug.Log("File list saved to: " + listFilePath);
//    }

//    private Vector3 UnapplyUnityTransforms(Vector3 originalVector, Vector3 anglesInDegrees)
//    {
//        /* REVERSE ANY ROTATION ON MODEL */
//        Quaternion xRotation = Quaternion.AngleAxis(anglesInDegrees.x, Vector3.right);
//        Quaternion yRotation = Quaternion.AngleAxis(anglesInDegrees.y, Vector3.up);
//        Quaternion zRotation = Quaternion.AngleAxis(anglesInDegrees.z, Vector3.forward);

//        Vector3 rotatedVector = xRotation * originalVector;
//        rotatedVector = yRotation * rotatedVector;
//        rotatedVector = zRotation * rotatedVector;

//        /* NEGATE X TO FLIP THE X-AXIS */
//        return new Vector3(-rotatedVector.x, rotatedVector.y, rotatedVector.z);
//    }

//    public void ResetAll()
//    {
//        if (isRecording)
//        {
//            SetIsRecording(false);
//        }
//        currentSession = new QNAModelGazeRecorder.SessionData();
//        promptObject.GetComponent<TextMeshPro>().SetText("「Enter」キーを押してください");

//        if (heatmapSource != null)
//        {
//            heatmapSource.ClearDrawing();
//        }
//    }

//    public void SaveAllData()
//    {
//        ExportPointCloud(QNAModelController.currentModel);
//        Export3DModel(QNAModelController.currentModel);
//        SaveQuestionnaireAnswers(); // Save the answers
//        Debug.Log("SAVED DATA AT: " + saveDir);
//    }

//    public void ExportPointCloud(GameObject target)
//    {
//        // Modify the header if you change the format
//        File.WriteAllText(Path.Combine(saveDir, "pointcloud.csv"), pc_sb.ToString());
//    }

//    public void Export3DModel(GameObject target)
//    {
//        Mesh mesh = meshFilter.sharedMesh;
//        string objContent = MeshToString(mesh, target);
//        File.WriteAllText(Path.Combine(saveDir, "model.obj"), objContent);
//    }

//    private string MeshToString(Mesh mesh, GameObject target)
//    {
//        StringBuilder sb = new StringBuilder();
//        sb.Append("# Exported Gaze Object\n");

//        Mesh tempMesh = Instantiate(mesh);

//        Vector3[] transVertices = new Vector3[tempMesh.vertexCount];
//        for (int i = 0; i < tempMesh.vertices.Length; i++)
//        {
//            transVertices[i] = UnapplyUnityTransforms(tempMesh.vertices[i], target.transform.eulerAngles);
//        }
//        tempMesh.vertices = transVertices;

//        tempMesh.RecalculateNormals();

//        foreach (Vector3 vertex in tempMesh.vertices)
//        {
//            sb.Append($"v {vertex.x:F6} {vertex.y:F6} {vertex.z:F6}\n");
//        }

//        foreach (Vector3 normal in tempMesh.normals)
//        {
//            sb.Append($"vn {normal.x:F6} {normal.y:F6} {normal.z:F6}\n");
//        }

//        foreach (Vector2 uv in tempMesh.uv)
//        {
//            sb.Append($"vt {uv.x:F6} {uv.y:F6}\n");
//        }

//        // Write out faces (with winding order flipped)
//        for (int i = 0; i < tempMesh.subMeshCount; i++)
//        {
//            int[] triangles = tempMesh.GetTriangles(i);
//            for (int j = 0; j < triangles.Length; j += 3)
//            {
//                // Swap first and third index to reverse triangle winding
//                int temp = triangles[j];
//                triangles[j] = triangles[j + 2];
//                triangles[j + 2] = temp;

//                // Output face
//                sb.Append($"f {triangles[j] + 1}/{triangles[j] + 1}/{triangles[j] + 1} " +
//                            $"{triangles[j + 1] + 1}/{triangles[j + 1] + 1}/{triangles[j + 1] + 1} " +
//                            $"{triangles[j + 2] + 1}/{triangles[j + 2] + 1}/{triangles[j + 2] + 1}\n");
//            }
//        }

//        return sb.ToString();
//    }

//    //private void ShowQuestionnaire(string gazedObjectName, string gazedVoxelID)
//    //{
//    //    if (questionPopupPrefab == null)
//    //    {
//    //        Debug.LogError("Questionnaire popup prefab is not assigned!");
//    //        return;
//    //    }

//    //    GameObject popupInstance = Instantiate(questionPopupPrefab);
//    //    popupInstance.SetActive(true);
//    //    popupInstance.transform.position = CameraCache.Main.transform.position + CameraCache.Main.transform.forward * 0.9f; // x meters in front
//    //    popupInstance.transform.forward = CameraCache.Main.transform.forward; // Orient towards the user

//    //    QuestionnaireController questionnaireController = popupInstance.GetComponent<QuestionnaireController>();
//    //    if (questionnaireController != null)
//    //    {
//    //        questionnaireController.InitializeQuestionnaire(gazedObjectName, gazedVoxelID, OnQuestionnaireAnswered);
//    //    }
//    //    else
//    //    {
//    //        Debug.LogError("QuestionnaireController script not found on the popup prefab!");
//    //    }
//    //}

//    //private void OnQuestionnaireAnswered(string gazedObjectName, string gazedVoxelID, string selectedAnswer)
//    //{
//    //    Debug.Log($"User answered for {gazedObjectName}, Voxel {gazedVoxelID}: {selectedAnswer}");

//    //    currentSession.questionnaireAnswers.Add(new QuestionnaireAnswer
//    //    {
//    //        timestamp = Time.unscaledTimeAsDouble - startingTime,
//    //        gazedObjectName = gazedObjectName,
//    //        gazedVoxelID = gazedVoxelID, // Store the voxel ID
//    //        answer = selectedAnswer,
//    //        voxelPosition = voxelTriggerPosition
//    //    });

//    //    isQuestionnaireActive = false;

//    //    if (isLiveHeatmap)
//    //    {
//    //        GetComponent<DrawOn3DTexture>().ToggleLiveHeatmap(true);
//    //    }
//    //}

//    private void OnQuestionnaireAnswered(string selectedAnswer)
//    {
//        currentSession.questionnaireAnswers.Add(new QuestionnaireAnswer
//        {
//            timestamp = Time.unscaledTimeAsDouble - startingTime,
//            answer = selectedAnswer,
//            estimatedGamePosition = localHitPosition
//        });

//        //isQuestionnaireActive = false;

//        //if (isLiveHeatmap)
//        //{
//        //    GetComponent<DrawOn3DTexture>().ToggleLiveHeatmap(true);
//        //}
//    }

//    private void SaveQuestionnaireAnswers()
//    {
//        if (currentSession.questionnaireAnswers.Count == 0) return;

//        StringBuilder qa_sb = new StringBuilder();
//        qa_sb.AppendLine("estX,estY,estZ,answer,timestamp");

//        foreach (var qa in currentSession.questionnaireAnswers)
//        {
//            qa_sb.AppendLine($"{qa.estimatedGamePosition.x:F6},{qa.estimatedGamePosition.y:F6},{qa.estimatedGamePosition.z:F6},{qa.answer},{qa.timestamp:F6}");
//        }

//        File.WriteAllText(Path.Combine(saveDir, "qa.csv"), qa_sb.ToString());
//    }
//}
#endregion