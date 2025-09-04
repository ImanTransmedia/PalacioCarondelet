using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization.Components;

public class Interact : MonoBehaviour
{
    [SerializeField] private InputSystem_Actions inputActions;

    [Header("Interaction Settings")]
    [SerializeField] private float interactRange = 3f;

    [Header("Layers")]
    [SerializeField] private LayerMask layer3D;
    [SerializeField] private LayerMask layerTexture;
    [SerializeField] private LayerMask layerPainting;
    [SerializeField] private LayerMask layerDoor;

    [Header("Prefabs & Visuals")]
    public GameObject interactPrefab;
    public GameObject doorInteractPrefab;

    [SerializeField] private DoorNameDisplay doorNameDisplay;
    private DoorSceneLoader lastSeenDoor;

    //public float zOffset = -0.6f;

    private Transform currentTarget;
    private GameObject currentInstance;

    public FirstPersonMovement firstPerson;
    public bool wasLookingAtDoor = false;

    private void Awake()
    {
        inputActions = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        inputActions.Player.Enable();
        inputActions.Player.Interact.started += OnInteractPerformed;
    }
    private void Start() {
        GameObject hud = GameObject.Find("HUD_Manager");
        if (hud != null) {
            doorNameDisplay = hud.GetComponent<DoorNameDisplay>();
        }
        else {
            Debug.LogWarning("No se encontró el objeto 'HUD_manager'");
        }
    }

    private void OnDisable()
    {
        inputActions.Player.Interact.started -= OnInteractPerformed;
        inputActions.Player.Disable();
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        TryInteract();
    }

    public void TryInteract()
    {
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        int combinedLayerMask = layer3D | layerTexture | layerPainting | layerDoor;

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, combinedLayerMask))
        {
            int hitLayerMask = 1 << hit.collider.gameObject.layer;

            if ((hitLayerMask & layer3D) != 0)
            {
                firstPerson.isInteracting = true;
                hit.collider.GetComponent<ItemDisplay>()?.OnInteract();
            }
            else if ((hitLayerMask & layerTexture) != 0)
            {
                firstPerson.isInteracting = true;
                hit.collider.GetComponent<textureDisplay>()?.OnInteract();
            }
            else if ((hitLayerMask & layerPainting) != 0)
            {
                firstPerson.isInteracting = true;
                hit.collider.GetComponent<paintingDisplay>()?.OnInteract();
            }
            else if ((hitLayerMask & layerDoor) != 0)
            {
                hit.collider.GetComponent<DoorSceneLoader>()?.LoadNewScene();
            }
        }
    }

  private void Update()
{
    Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
    int combinedLayerMask = layer3D | layerTexture | layerPainting | layerDoor;

    if (Physics.Raycast(ray, out RaycastHit hit, interactRange, combinedLayerMask))
    {
        int hitLayerMask = 1 << hit.collider.gameObject.layer;

        bool isDoor = (hitLayerMask & layerDoor) != 0;
        wasLookingAtDoor = isDoor;

            if (isDoor)
            {
                UIIngameManager.Instance.ShowInteractPrompt(true);
                UIIngameManager.Instance.HideInteractPrompt(false);
                DoorSceneLoader door = hit.collider.GetComponent<DoorSceneLoader>();
              if (door != null)
                {
                    doorNameDisplay.UpdateDoorName(door.nombreEscenario);
                    if (door != lastSeenDoor)
                    {
                        lastSeenDoor = door;
                    }
                }
            }
            else
            {
                UIIngameManager.Instance.ShowInteractPrompt(false);
                UIIngameManager.Instance.HideInteractPrompt(true);
                 if (lastSeenDoor != null)
                    {
                        lastSeenDoor = null;
                    }
            }

        if (hit.transform != currentTarget)
        {
            DestroyCurrentInstance();
            currentTarget = hit.transform;

           GameObject prefabToInstantiate = isDoor ? doorInteractPrefab : interactPrefab;
           currentInstance = Instantiate(prefabToInstantiate);

            Bounds colliderBounds = hit.collider.bounds;
            Vector3 centerPosition = colliderBounds.center;

            Vector3 objectOffset = Vector3.zero;
            if (hit.collider.TryGetComponent(out ItemDisplay item))
                objectOffset = item.eyeOffset;
            else if (hit.collider.TryGetComponent(out textureDisplay texture))
                objectOffset = texture.eyeOffset;
            else if (hit.collider.TryGetComponent(out paintingDisplay painting))
                objectOffset = painting.eyeOffset;
             else if (hit.collider.TryGetComponent(out DoorSceneLoader door))
                objectOffset = door.doorIconOffset;

            Vector3 finalPosition = centerPosition + objectOffset;
            currentInstance.transform.position = finalPosition;
        }
    }
    else
    {
        UIIngameManager.Instance.HideInteractPrompt(true);  
        UIIngameManager.Instance.HideInteractPrompt(false); 
        DestroyCurrentInstance();
        wasLookingAtDoor = false;
    }
}

    void DestroyCurrentInstance()
    {
        if (currentInstance != null)
        {
            Destroy(currentInstance);
            currentInstance = null;
        }
        currentTarget = null;
    }

    Bounds GetBounds(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        Bounds bounds = new Bounds(obj.transform.position, Vector3.zero);

        foreach (Renderer rend in renderers)
        {
            bounds.Encapsulate(rend.bounds);
        }

        return bounds;
    }
}
