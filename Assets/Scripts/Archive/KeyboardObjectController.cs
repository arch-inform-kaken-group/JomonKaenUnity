using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.Toolkit.Utilities.Solvers;
using TMPro;
using UnityEngine;

/// <summary>
/// Add keyboard and mouse controls for selecting, moving and rotating objects that already have MRTK manipulation components.
/// Updates both the object and text display under the same parent.
/// </summary>
public class KeyboardMouseObjectController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 0.4f; // xyz movement speed
    [SerializeField] private float rotateSpeed = 60f; // ryp rotational
    [SerializeField] private KeyCode toggleManipulationKey = KeyCode.M; // key press to allow movement and start recording gaze data if object is selected
    [SerializeField] private KeyCode selectKey = KeyCode.Space; // mainly used to deselect objects

    /// <summary>
    /// XYZ movement keys
    /// </summary>
    [Header("Movement Keys")]
    [SerializeField] private KeyCode moveForwardKey = KeyCode.Z;
    [SerializeField] private KeyCode moveBackwardKey = KeyCode.X;
    [SerializeField] private KeyCode moveLeftKey = KeyCode.C;
    [SerializeField] private KeyCode moveRightKey = KeyCode.V;
    [SerializeField] private KeyCode moveUpKey = KeyCode.F;
    [SerializeField] private KeyCode moveDownKey = KeyCode.G;

    /// <summary>
    /// RYP rotational keys
    /// </summary>
    [Header("Rotation Keys")]
    [SerializeField] private KeyCode rotateXPosKey = KeyCode.I;
    [SerializeField] private KeyCode rotateXNegKey = KeyCode.K;
    [SerializeField] private KeyCode rotateYPosKey = KeyCode.J;
    [SerializeField] private KeyCode rotateYNegKey = KeyCode.L;
    [SerializeField] private KeyCode rotateZPosKey = KeyCode.U;
    [SerializeField] private KeyCode rotateZNegKey = KeyCode.O;

    /// <summary>
    /// Object that changes colour (material) based on state
    /// </summary>
    [Header("Visual Feedback")]
    [SerializeField] private Material defaultMaterial;
    [SerializeField] private Material selectedMaterial;
    [SerializeField] private Material movingMaterial;
    [SerializeField] private GameObject visualDisplayObject;
    [SerializeField] private GameObject viewBlocker;

    /// <summary>
    /// Text that changes based on state
    /// </summary>
    [Header("Status Display")]
    [SerializeField] private GameObject statusDisplayObject;
    [SerializeField] private string notSelectedText = "Nothing Selected\nUse mouse to\nselect an object";
    [SerializeField] private string selectedText = "Object Selected\nPress M to view\nand manipulate object";
    [SerializeField] private string movingText = "Moving Object\nZXCV/FG: Move XYZ\nIJKL/UO: Rotate\nClick/Space: exit move mode\nEnter: Save\nEsc: Cancel";

    // Reference to MRTK components
    //private ObjectManipulator objectManipulator;
    //private SolverHandler solverHandler;

    // Selection and manipulation state
    private bool isSelected = false;
    private bool keyboardManipulationActive = false;

    // Renderers and text components
    private MeshRenderer meshRenderer;
    private TextMeshPro textComponent;
    private MeshRenderer statusMeshRenderer;

    // Original position/rotation for cancelling manipulation
    private Vector3 originalPosition;
    private Quaternion originalRotation;

    // Static reference to the currently selected controller
    public static KeyboardMouseObjectController currentlySelected = null;
    public GameObject hitTarget = null;

    // eye tracking
    private EyeTrackingTarget eyeTrackingTarget = null;

    // start recording, export data
    /// <summary>
    /// Should have access to EnhancedDataRecorder in the same game object
    /// </summary>
    public EnhancedDataRecorder enhancedDataRecorder;

    void Start()
    {
        // Get references to components
        //objectManipulator = GetComponent<ObjectManipulator>();
        //solverHandler = GetComponent<SolverHandler>();
        meshRenderer = GetComponent<MeshRenderer>();
        eyeTrackingTarget = GetComponent<EyeTrackingTarget>();

        eyeTrackingTarget.enabled = false;

        // Try to find text component under the same parent
        Transform parentTransform = transform.parent;
        if (parentTransform != null)
        {
            foreach (Transform child in parentTransform)
            {
                // Skip the current object
                if (child == transform) continue;

                // Look for TextMeshPro component
                TextMeshPro tmp = child.GetComponent<TextMeshPro>();
                if (tmp != null)
                {
                    textComponent = tmp;
                }
            }
        }

        // Find status display if not set in inspector
        if (statusDisplayObject == null)
        {
            statusDisplayObject = GameObject.Find("StatusDisplay");
        }

        if (statusDisplayObject != null)
        {
            // Try to get the text component
            TextMeshPro statusText = statusDisplayObject.GetComponent<TextMeshPro>();
            if (statusText != null)
            {
                statusText.text = notSelectedText;
            }

            // Try to get the renderer
            statusMeshRenderer = visualDisplayObject.GetComponent<MeshRenderer>();
        }

        // Store original position/rotation
        originalPosition = transform.position;
        originalRotation = transform.rotation;

        // Make sure we have materials for visual feedback
        if (defaultMaterial == null && meshRenderer != null)
        {
            defaultMaterial = meshRenderer.material;
        }
    }

    void Update()
    {
        // Check for mouse click selection
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                // If the player clicks on this object
                if (hit.transform == this.transform)
                {
                    // Deselect any previously selected object
                    if (currentlySelected != null && currentlySelected != this)
                    {
                        currentlySelected.SetSelected(false);
                    }

                    SetSelected(true);
                    currentlySelected = this;
                }
                // If the player clicks elsewhere and this is selected, deselect
                else if (isSelected && currentlySelected == this)
                {
                    SetSelected(false);
                    currentlySelected = null;
                    UpdateStatusDisplay(notSelectedText);
                }
            }
        }

        // Check for keyboard selection
        if (Input.GetKeyDown(selectKey) && currentlySelected == this)
        {
            // Toggle selection state with key press
            SetSelected(!isSelected);
            if (!isSelected)
            {
                currentlySelected = null;
                UpdateStatusDisplay(notSelectedText);
            }
        }

        // Toggle keyboard manipulation on/off
        if (Input.GetKeyDown(toggleManipulationKey) && isSelected && currentlySelected == this)
        {
            keyboardManipulationActive = !keyboardManipulationActive;

            enhancedDataRecorder.isRecording = !enhancedDataRecorder.isRecording;
            if (enhancedDataRecorder.isRecording)
            {
            }

            // Manage MRTK components when toggling
            if (keyboardManipulationActive)
            {
                // Disable MRTK manipulation components temporarily
                //if (objectManipulator != null) objectManipulator.enabled = false;
                //if (solverHandler != null) solverHandler.enabled = false;

                // Update visual feedback
                UpdateVisualFeedback(movingMaterial);
                UpdateTextComponent(transform.name + " - Moving");
                UpdateStatusDisplay(movingText);

                // enable eye-tracking
                eyeTrackingTarget.enabled = true;

                viewBlocker.SetActive(false);

                Debug.Log($"Keyboard manipulation activated for {gameObject.name}");
            }
            else
            {
                // Re-enable MRTK manipulation components
                //if (objectManipulator != null) objectManipulator.enabled = true;
                //if (solverHandler != null) solverHandler.enabled = true;

                // Update visual feedback
                UpdateVisualFeedback(selectedMaterial);
                UpdateTextComponent(transform.name + " - Selected");
                UpdateStatusDisplay(selectedText);

                // disable eye-tracking
                eyeTrackingTarget.enabled = false;

                viewBlocker.SetActive(true);

                Debug.Log($"Keyboard manipulation deactivated for {gameObject.name}");
            }
        }

        // Only process keyboard input if keyboard manipulation is active
        if (keyboardManipulationActive && currentlySelected == this)
        {
            // Handle movement
            HandleMovement();

            // Handle rotation
            HandleRotation();

            // Cancel changes with Escape key
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelChanges();
            }

            // Apply/save changes with Enter key
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                SaveCurrentTransform();

                // Exit manipulation mode but stay selected
                keyboardManipulationActive = false;

                // Re-enable MRTK components
                //if (objectManipulator != null) objectManipulator.enabled = true;
                //if (solverHandler != null) solverHandler.enabled = true;

                // Update visual feedback
                UpdateVisualFeedback(selectedMaterial);
                UpdateTextComponent(transform.name + " - Selected");
                UpdateStatusDisplay(selectedText);
            }
        }
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;

        // If we're deselecting, ensure we exit manipulation mode
        if (!isSelected && keyboardManipulationActive)
        {
            keyboardManipulationActive = false;

            // Re-enable MRTK components
            //if (objectManipulator != null) objectManipulator.enabled = true;
            //if (solverHandler != null) solverHandler.enabled = true;
        }

        // Update visual feedback
        if (isSelected)
        {
            UpdateVisualFeedback(selectedMaterial);
            UpdateTextComponent(transform.name + " - Selected");
            UpdateStatusDisplay(selectedText);
            Debug.Log($"Object {gameObject.name} selected");
        }
        else
        {
            UpdateVisualFeedback(defaultMaterial);
            UpdateTextComponent(transform.name);
            Debug.Log($"Object {gameObject.name} deselected");
        }
    }

    private void UpdateVisualFeedback(Material material)
    {
        if (material != null)
        {
            // only update the material of the specified object
            if (statusMeshRenderer != null)
            {
                statusMeshRenderer.material = material;
            }

            //// Update material on other renderers under the same parent
            //Transform parentTransform = transform.parent;
            //if (parentTransform != null)
            //{
            //    foreach (Transform child in parentTransform)
            //    {
            //        // Skip the current object and text objects
            //        if (child.GetComponent<KeyboardMouseObjectController>() != null || child.GetComponent<TextMeshPro>() != null)
            //        {
            //            continue;
            //        }
            //        else
            //        {
            //            MeshRenderer childRenderer = child.GetComponent<MeshRenderer>();
            //            if (childRenderer != null && childRenderer != meshRenderer)
            //            {
            //                childRenderer.material = material;
            //            }
            //        }


            //    }
            //}
        }
    }

    private void UpdateTextComponent(string text)
    {
        if (textComponent != null)
        {
            textComponent.text = text;
        }
    }

    private void UpdateStatusDisplay(string text)
    {
        if (statusDisplayObject != null)
        {
            TextMeshPro statusText = statusDisplayObject.GetComponent<TextMeshPro>();
            if (statusText != null)
            {
                statusText.text = text;
            }
        }
    }

    private void HandleMovement()
    {
        Vector3 movement = Vector3.zero;

        // Check all movement keys
        if (Input.GetKey(moveForwardKey)) movement += Vector3.forward * moveSpeed;
        if (Input.GetKey(moveBackwardKey)) movement += Vector3.back * moveSpeed;
        if (Input.GetKey(moveRightKey)) movement += Vector3.right * moveSpeed;
        if (Input.GetKey(moveLeftKey)) movement += Vector3.left * moveSpeed;
        if (Input.GetKey(moveUpKey)) movement += Vector3.up * moveSpeed;
        if (Input.GetKey(moveDownKey)) movement += Vector3.down * moveSpeed;

        // Apply movement in world space - move the parent if it exists
        Transform objectToMove = transform.parent != null ? transform.parent : transform;
        objectToMove.Translate(movement * Time.deltaTime, Space.World);
    }

    private void HandleRotation()
    {
        Vector3 rotation = Vector3.zero;

        // Check all rotation keys
        if (Input.GetKey(rotateXPosKey)) rotation.x += rotateSpeed;
        if (Input.GetKey(rotateXNegKey)) rotation.x -= rotateSpeed;
        if (Input.GetKey(rotateYPosKey)) rotation.y += rotateSpeed;
        if (Input.GetKey(rotateYNegKey)) rotation.y -= rotateSpeed;
        if (Input.GetKey(rotateZPosKey)) rotation.z += rotateSpeed;
        if (Input.GetKey(rotateZNegKey)) rotation.z -= rotateSpeed;

        // Apply rotation - rotate the parent if it exists
        //Transform objectToRotate = transform.parent != null ? transform.parent : transform;
        Transform objectToRotate = transform;
        objectToRotate.Rotate(rotation * Time.deltaTime, Space.World);
    }

    private void CancelChanges()
    {
        // Revert to original position and rotation - for the parent if it exists
        Transform objectToReset = transform.parent != null ? transform.parent : transform;
        objectToReset.position = originalPosition;
        objectToReset.rotation = originalRotation;

        // Turn off keyboard manipulation
        keyboardManipulationActive = false;

        // Re-enable MRTK components
        //if (objectManipulator != null) objectManipulator.enabled = true;
        //if (solverHandler != null) solverHandler.enabled = true;

        // Update visual feedback if still selected
        if (isSelected)
        {
            UpdateVisualFeedback(selectedMaterial);
            UpdateTextComponent(transform.name + " - Selected");
            UpdateStatusDisplay(selectedText);
        }
        else
        {
            UpdateVisualFeedback(defaultMaterial);
            UpdateTextComponent(transform.name);
        }

        Debug.Log("Changes canceled, reverted to original transform");
    }

    // Use this to save a new position/rotation as the original
    public void SaveCurrentTransform()
    {
        Transform objectToSave = transform.parent != null ? transform.parent : transform;
        originalPosition = objectToSave.position;
        originalRotation = objectToSave.rotation;
        Debug.Log("Saved current transform as original");
    }
}