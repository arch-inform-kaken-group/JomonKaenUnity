using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.SampleGazeData;

public class ExpModelController : MonoBehaviour
{
    [SerializeField]
    private List<GameObject> groups;

    [SerializeField]
    private GameObject promptObject;

    [SerializeField]
    private GameObject eyeRecalibrationObject;

    private List<GameObject> models = new List<GameObject>();
    private int currentModelIndex = 0;
    private Vector3 previousModelPosition = Vector3.zero;
    public static GameObject currentModel;

    private string sessionPath;
    private GameObject group;

    // recording state
    private bool admin = false;
    public static bool recorded = false;

    void Start()
    {
        sessionPath = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");

        for (int i=0; i < groups.Count; i++)
        {
            models = groups[i].GetComponent<GroupItems>().GetModels();
            for (int j =0; j < models.Count; j++)
            {
                models[j].GetComponent<ExpModelGazeRecorder>().sessionPath = sessionPath;
                models[j].GetComponent<EyeTrackingTarget>().enabled = false;
                models[j].SetActive(false);
            }
        }

        DisableAllLiveHeatmap();

        promptObject.SetActive(false);

        group = groups[0];
        models = group.GetComponent<GroupItems>().GetModels();

        LoadModel();
    }

    void Update()
    {
        
    }

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

    public void SelectGroup(int groupNumber) 
    {
        group = groups[groupNumber];
        models = group.GetComponent<GroupItems>().GetModels();

        sessionPath = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");
        for (int j = 0; j < models.Count(); j++)
        {
            ExpModelGazeRecorder recorder = models[j].GetComponent<ExpModelGazeRecorder>();
            recorder.sessionPath = sessionPath;
            recorder.ResetAll();
        }

        promptObject.SetActive(false);

        currentModelIndex = 0;
        LoadModel();
    }

    public void StartRecording() 
    {
        if (!currentModel.GetComponent<ExpModelGazeRecorder>().isRecording && !recorded)
        {
            currentModel.GetComponent<ExpModelGazeRecorder>().SetIsRecording(true);
            currentModel.GetComponent<EyeTrackingTarget>().enabled = true;
        }
    }

    public void StopRecording() 
    {
        if (currentModel.GetComponent<ExpModelGazeRecorder>().isRecording)
        {
            currentModel.GetComponent<ExpModelGazeRecorder>().SetIsRecording(false);
            currentModel.GetComponent<ExpModelGazeRecorder>().SaveAllData();
            currentModel.GetComponent<EyeTrackingTarget>().enabled = false;
        }
    }

    private void LoadModel() 
    {
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
        currentModel.GetComponent<ExpModelGazeRecorder>().ResetAll();

        // Record the original transform
        previousModelPosition = currentModel.transform.parent.position;

        // Move the model to the viewing area
        currentModel.transform.parent.position = transform.position;

        recorded = false;
    }

    public void LoadPrevious() 
    {
        if (!currentModel.GetComponent<ExpModelGazeRecorder>().isRecording || admin)
        {
            if (currentModelIndex == 0)
            {
                //currentModelIndex = models.Count - 1;
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
        if ((!currentModel.GetComponent<ExpModelGazeRecorder>().isRecording && recorded) || admin)
        {
            if (currentModelIndex == models.Count - 1)
            {
                //currentModelIndex = 0;
                promptObject.SetActive(true);
                StopRecording();
            }
            else
            {
                currentModelIndex++;
                LoadModel();
            }

            Debug.Log("Loading " + models[currentModelIndex].name);
        }
    }

    public List<GameObject> GetGroups()
    {
        return groups;
    }

    public void EyeRecalibrationTesting()
    {
        eyeRecalibrationObject.SetActive(!eyeRecalibrationObject.activeInHierarchy);
    }

    public void ToggleAdminMode()
    {
        admin = !admin;
    }

    public static void ToggleRecorded()
    {
        recorded = !recorded;
    }
}
