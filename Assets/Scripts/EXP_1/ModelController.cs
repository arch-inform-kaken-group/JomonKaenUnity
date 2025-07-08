using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.SampleGazeData;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Profiling;

public class ModelController : MonoBehaviour
{
    [SerializeField]
    private List<GameObject> groups = new List<GameObject>();

    [SerializeField]
    private GameObject promptObject;

    private List<GameObject> models = new List<GameObject>();
    private int currentModelIndex = 0;
    private Vector3 previousModelPosition = Vector3.zero;
    public static GameObject currentModel;

    private string sessionPath;
    private GameObject group;

    // Start is called before the first frame update
    void Start()
    {
        sessionPath = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");

        // deactivate all models to reduce overhead
        for (int i = 0; i < groups.Count; i++) {
            models = groups[i].GetComponent<GroupItems>().GetModels();
            for (int j = 0; j < models.Count(); j++)
            {
                models[j].GetComponent<ModelGazeRecorder>().sessionPath = sessionPath;
                models[j].GetComponent<EyeTrackingTarget>().enabled = false;
                models[j].SetActive(false);
            }
        }
       
        group = groups[0];
        models = group.GetComponent<GroupItems>().GetModels();

        LoadModel();
    }

    // Update is called once per frame 
    void Update()
    {
        
    }

    public void DisableAllLiveHeatmap()
    {
        for (int i = 0; i < groups.Count; i++)
        {
            List<GameObject>  m = groups[i].GetComponent<GroupItems>().GetModels();
            for (int j = 0; j < m.Count(); j++)
            {
                m[j].GetComponent<DrawOn3DTexture>().ToggleLiveHeatmap(false);
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
            ModelGazeRecorder recorder = models[j].GetComponent<ModelGazeRecorder>();
            recorder.sessionPath = sessionPath;
            recorder.ResetAll();
        }

        currentModelIndex = 0;
        LoadModel();
    }

    public void StartRecording()
    {
        promptObject.SetActive(false);
        currentModel.GetComponent<ModelGazeRecorder>().SetIsRecording(true);
        currentModel.GetComponent<EyeTrackingTarget>().enabled = true;
    }

    public void StopRecording()
    {
        promptObject.SetActive(true);
        foreach (TextMeshPro tmp in promptObject.GetComponentsInChildren<TextMeshPro>())
        {
            tmp.SetText("Loading . . .");
        }

        if (currentModel.GetComponent<ModelGazeRecorder>().isRecording)
        {
            currentModel.GetComponent<ModelGazeRecorder>().SetIsRecording(false);
            currentModel.GetComponent<ModelGazeRecorder>().SaveAllData();
            currentModel.GetComponent<EyeTrackingTarget>().enabled = false;
        }

        foreach (TextMeshPro tmp in promptObject.GetComponentsInChildren<TextMeshPro>())
        {
            tmp.SetText("Say 'Start'");
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
        
        // Record the original transform
        previousModelPosition = currentModel.transform.parent.position;

        // Move the model to the viewing area
        currentModel.transform.parent.position = transform.position;
    }

    public void LoadPrevious() 
    {
        if (currentModelIndex == 0)
        {
            currentModelIndex = models.Count - 1;
        }
        else
        {
            currentModelIndex--;
        }

        LoadModel();

        Debug.Log("Loading " + models[currentModelIndex].name);
    }

    public void LoadNext()
    {
        if (currentModelIndex == models.Count - 1)
        {
            currentModelIndex = 0;
        }
        else
        {
            currentModelIndex++;
        }

        LoadModel();

        Debug.Log("Loading " + models[currentModelIndex].name);
    }

    public List<GameObject> GetGroups()
    {
        return groups;
    }
}
