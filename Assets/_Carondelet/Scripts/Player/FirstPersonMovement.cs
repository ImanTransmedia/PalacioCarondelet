using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonMovement : MonoBehaviour
{
    [Header("Controles alternativos")]
    public float alternateLookSpeed = 100f;

    private AccessibilityManager accessibilityManager;
    private bool usingAlternateControls = false;
    [Header("Valores de control")]
    public InputSystem_Actions inputActions;

    Vector2 moveInput;
    Vector2 lookInput;

    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float mouseSensitivity = 25f;

    [Header("Camara")]
    [SerializeField] private CinemachineVirtualCamera virtualCamera;

    //[SerializeField] AudioSource stepSFX;

    private CharacterController controller;
    private Transform cameraHolder;
    private float xRotation = 0f;

    public bool isInteracting;

    void Start()
    {
        accessibilityManager = FindObjectOfType<AccessibilityManager>();
        controller = GetComponent<CharacterController>();
        cameraHolder = virtualCamera.transform;
        if (isInteracting = false)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
     
    }

    private void Awake()
    {
        isInteracting = false;
        inputActions = new InputSystem_Actions();
    }
    private void OnEnable()
    {
        inputActions.Player.Enable();
        inputActions.Player.Move.performed += OnMove;
        inputActions.Player.Move.canceled += OnMove;
        inputActions.Player.Look.performed += OnLook;
        inputActions.Player.Look.canceled += OnLook;
        //inputActions.Player.Interact.started += OnInteractPerformed;
    }

    private void OnDisable()
    {
        inputActions.Player.Move.performed -= OnMove;
        inputActions.Player.Move.canceled -= OnMove;
        inputActions.Player.Look.performed -= OnLook;
        inputActions.Player.Look.canceled -= OnLook;
        //inputActions.Player.Interact.started -= OnInteractPerformed;
        inputActions.Player.Disable();
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    private void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }



    void Update()
{
    if (!isInteracting)
    {
     
        bool currentAlt = accessibilityManager != null && accessibilityManager.enableAlternativeControls;
        if (currentAlt != usingAlternateControls)
        {
            usingAlternateControls = currentAlt;

            if (usingAlternateControls)
            {
              
                xRotation = 0f;
                cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            }
        }

        HandleMovement();

        if (usingAlternateControls)
            HandleKeyLook();
        else
            HandleMouseLook();
    }

    controller.Move(new Vector3(0, -0.1f, 0));
}

    private void HandleMovement()
    {
        Vector3 moveDirection = (transform.forward * moveInput.y + transform.right * moveInput.x).normalized;
        controller.Move(moveDirection * moveSpeed * Time.deltaTime);
    }

    private void HandleMouseLook()
    {
        float mouseX = lookInput.x * mouseSensitivity /* Time.deltaTime*/;
        float mouseY = lookInput.y * mouseSensitivity /* Time.deltaTime*/;

        // Vertical rotation (up/down)
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // Apply rotations
        cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f); // Camera tilt
        transform.Rotate(Vector3.up * mouseX); // Player body rotation
    }

        private void HandleKeyLook()
        {
            float keyX = 0f;
            float keyY = 0f;

            if (Keyboard.current.iKey.isPressed) keyY = 1f;
            if (Keyboard.current.kKey.isPressed) keyY = -1f;
            if (Keyboard.current.jKey.isPressed) keyX = -1f;
            if (Keyboard.current.lKey.isPressed) keyX = 1f;

            float mouseX = keyX * alternateLookSpeed * Time.deltaTime;
            float mouseY = keyY * alternateLookSpeed * Time.deltaTime;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);

            cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            transform.Rotate(Vector3.up * mouseX);
        }

    public void SetSensibility(float speed)
    {
        mouseSensitivity = speed; 
    }
}
