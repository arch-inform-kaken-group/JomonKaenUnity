using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using System.Linq;
using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.Toolkit.Input;
using UnityEngine.Windows.Speech;

public class QuestionnaireController : MonoBehaviour
{
    [SerializeField] private TextMeshPro questionText;
    [SerializeField] private Interactable[] answerButtons;

    private string gazedObjectName;
    private string gazedVoxelID;
    private Action<string, string, string> onAnswerSelectedCallback;

    //private string[] answerChoices = new string[]
    //{
    //    "Because the shape caught my attention",
    //    "Because it looks beautiful or artistic",
    //    "I don't understand its meaning or use / I am thinking about it",
    //    "Because it feels eerie / disturbing / or unsettling",
    //    "No specific reason / Just happened to look",
    //    "Need more time to view"
    //};

    private string[] answerChoices = new string[]
    {
        "1 面白い／気になる形だと感じた",
        "2 美しい／芸術的だと感じた",
        "3 疑問／不思議／意味不明と感じた",
        "4 不安・不気味・怖いと感じた",
        "5 なんとなく見ていた",
    };

    private KeywordRecognizer keywordRecognizer;
    private System.Collections.Generic.Dictionary<string, Action> speechCommands = new System.Collections.Generic.Dictionary<string, Action>();

    public void InitializeQuestionnaire(string objectName, string voxelID, Action<string, string, string> callback)
    {
        gazedObjectName = objectName;
        gazedVoxelID = voxelID; // Store the voxel ID
        onAnswerSelectedCallback = callback;

        //questionText.text = "Why are you looking at this part? Say the number to answer.";
        questionText.text = "この部分を見ているのはなぜですか？";

        if (keywordRecognizer != null)
        {
            keywordRecognizer.Stop();
            keywordRecognizer.Dispose();
            keywordRecognizer = null;
        }
        speechCommands.Clear();


        for (int i = 0; i < answerButtons.Length; i++)
        {
            if (i < answerChoices.Length)
            {
                answerButtons[i].GetComponentInChildren<TextMeshPro>().text = answerChoices[i];
                int choiceIndex = i; // Capture for lambda

                // Set up button click listener
                answerButtons[i].OnClick.RemoveAllListeners();
                answerButtons[i].OnClick.AddListener(() => OnAnswerSelected(answerChoices[choiceIndex]));
                answerButtons[i].gameObject.SetActive(true);

                // Speech Input Integration
                string spokenNumber = GetSpokenNumber(i + 1);
                if (!string.IsNullOrEmpty(spokenNumber))
                {
                    // Add speech command to dictionary
                    speechCommands.Add(spokenNumber, () => OnAnswerSelected(answerChoices[choiceIndex]));
                }
            }
            else
            {
                answerButtons[i].gameObject.SetActive(false);
            }
        }

        // Initialize and start KeywordRecognizer after setting up all commands
        if (speechCommands.Count > 0)
        {
            keywordRecognizer = new KeywordRecognizer(speechCommands.Keys.ToArray());
            keywordRecognizer.OnPhraseRecognized += OnPhraseRecognized;
            keywordRecognizer.Start();
            Debug.Log("Speech recognition started for questionnaire.");
        }
        else
        {
            Debug.LogWarning("No speech commands registered for the questionnaire.");
        }
    }

    void Update()
    {
        CheckKeyboardInput();
    }

    private void CheckKeyboardInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            SelectAnswerByNumber(1);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            SelectAnswerByNumber(2);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            SelectAnswerByNumber(3);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
        {
            SelectAnswerByNumber(4);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5))
        {
            SelectAnswerByNumber(5);
        }
    }

    private void SelectAnswerByNumber(int number)
    {
        if (number > 0 && number <= answerChoices.Length)
        {
            OnAnswerSelected(answerChoices[number - 1]);
        }
        else
        {
            Debug.LogWarning($"Attempted to select an invalid answer number: {number}");
        }
    }

    private void OnPhraseRecognized(PhraseRecognizedEventArgs args)
    {
        Debug.Log($"Speech recognized: {args.text}");
        if (speechCommands.TryGetValue(args.text, out Action action))
        {
            action.Invoke();
        }
    }

    private string GetSpokenNumber(int num)
    {
        switch (num)
        {
            case 1: return "one";
            case 2: return "two";
            case 3: return "three";
            case 4: return "four";
            case 5: return "five";
            default: return null;
        }
    }

    private void OnAnswerSelected(string selectedAnswer)
    {
        Debug.Log($"Selected answer: {selectedAnswer}");
        // Pass the voxel ID back in the callback
        onAnswerSelectedCallback?.Invoke(gazedObjectName, gazedVoxelID, selectedAnswer);

        // Stop and dispose of the KeywordRecognizer when done with the questionnaire
        if (keywordRecognizer != null)
        {
            keywordRecognizer.Stop();
            keywordRecognizer.Dispose();
            keywordRecognizer = null; // Clear the reference
        }
        Destroy(gameObject); // Close the pop-up
    }

    private void OnDestroy()
    {
        // Ensure the recognizer is stopped and disposed if the GameObject is destroyed
        if (keywordRecognizer != null)
        {
            keywordRecognizer.Stop();
            keywordRecognizer.Dispose();
            keywordRecognizer = null;
        }
    }
}
