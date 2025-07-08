using Microsoft.MixedReality.Toolkit.Input;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Control which experiment is run, currently this script is disabled as we have settled on configuration 3
/// To ensure the program functions correctly, please refer to the documentation on how to change experiments
/// 
/// どの実験を実行するかを制御します。現在、設定3で決定したため、このスクリプトは無効化されています。
/// プログラムが正しく機能するように、実験の変更方法についてはドキュメントを参照してください。
/// </summary>
public class ExperimentController : MonoBehaviour
{
    [SerializeField]
    private int experimentNo = 1;

    void Start()
    {
        if (experimentNo == 1)
        {
            DisableExperiment2();
            DisableExperiment3();
        }
        else if (experimentNo == 2) 
        {
            DisableExperiment1();
            DisableExperiment3();
        }
        else if (experimentNo == 3)
        {
            DisableExperiment1();
            DisableExperiment2();
        }
    }

    void Update()
    {
        
    }

    private void DisableExperiment1()
    {
        List<GameObject> exp1Groups = GetComponent<ModelController>().GetGroups();
        for (int i = 0; i < exp1Groups.Count; i++)
        {
            List<GameObject> models = exp1Groups[i].GetComponent<GroupItems>().GetModels();
            for (int j = 0; j < models.Count; j++)
            {
                ModelGazeRecorder gazeRecorder = models[j].GetComponent<ModelGazeRecorder>();
                if (gazeRecorder != null)
                {
                    gazeRecorder.enabled = false;
                }
                else
                {
                    Debug.LogWarning("ModelGazeRecorder component not found on model " + models[j].name);
                }
            }
        }
    }

    private void DisableExperiment2()
    {
        List<GameObject> exp2Groups = GetComponent<ExpModelController>().GetGroups();
        for (int i = 0; i < exp2Groups.Count; i++)
        {
            List<GameObject> models = exp2Groups[i].GetComponent<GroupItems>().GetModels();
            for (int j = 0; j < models.Count; j++)
            {
                ExpModelGazeRecorder gazeRecorder = models[j].GetComponent<ExpModelGazeRecorder>();
                if (gazeRecorder != null)
                {
                    gazeRecorder.enabled = false;
                }
                else
                {
                    Debug.LogWarning("ExpModelGazeRecorder component not found on model " + models[j].name);
                }
            }
        }
    }

    private void DisableExperiment3()
    {
        List<GameObject> exp3Groups = GetComponent<QNAModelController>().GetGroups();
        for (int i = 0; i < exp3Groups.Count; i++)
        {
            List<GameObject> models = exp3Groups[i].GetComponent<GroupItems>().GetModels();
            for (int j = 0; j < models.Count; j++)
            {
                QNAModelGazeRecorder gazeRecorder = models[j].GetComponent<QNAModelGazeRecorder>();
                if (gazeRecorder != null)
                {
                    gazeRecorder.enabled = false;
                }
                else
                {
                    Debug.LogWarning("QNAModelGazeRecorder component not found on model " + models[j].name);
                }
            }
        }
    }
}
