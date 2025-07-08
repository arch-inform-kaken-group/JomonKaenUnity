using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class HL2Controls : MonoBehaviour, IMixedRealityFocusHandler, IMixedRealityPointerHandler
{
    [Header("Visual Feedback Materials")]
    [SerializeField] private Material defaultMaterial;
    [SerializeField] private Material gazedMaterial;
    [SerializeField] private Material selectedMaterial;
    [SerializeField] private Material recordingMaterial;

    [Header("Target Visuals")]
    [Tooltip("The GameObject whose material will be changed. If null, uses this GameObject's Renderer.")]
    [SerializeField] private GameObject visualDisplayObject;

    [Header("Eye Tracking Settings")]
    [SerializeField] private bool enableEyeTracking = true;
    [SerializeField] private KeyCode toggleRecordingKey = KeyCode.M;

    private MeshRenderer meshRenderer;
    private bool isGazed = false;
    private bool isSelected = false;
    private bool isRecording = false;
    public static HL2Controls currentlySelected = null;

    // Eye tracking components
    private EyeTrackingTarget eyeTrackingTarget;

    public EnhancedDataRecorder enhancedDataRecorder;

    void Start()
    {
        InitializeComponents();
        SetVisualState(InteractionState.Default);
    }

    void Update()
    {
        HandleKeyboardInput();
    }

    private void InitializeComponents()
    {
        // Initialize visual components
        meshRenderer = visualDisplayObject != null ?
            visualDisplayObject.GetComponent<MeshRenderer>() :
            GetComponent<MeshRenderer>();

        if (meshRenderer == null)
        {
            Debug.LogError($"HL2Controls on {gameObject.name} requires a MeshRenderer.", this);
            enabled = false;
            return;
        }

        defaultMaterial = defaultMaterial ?? meshRenderer.material;

        // Initialize eye tracking if enabled
        if (enableEyeTracking)
        {
            eyeTrackingTarget = GetComponent<EyeTrackingTarget>() ?? gameObject.AddComponent<EyeTrackingTarget>();
            SetEyeTrackingEnabled(false); // Start with eye tracking disabled
        }
    }

    private void HandleKeyboardInput()
    {
        if (Input.GetKeyDown(toggleRecordingKey))
        {
            ToggleRecording();
        }
    }

    public void OnFocusEnter(FocusEventData eventData)
    {
        if (IsValidPointer(eventData.Pointer))
        {
            isGazed = true;
            UpdateVisualState();
        }
    }

    public void OnFocusExit(FocusEventData eventData)
    {
        if (IsValidPointer(eventData.Pointer))
        {
            isGazed = false;
            UpdateVisualState();
        }
    }

    private bool IsValidPointer(IMixedRealityPointer pointer)
    {
        // Supports head gaze, eye gaze, and mouse pointer (for emulator)
        return pointer.InputSourceParent.SourceType == InputSourceType.Head ||
               pointer.InputSourceParent.SourceType == InputSourceType.Eyes ||
               pointer is MousePointer;
    }

    public void OnPointerClicked(MixedRealityPointerEventData eventData)
    {
        if (!isGazed) return;

        if (isSelected)
        {
            if (isRecording) ToggleRecording();
            else SetSelected(false);
        }
        else
        {
            SelectObject();
        }

        eventData.Use();
    }

    private void SelectObject()
    {
        if (currentlySelected != null)
        {
            currentlySelected.SetSelected(false);
        }
        SetSelected(true);
        currentlySelected = this;
    }

    public void SetSelected(bool selected)
    {
        if (isSelected == selected) return;

        isSelected = selected;

        if (!isSelected)
        {
            if (isRecording) ToggleRecording();
            currentlySelected = null;
        }

        UpdateVisualState();
    }

    private void ToggleRecording()
    {
        if (!isSelected || !enableEyeTracking) return;

        enhancedDataRecorder.isRecording = !enhancedDataRecorder.isRecording;

        isRecording = !isRecording;
        SetEyeTrackingEnabled(isRecording);
        UpdateVisualState();

        Debug.Log($"Eye tracking {(isRecording ? "enabled" : "disabled")} for {gameObject.name}");
    }

    private void SetEyeTrackingEnabled(bool enabled)
    {
        if (eyeTrackingTarget != null)
        {
            eyeTrackingTarget.enabled = enabled;
        }
    }

    private void UpdateVisualState()
    {
        InteractionState state = InteractionState.Default;

        if (isRecording)
        {
            state = InteractionState.Recording;
        }
        else if (isSelected)
        {
            state = InteractionState.Selected;
        }
        else if (isGazed)
        {
            state = InteractionState.Gazed;
        }

        SetVisualState(state);
    }

    private void SetVisualState(InteractionState state)
    {
        if (meshRenderer == null) return;

        Material materialToApply = state switch
        {
            InteractionState.Gazed => gazedMaterial ?? defaultMaterial,
            InteractionState.Selected => selectedMaterial ?? defaultMaterial,
            InteractionState.Recording => recordingMaterial ?? selectedMaterial ?? defaultMaterial,
            _ => defaultMaterial
        };

        meshRenderer.material = materialToApply;
    }

    void OnDisable()
    {
        CleanUp();
    }

    void OnDestroy()
    {
        CleanUp();
    }

    private void CleanUp()
    {
        if (isSelected && currentlySelected == this)
        {
            currentlySelected = null;
        }

        SetEyeTrackingEnabled(false);
        SetVisualState(InteractionState.Default);
    }

    // Required interface methods
    public void OnPointerDown(MixedRealityPointerEventData eventData) { }
    public void OnPointerUp(MixedRealityPointerEventData eventData) { }
    public void OnPointerDragged(MixedRealityPointerEventData eventData) { }

    private enum InteractionState
    {
        Default,
        Gazed,
        Selected,
        Recording
    }
}