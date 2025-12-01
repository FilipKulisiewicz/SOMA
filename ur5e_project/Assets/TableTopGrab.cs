
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

public class TableTopGrabAdvanced : MonoBehaviour
{
    [Header("Vertical Movement")]
    public float verticalSpeed = 0.3f;
    public InputActionProperty UpButton; 
    public InputActionProperty DownButton;

    [Header("Rotation")]
    public bool snapRotation = false;
    public float snapAngle = 90f;
    public InputActionProperty rotateAxis; // Thumbstick Vector2

    [Header("Disable Orientation Alignment On Grab")]
    public bool keepOrientationOnGrab = true;

    private XRGrabInteractable grab;
    private Transform attachTransform;
    private float lockedY;
    private bool isGrabbed = false;

    private float dy = 0f;
    private float snapCooldown = 0.75f;
    private float snapTimer = 0f;

    [Header("Snapping")]
    public bool enableHorizontalSnapping = false;
    public float snapStep = 0.05f;

    private Quaternion initialRotation;

    

    private void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();

        if (keepOrientationOnGrab)
            grab.trackRotation = false;
    }

    private void OnEnable()
    {
        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnRelease);
    }

    private void OnDisable()
    {
        grab.selectEntered.RemoveListener(OnGrab);
        grab.selectExited.RemoveListener(OnRelease);
    }

    private float originalMass;

    [Header("Make Other Boxes Kinematic While Grabbing One")]
    public bool makeOthersNotKinematic = true;

    // ----- BEGIN GRAB -----
    private void OnGrab(SelectEnterEventArgs args)
    {
        dy = 0f;
        isGrabbed = true;
        // reduce mass so it doesn't push other boxes much
        var rb = GetComponent<Rigidbody>();
        // ensure rotation is locked while grabbed
        if (rb != null)
        {
            originalMass = rb.mass;
            rb.mass = originalMass * 0.1f;
        }
        attachTransform = args.interactorObject.GetAttachTransform(grab);
        // lock to current object height so release won't jump
        lockedY = transform.position.y;
        initialRotation = transform.rotation;

        // --- NEW: freeze all other boxes so they do not get pushed ---
        if (makeOthersNotKinematic)
        {
            foreach (var box in GameObject.FindGameObjectsWithTag("Box"))
            {
                if (box == this.gameObject) continue;
                var rbOther = box.GetComponent<Rigidbody>();
                if (rbOther != null)
                {
                    rbOther.isKinematic = true;
                }
            }
        }
    }

    // ----- BEGIN RELEASE -----
    private void OnRelease(SelectExitEventArgs args)
    {
        // restore mass
        // also unfreeze rotation
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.mass = originalMass;
        rb.freezeRotation = false; // unlock rotation

        // --- NEW: unfreeze all other boxes back to normal physics ---
        if (makeOthersNotKinematic)
        {
            foreach (var box in GameObject.FindGameObjectsWithTag("Box"))
            {
                if (box == this.gameObject) continue;
                var rbOther = box.GetComponent<Rigidbody>();
                if (rbOther != null)
                {
                    rbOther.isKinematic = false;
                }
            }
        }
        isGrabbed = false;
        attachTransform = null;
    }

    private void Update()
    {
        if (!isGrabbed || attachTransform == null) return;

        snapTimer -= Time.deltaTime;

        // Vertical: A/B

        if (UpButton.action.IsPressed()) dy += verticalSpeed * Time.deltaTime;
        if (DownButton.action.IsPressed()) dy -= verticalSpeed * Time.deltaTime;
        // ------------------------
        // 1. Horizontal Movement
        // ------------------------

        Vector3 p = attachTransform.position;
        
        // Optional horizontal snapping
        if (enableHorizontalSnapping)
        {
            p.x = Mathf.Round(p.x / snapStep) * snapStep;
            p.z = Mathf.Round(p.z / snapStep) * snapStep;
        }
        p.y = lockedY + dy;
        // follow ray hit without bending and keep Y locked
                var rb = GetComponent<Rigidbody>();
        if (rb != null){
            
                rb.MovePosition(p);
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.freezeRotation = true; // new: lock rotation
        }
        else{
            transform.position = p;
        }
        // ------------------------
        // 2. Thumbstick rotation
        // ------------------------
        Vector2 stick = rotateAxis.action.ReadValue<Vector2>();
        // For readability:
        float rightLeft = stick.x;  // rotate around Y
        float upDown = stick.y;     // rotate around X

        if (!snapRotation)
        {
            // Smooth rotation
            transform.Rotate(Vector3.up, rightLeft * snapAngle * Time.deltaTime, Space.World);
            transform.Rotate(Vector3.right, -upDown * snapAngle * Time.deltaTime, Space.World);  
            // (minus upDown so pushing up tilts forward)
        }
        else
        {
            // Snap rotation
            if (snapTimer <= 0f)
            {
                // Y-axis snap (table spin)
                if (rightLeft > 0.6f)
                {
                    transform.Rotate(Vector3.up, snapAngle, Space.World);
                    snapTimer = snapCooldown;
                }
                else if (rightLeft < -0.6f)
                {
                    transform.Rotate(Vector3.up, -snapAngle, Space.World);
                    snapTimer = snapCooldown;
                }

                // X-axis snap (tilt forward/back)
                if (upDown > 0.6f)
                {
                    transform.Rotate(Vector3.right, -snapAngle, Space.World);
                    snapTimer = snapCooldown;
                }
                else if (upDown < -0.6f)
                {
                    transform.Rotate(Vector3.right, snapAngle, Space.World);
                    snapTimer = snapCooldown;
                }
            }
        }
    }
}
