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
    private int numTarget = 60;

    [SerializeField]
    private bool multipleTarget = true;

    [SerializeField]
    private bool is3DObject = true;

    [SerializeField]
    private int numTargetPerFace = 10;

    private int currentFaceIndex = 0;

    [SerializeField]
    private List<GameObject> targetList = new List<GameObject>();

    [SerializeField]
    private List<int> targetFace3D = new List<int>();

    private List<List<GameObject>> targetList3D = new List<List<GameObject>>();

    [SerializeField]
    private GameObject currentModel;

    public string sessionPath;
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

        if (is3DObject)
        {
            List<GameObject> targetsTemp = new List<GameObject>();
            for (int i = 0; i < targetList.Count; i++)
            {
                if (targetFace3D.Contains(i))
                {
                    targetList3D.Add(targetsTemp);
                    targetsTemp = new List<GameObject>();
                } else
                {
                    targetsTemp.Add(targetList[i]);
                }
            }
            targetList3D.Add(targetsTemp);
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
            if (multipleTarget) 
            {
                if (numTargetAppeared == numTarget)
                {
                    currentTarget.SetActive(false);
                    SetIsRecording(false);
                    SaveAllData();

                    ERCGazeController.ToggleRecorded();
                }
                else
                {
                    timeInterval = Range(100, 151) / 100.0;
                    currentTarget.SetActive(false);
                    
                    if (is3DObject)
                    {
                        if (numTargetAppeared % numTargetPerFace == 0)
                        {
                            currentFaceIndex = (currentFaceIndex + 1) % targetList3D.Count;
                            Debug.Log(currentFaceIndex);
                        }

                        int nextIndex = Range(0, targetList3D[currentFaceIndex].Count);
                        while (currentIndex == nextIndex)
                        {
                            nextIndex = Range(0, targetList3D[currentFaceIndex].Count);
                        }
                        currentIndex = nextIndex;
                        currentTarget = targetList3D[currentFaceIndex][currentIndex];
                    } else
                    {
                        int nextIndex = Range(0, targetList.Count);
                        while (currentIndex == nextIndex)
                        {
                            nextIndex = Range(0, targetList.Count);
                        }
                        currentIndex = nextIndex;
                        currentTarget = targetList[currentIndex];
                    }

                    currentTarget.SetActive(true);
                    numTargetAppeared++;
                }
            } else
            {
                if (numTargetAppeared == numTarget)
                {
                    SetIsRecording(false);
                    SaveAllData();

                    ERCGazeController.ToggleRecorded();
                }

                timeInterval = Range(100, 151) / 100.0;
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
            //sessionPath = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + "_precision";
            numTargetAppeared = 1;
            timeInterval = Range(100, 151) / 100.0;

            saveDir = Path.Combine(Application.persistentDataPath, sessionPath, currentModel.name);

            targetRenderer = currentModel.GetComponent<Renderer>();
            localBounds = targetRenderer.localBounds;
            pc_sb = new StringBuilder();
            pc_sb.AppendLine("localX,localY,localZ,globalX, globalY,globalZ,targetX,targetY,targetZ," +
                "headX,headY,headZ,headForwardX,headForwardY,headForwardZ,eyeOriginX,eyeOriginY,eyeOriginZ," +
                "eyeDirectionX,eyeDirectionY,eyeDirectionZ,timestamp");

            if (!Directory.Exists(saveDir))
            {
                Directory.CreateDirectory(saveDir);
            }

            if (multipleTarget)
            {
                if (is3DObject)
                {
                    currentIndex = Range(0, targetList3D[currentFaceIndex].Count);
                    currentTarget = targetList3D[currentFaceIndex][currentIndex];
                } else
                {
                    currentIndex = Range(0, targetList.Count);
                    currentTarget = targetList[currentIndex];
                }
                currentTarget.SetActive(true);
            }
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
            Vector3 tarTrans = multipleTarget ? currentTarget.transform.position : Vector3.zero;
            tarTrans = target.transform.InverseTransformPoint(tarTrans);
            gaze.localHitPosition = target.transform.InverseTransformPoint(gaze.hitPosition);
            Vector3 pos = gaze.localHitPosition;
            if (localBounds.Contains(pos) && gaze.targetName == target.name && gaze.targetName != "null")
            {
                pc_sb.AppendLine($"{pos.x:F6},{-pos.y:F6},{pos.z:F6}," +
                    $"{gaze.hitPosition.x:F6},{-gaze.hitPosition.y:F6},{gaze.hitPosition.z:F6}," +
                    $"{tarTrans.x:F6},{-tarTrans.y:F6},{tarTrans.z:F6}," +
                    $"{gaze.headPosition.x:F6},{-gaze.headPosition.y:F6},{gaze.headPosition.z:F6}," +
                    $"{gaze.headForward.x:F6},{gaze.headForward.y:F6},{gaze.headForward.z:F6}," +
                    $"{gaze.eyeOrigin.x:F6},{gaze.eyeOrigin.y:F6},{gaze.eyeOrigin.z:F6}," +
                    $"{gaze.eyeDirection.x:F6},{gaze.eyeDirection.y:F6},{gaze.eyeDirection.z:F6}," +
                    $"{(gaze.timestamp - startingTime):F6}");
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
        sb.AppendLine("localX,localY,localZ,rotationW,rotationX,rotationY,rotationZ,scaleX,scaleY,scaleZ,targetName");
        foreach (GameObject target in targetList)
        {
            Vector3 pos = target.transform.localPosition;
            Quaternion rot = target.transform.rotation;
            pos = new Vector3(pos.x, pos.y, pos.z + (zSum / zNum));
            sb.AppendLine($"{pos.x:F6},{-pos.y:F6},{pos.z:F6},{rot.w:F6},{rot.x:F6},{rot.y:F6},{rot.z:F6},{target.transform.localScale.x:F6},{target.transform.localScale.y:F6},{target.transform.localScale.z:F6},{target.name}");
        }
        File.WriteAllText(Path.Combine(saveDir, fileName), sb.ToString());
    }
}