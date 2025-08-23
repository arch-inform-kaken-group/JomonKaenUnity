using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.SampleGazeData;
using Microsoft.MixedReality.Toolkit.SpatialAwareness;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Utilities;
using static QNAModelGazeRecorder;
using System.IO;
using System.Text;

/// <summary>
/// Main control script for the experiment, functions include
/// 1) Selecting & loading model groups
/// 2) Stop & Start the recording sequence of individual pottery & dogu by calling their [PREFIX]ModelGazeRecorder.cs script
/// 3) Play sound during keypad input / data collection
/// 4) Contains functions for admin controls such as selecting next / previous pottery, resetting the current pottery, enable / disable live heatmap view
/// 
/// 実験の主要制御スクリプトです。以下の機能を含みます：
/// 1) モデルグループの選択とロード
/// 2) 個々の土器と土偶に対する記録シーケンスの停止と開始（それぞれの [PREFIX]ModelGazeRecorder.cs スクリプトを呼び出すことで）
/// 3) キーパッド入力時／データ収集時のサウンド再生
/// 4) 次／前の土器の選択、現在の土器のリセット、ライブヒートマップ表示の有効化／無効化など、管理者コントロール用の関数を含む
/// </summary>
public class QNAModelController : MonoBehaviour
{
    [SerializeField]
    private List<GameObject> groups;

    [SerializeField]
    private GameObject promptObject;

    [SerializeField]
    private GameObject groupPromptObject;

    [SerializeField] private GameObject adminOnUI;
    [SerializeField] private GameObject adminOffUI;

    [SerializeField] private GameObject startButton;
    [SerializeField] private GameObject QNAPrompt;
    [SerializeField] private static GameObject qnaPrompt;

    [SerializeField] private GameObject languageQNA;
    private GameObject popupInstance;
    private bool isAskingLanguage = false;

    [Header("Marker Spawning Settings")]
    [Tooltip("An array of 3D marker prefabs to be spawned. Assign your marker objects here in the Inspector.")]
    [SerializeField]
    public GameObject[] markerPrefabs;
    static public GameObject[] statMarkerPrefab;

    private List<GameObject> models = new List<GameObject>();
    private int currentModelIndex = 0;
    private Vector3 previousModelPosition = Vector3.zero;
    public static GameObject currentModel;

    private string sessionPath;
    private GameObject group;

    // recording state
    private bool admin = false;
    public static bool recorded = false;

    private int groupIndex = 0;

    //// Change this mapping for each deployment according to the group
    //// 各デプロイメントにおいて、グループに応じてこのマッピングを変更してください。
    //private Dictionary<int, string> groupTextMap = new Dictionary<int, string> {
    //    {0, "15" },
    //    {1, "16" },
    //    {2, "17" },
    //};

    void Awake() // Use Awake to get component reference before Start
    {
        qnaPrompt = QNAPrompt;
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = true;

        statMarkerPrefab = markerPrefabs;
    }

    void Start()
    {
        sessionPath = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");

        for (int i = 0; i < groups.Count; i++)
        {
            models = groups[i].GetComponent<GroupItems>().GetModels();
            for (int j = 0; j < models.Count; j++)
            {
                QNAModelGazeRecorder modelRecorder = models[j].GetComponent<QNAModelGazeRecorder>();
                if (modelRecorder != null)
                {
                    models[j].GetComponent<QNAModelGazeRecorder>().sessionPath = sessionPath;
                    models[j].GetComponent<EyeTrackingTarget>().enabled = false;
                    models[j].SetActive(false);
                }                
            }
        }

        DisableAllLiveHeatmap();

        promptObject.SetActive(false);
        qnaPrompt.SetActive(true);

        group = groups[0];
        models = group.GetComponent<GroupItems>().GetModels();
        groupPromptObject.GetComponent<TextMeshPro>().SetText(group.name);

        for (int i = 0; i < models.Count; i++) 
        {
            models[i].transform.parent.gameObject.SetActive(true);
        }

        LoadModel();

        ShowQuestionnaire();
    }

