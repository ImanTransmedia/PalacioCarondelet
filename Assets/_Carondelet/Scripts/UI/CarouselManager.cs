using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization;

public class CarouselManager : MonoBehaviour
{
    [Header("UI References")]
    private AccessibilityManager accessibilityManager;
    public GameObject carrouselPanel;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;
    public Image imageDisplay;

    [Header("Painting List")]
    private List<paintingDisplay> paintingDisplays = new List<paintingDisplay>();
    private int currentIndex = 0;

    private int currentLoadVersion = 0;

    // Control de suscripci�n/espera
    private Coroutine _waiter;
    private bool _builtAfterConfig = false;

    private void OnEnable()
    {
        TrySubscribeToConfigReady();
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnConfigReady -= HandleConfigReady;

        if (_waiter != null)
        {
            StopCoroutine(_waiter);
            _waiter = null;
        }
    }

    private void Start()
    {
        accessibilityManager = Object.FindFirstObjectByType<AccessibilityManager>();

        // Si la config YA est� lista cuando arrancamos, construimos el carrusel ahora
        if (GameManager.Instance != null && GameManager.Instance.Config != null)
        {
            RebuildCarouselItems();
            _builtAfterConfig = true;

            // Si hay �tems y el panel a�n est� oculto, actualizamos la pintura visible
            if (paintingDisplays.Count > 0 && !carrouselPanel.activeInHierarchy)
            {
                UpdatePainting();
            }
        }
        // Si no est� lista, esperaremos a OnConfigReady (por TrySubscribeToConfigReady)
    }

    private void TrySubscribeToConfigReady()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnConfigReady -= HandleConfigReady; // evitar dobles
            GameManager.Instance.OnConfigReady += HandleConfigReady;
        }
        else
        {
            // Espera a que el GM aparezca y luego suscribe
            _waiter = StartCoroutine(WaitForGMThenSubscribe());
        }
    }

    private IEnumerator WaitForGMThenSubscribe()
    {
        while (GameManager.Instance == null)
            yield return null;

        GameManager.Instance.OnConfigReady -= HandleConfigReady;
        GameManager.Instance.OnConfigReady += HandleConfigReady;

        // Si la config ya est�, dispara como si hubiera llegado el evento
        if (GameManager.Instance.Config != null)
            HandleConfigReady();
    }

    private void HandleConfigReady()
    {
        // Construye/ordena basado en los valores ya aplicados por paintingDisplay.FillOnceAtPlay()
        RebuildCarouselItems();
        _builtAfterConfig = true;

        if (paintingDisplays.Count > 0)
        {
            // No abras el panel autom�ticamente, pero s� prepara la primera imagen
            UpdatePainting();
        }

        // Si solo necesitas construir una vez, puedes desuscribirte
        GameManager.Instance.OnConfigReady -= HandleConfigReady;
    }

    /// <summary>
    /// Reconstruye la lista de paintings SOLO cuando la config ya est� disponible.
    /// Asegura que isInCarrousel/indexInCarrousel provengan de la configuraci�n remota.
    /// </summary>
    private void RebuildCarouselItems()
    {
        paintingDisplay[] displays = FindObjectsByType<paintingDisplay>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        paintingDisplays = displays
            .Where(d => d.isInCarrousel)
            .OrderBy(d => d.indexInCarrousel)
            .ToList();

        // Resetea el �ndice si la lista cambi� de tama�o
        if (currentIndex >= paintingDisplays.Count)
            currentIndex = 0;

        // Invalida cargas previas
        currentLoadVersion++;
    }

    private void Update()
    {
        if (carrouselPanel != null && carrouselPanel.activeInHierarchy)
        {
            if (accessibilityManager != null && accessibilityManager.enableAlternativeControls)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                {
                    PreviousItem();
                }
                else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                {
                    NextItem();
                }
            }
        }
    }

    public void NextItem()
    {
        if (paintingDisplays.Count == 0) return;

        currentIndex = (currentIndex + 1) % paintingDisplays.Count;
        UpdatePainting();
    }

    public void PreviousItem()
    {
        if (paintingDisplays.Count == 0) return;

        currentIndex = (currentIndex - 1 + paintingDisplays.Count) % paintingDisplays.Count;
        UpdatePainting();
    }

    private void UpdatePainting()
    {
        if (paintingDisplays.Count == 0) return;

        // Si a�n no hemos reconstruido tras la config, no dispares descargas con valores por defecto
        if (!_builtAfterConfig && (GameManager.Instance == null || GameManager.Instance.Config == null))
            return;

        paintingDisplay currentDisplay = paintingDisplays[currentIndex];
        titleText.text = currentDisplay.itemName1.GetLocalizedString();
        descriptionText.text = currentDisplay.itemSubTitle1.GetLocalizedString();

        // Si ya est� en cach� en el propio paintingDisplay, �salo
        if (currentDisplay.itemImage != null)
        {
            imageDisplay.sprite = currentDisplay.itemImage;
            return;
        }

        int loadVersion = ++currentLoadVersion;

        StartCoroutine(GameManager.Instance.DownloadImageSprite(
            currentDisplay.salon,
            currentDisplay.imageName,
            sprite =>
            {
                if (loadVersion != currentLoadVersion) return;

                if (sprite != null)
                {
                    currentDisplay.itemImage = sprite;
                    imageDisplay.sprite = sprite;
                }
                else
                {
                    Debug.LogWarning($"[Carousel] No se pudo resolver imagen para {currentDisplay.imageName} en {currentDisplay.salon}");
                }
            },
            err =>
            {
                if (loadVersion != currentLoadVersion) return;
                Debug.LogError(err);
            }
        ));
    }

    /// <summary>
    /// Llamable externamente si necesitas forzar una reconstrucci�n manual (por ejemplo, al recargar escena).
    /// </summary>
    public void ForceRefresh()
    {
        if (GameManager.Instance != null && GameManager.Instance.Config != null)
        {
            RebuildCarouselItems();
            _builtAfterConfig = true;
            if (paintingDisplays.Count > 0)
                UpdatePainting();
        }
    }

    public void OpenCarouselAtIndex(int index)
    {
        if (paintingDisplays.Count == 0) return;

        if (index >= 0 && index < paintingDisplays.Count)
        {
            currentIndex = index;

            // Si alguien intenta abrir antes de que se construya tras config, fuerza refresco
            if (!_builtAfterConfig && GameManager.Instance != null && GameManager.Instance.Config != null)
            {
                RebuildCarouselItems();
                _builtAfterConfig = true;
            }

            UpdatePainting();
            carrouselPanel.SetActive(true);
        }
    }
}
