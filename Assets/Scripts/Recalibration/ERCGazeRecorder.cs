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
using static UnityEngine.Random;

public class ERCGazeRecorder : MonoBehaviour
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

    [SerializeField]
    private int numTarget = 80;

    [SerializeField]
    private List<GameObject> targetList = new List<GameObject>();

    [SerializeField]
    private GameObject currentModel;

    [SerializeField]
    private GameObject button;

    private string sessionPath;
    private int numTargetAppeared;
    private double timeInterval;
    private float zSum;
    private float zNum;
    private int currentIndex;
    private GameObject currentTarget;

    public bool isRecording;

    private string saveDir;
    private double startingTime;
    private Renderer targetRenderer;
    private Bounds localBounds;
    private StringBuilder pc_sb = new StringBuilder();

    void Start()
    {
        // deactivate all points
        for (int i = 0; i < targetList.Count; i++)
        {
            targetList[i].SetActive(false);
        }
    }

    void Update()
    {
        if (!isRecording || currentModel == null) return;

        var eyeTarget = EyeTrackingTarget.LookedAtEyeTarget;
        var gazedObject = eyeTarget != null ? eyeTarget.gameObject : null;

        RecordGazeData(gazedObject);

        if (timeInterval < 0)
        {
            if (numTargetAppeared == numTarget)
            {
                currentTarget.SetActive(false);
                SetIsRecording(false);
                SaveAllData();
                button.SetActive(true);
            } else
            {
                timeInterval = Range(100, 151) / 100.0;
                currentTarget.SetActive(false);
                int nextIndex = Range(0, targetList.Count);
                while (currentIndex == nextIndex)
                {
                    nextIndex = Range(0, targetList.Count);
                }
                currentIndex = nextIndex;
                currentTarget = targetList[currentIndex];
                currentTarget.SetActive(true);
                numTargetAppeared++;
            }
        } else
        {
            timeInterval -= Time.deltaTime;
        }
    }

    public void SetIsRecording(bool val)
    {
        isRecording = val;
        startingTime = Time.unscaledTimeAsDouble;

        if (val && currentModel != null)
        {
            sessionPath = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + "_recalibration";
            numTargetAppeared = 1;
            timeInterval = Range(100, 151) / 100.0;

            saveDir = Path.Combine(Application.persistentDataPath, sessionPath, currentModel.name);

            targetRenderer = currentModel.GetComponent<Renderer>();
            localBounds = targetRenderer.localBounds;
            pc_sb = new StringBuilder();
            pc_sb.AppendLine("x,y,z,targetX,targetY,targetZ,eyeX,eyeY,eyeZ,timestamp");

            if (!Directory.Exists(saveDir))
            {
                Directory.CreateDirectory(saveDir);
            }

            currentIndex = Range(0, targetList.Count);
            currentTarget = targetList[currentIndex];
            currentTarget.SetActive(true);

            button.SetActive(false);
        }
    }

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

        if (target != null && target.name == currentModel.name)
        {
            Vector3 tarTrans = currentTarget.transform.position;
            gaze.localHitPosition = target.transform.InverseTransformPoint(gaze.hitPosition);
            Vector3 pos = gaze.localHitPosition;
            if (localBounds.Contains(pos) && gaze.targetName == target.name && gaze.targetName != "null")
            {
                pc_sb.AppendLine($"{pos.x:F3},{-pos.y:F3},{pos.z:F3},{tarTrans.x:F3},{-tarTrans.y:F3},{tarTrans.z:F3},{gaze.headPosition.x:F3},{-gaze.headPosition.y:F3},{gaze.headPosition.z:F3},{(gaze.timestamp - startingTime):F3}");
                zSum += pos.z;
                zNum += 1.0f;
            }
        }
        else
        {
            gaze.localHitPosition = Vector3.zero;
        }
    }

    public void ResetAll()
    {
        if (isRecording)
        {
            SetIsRecording(false);
        }
        StopAllCoroutines(); // Ensure any ongoing audio recording coroutines are stopped
    }

    public void SaveAllData()
    {
        ExportPointCloud(currentModel);
        SaveTargetCoordinates("target.csv");
        Debug.Log("SAVED DATA AT: " + saveDir);
    }

    public void ExportPointCloud(GameObject target)
    {
        File.WriteAllText(Path.Combine(saveDir, "pointcloud.csv"), pc_sb.ToString());
    }

    public void SaveTargetCoordinates(string fileName)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("x,y,z,scaleX,scaleY,scaleZ,targetName");
        foreach (GameObject target in targetList)
        {
            Vector3 pos = target.transform.localPosition;
            pos = new Vector3(pos.x, pos.y, pos.z + (zSum / zNum));
            sb.AppendLine($"{pos.x:F3},{-pos.y:F3},{pos.z:F3},{target.transform.localScale.x},{target.transform.localScale.y},{target.transform.localScale.z},{target.name}");
        }
        File.WriteAllText(Path.Combine(saveDir, fileName), sb.ToString());
    }
}