    void Update()
    {
        if (recorded)
        {
            LoadNext();
        }

        if (isAskingLanguage)
        {
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKey(KeyCode.Keypad4))
            {
                OnQuestionnaireAnswered("ENGLISH");
            } 
            else if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKey(KeyCode.Keypad5))
            {
                OnQuestionnaireAnswered("MALAY");
            }
            else if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKey(KeyCode.Keypad6))
            {
                OnQuestionnaireAnswered("MANDARIN");
            }
            else if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKey(KeyCode.Keypad8))
            {
                OnQuestionnaireAnswered("TAMIL");
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKey(KeyCode.Keypad2))
            {
                OnQuestionnaireAnswered("OTHER");
            }
        }

        if (currentModelIndex == 0 && !currentModel.GetComponent<QNAModelGazeRecorder>().isRecording)
        {
            groupPromptObject.SetActive(true);
            if ((Input.GetKey(KeyCode.Alpha7) || Input.GetKey(KeyCode.Keypad7)) && (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9)))
            {
                groupIndex++;
                if (groupIndex >= groups.Count())
                {
                    groupIndex = 0;
                }
                SelectGroup(groupIndex);
            }

            if ((Input.GetKey(KeyCode.Alpha1) || Input.GetKey(KeyCode.Keypad1)) && (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)))
            {
                groupIndex--;
                if (groupIndex < 0)
                {
                    groupIndex = groups.Count() - 1;
                }
                SelectGroup(groupIndex);
            }
        }
    }

    public void StartRecording()
    {
        if (!currentModel.GetComponent<QNAModelGazeRecorder>().isRecording && !recorded && !isAskingLanguage)
        {
            groupPromptObject.SetActive(false);
            qnaPrompt.SetActive(true);
            startButton.SetActive(false);
            currentModel.GetComponent<QNAModelGazeRecorder>().SetIsRecording(true);
            currentModel.GetComponent<EyeTrackingTarget>().enabled = true;
        }
    }

    public void StopRecording()
    {
        if (currentModel.GetComponent<QNAModelGazeRecorder>().isRecording)
        {
            currentModel.GetComponent<QNAModelGazeRecorder>().SetIsRecording(false);
            currentModel.GetComponent<QNAModelGazeRecorder>().SaveAllData();
            currentModel.GetComponent<EyeTrackingTarget>().enabled = false;
        }
    }

    #region Model Manipulation
    public void LoadModel()
    {
        qnaPrompt.SetActive(true);

        // Reset previous model position and rotation if there was a previous model
        if (previousModelPosition != Vector3.zero)
        {
            currentModel.transform.parent.SetPositionAndRotation(previousModelPosition, new Quaternion());
            StopRecording();
            currentModel.SetActive(false);
        }

        // Select the next model
        currentModel = models[currentModelIndex];
        currentModel.SetActive(true);
        currentModel.GetComponent<QNAModelGazeRecorder>().ResetAll();

        // Record the original transform
        previousModelPosition = currentModel.transform.parent.position;

        // Move the model to the viewing area
        currentModel.transform.parent.position = transform.position;

        recorded = false;

        startButton.SetActive(true);

        if (admin)
        {
            ToggleAdminMode();
        }
    }

    public void LoadPrevious()
    {
        if (((!currentModel.GetComponent<QNAModelGazeRecorder>().isRecording && recorded) || admin) && !isAskingLanguage)
        {
            if (currentModelIndex == 0)
            {
                currentModelIndex = 0;
                StopRecording();
            }
            else
            {
                currentModelIndex--;
                LoadModel();
            }

            Debug.Log("Loading " + models[currentModelIndex].name);
        }
    }

    public void LoadNext()
    {
        if (((!currentModel.GetComponent<QNAModelGazeRecorder>().isRecording && recorded) || admin) && !isAskingLanguage)
        {
            if (currentModelIndex == models.Count - 1)
            {
                promptObject.SetActive(true);
                StopRecording();
            }
            else
            {
                currentModelIndex++;
                LoadModel();
            }

            recorded = false;
            Debug.Log("Loading " + models[currentModelIndex].name);
        }
    }
    #endregion

    #region Group Manipulation
    public void SelectGroup(int groupNumber)
    {
        for (int j = 0; j < models.Count(); j++)
        {
            models[j].transform.parent.gameObject.SetActive(false);
            QNAModelGazeRecorder recorder = models[j].GetComponent<QNAModelGazeRecorder>();
            recorder.ResetAll();
        }

        group = groups[groupNumber];
        models = group.GetComponent<GroupItems>().GetModels();

        sessionPath = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");
        for (int j = 0; j < models.Count(); j++)
        {
            models[j].transform.parent.gameObject.SetActive(true);
            QNAModelGazeRecorder recorder = models[j].GetComponent<QNAModelGazeRecorder>();
            recorder.sessionPath = sessionPath;
            recorder.ResetAll();
        }

        promptObject.SetActive(false);
        groupPromptObject.GetComponent<TextMeshPro>().SetText(group.name);

        groupIndex = groupNumber;

        currentModelIndex = 0;
        LoadModel();

        if (popupInstance != null)
        {
            Destroy(popupInstance.gameObject);
            ShowQuestionnaire();
        } else
        {
            ShowQuestionnaire();
        }
    }

    public List<GameObject> GetGroups()
    {
        return groups;
    }
    #endregion

    #region Admin Panel Toggles
    public void DisableAllLiveHeatmap()
    {
        for (int i = 0; i < groups.Count; i++)
        {
            List<GameObject> m = groups[i].GetComponent<GroupItems>().GetModels();
            for (int j = 0; j < m.Count(); j++)
            {
                m[j].GetComponent<DrawOn3DTexture>().ToggleLiveHeatmap(false);
                m[j].GetComponent<DrawOn3DTexture>().enabled = false;
            }
        }
    }

    public void EnableAllLiveHeatmap()
    {
        for (int i = 0; i < groups.Count; i++)
        {
            List<GameObject> m = groups[i].GetComponent<GroupItems>().GetModels();
            for (int j = 0; j < m.Count(); j++)
            {
                m[j].GetComponent<DrawOn3DTexture>().ToggleLiveHeatmap(true);
                m[j].GetComponent<DrawOn3DTexture>().enabled = true;
            }
        }
    }

    public void ToggleAdminMode()
    {
        admin = !admin;

        if (admin)
        {
            adminOnUI.SetActive(true);
            adminOffUI.SetActive(false);
        }
        else
        {
            adminOnUI.SetActive(false);
            adminOffUI.SetActive(true);
        }
    }

    public static void ToggleQnaPrompt()
    {
        qnaPrompt.SetActive(false);
    }

    public static void ToggleRecorded()
    {
        recorded = !recorded;
    }
    #endregion

    #region Generate Tone
    [Header("Base Tone Settings")]
    [Range(100, 5000)]
    public static float baseFrequency = 587.33f;
    [Range(0.1f, 1f)]
    public static float amplitude = 0.5f;

    [Header("Interval Settings")]
    [Range(0.1f, 2f)]
    public static float phaseShift = 0.5f;

    public static float sampleRate = 44100f;

    private static float phase1;
    private static float phase2;

    private static AudioSource audioSource;
    private static bool isPlayingSound = false;

    public static IEnumerator PlayMajorFifthInterval(float duration)
    {
        isPlayingSound = true;
        phase1 = 0f;
        phase2 = 0f;
        audioSource.Play();

        yield return new WaitForSeconds(duration);

        //StopSineWave();
    }

    public static void StopSineWave()
    {
        audioSource.Stop();
        isPlayingSound = false;
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (isPlayingSound)
        {
            float majorThirdFrequency = baseFrequency * (3f / 2f);

            for (int i = 0; i < data.Length; i += channels)
            {
                // Generate base sine wave sample
                float sample1 = amplitude * Mathf.Sin(phase1 * 2 * Mathf.PI);

                // Generate major third sine wave sample
                float sample2 = amplitude * Mathf.Sin(phase2 * 2 * Mathf.PI);

                // Mix the two samples (simple addition, can be normalized if desired)
                float mixedSample = (sample1 + sample2) * 0.5f;

                // Apply to all channels
                for (int channel = 0; channel < channels; channel++)
                {
                    data[i + channel] = mixedSample;
                }

                // Increment phases for both frequencies
                phase1 = (phase1 + (baseFrequency / sampleRate) * phaseShift) % 1f;
                phase2 = (phase2 + (majorThirdFrequency / sampleRate) * phaseShift) % 1f;
            }
        }
        else
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = 0f;
            }
        }
    }
    #endregion

    #region LANGUAGE QNA
    private void ShowQuestionnaire()
    {
        if (languageQNA == null)
        {
            Debug.LogError("Questionnaire popup prefab is not assigned!");
            return;
        }
        isAskingLanguage = true;
        popupInstance = Instantiate(languageQNA);
        popupInstance.SetActive(true);
        popupInstance.transform.position = CameraCache.Main.transform.position + CameraCache.Main.transform.forward * 0.4f; // x meters in front
        popupInstance.transform.forward = CameraCache.Main.transform.forward; // Orient towards the user
    }

    private void OnQuestionnaireAnswered(string selectedAnswer)
    {
        string saveDir = Path.Combine(Application.persistentDataPath, sessionPath);
        if (!Directory.Exists(saveDir))
        {
            Directory.CreateDirectory(saveDir);
        }
        StringBuilder language_sb = new StringBuilder();
        language_sb.AppendLine("language");
        language_sb.AppendLine(selectedAnswer);
        File.WriteAllText(Path.Combine(saveDir, "language.txt"), language_sb.ToString());
        isAskingLanguage = false;
        Destroy(popupInstance.gameObject);
    }
    #endregion
}
