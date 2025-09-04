using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.UIElements;

public class paintingDisplay : MonoBehaviour
{
    [Header("Configuración del Objeto")]
    [SerializeField] private string objectName;
    public LocalizedString itemName1;
    public LocalizedString itemSubTitle1;
    [SerializeField] public Vector3 eyeOffset = new Vector3(0f, 0f, 0f);
    public bool isInCarrousel = false;
    public int indexInCarrousel = 0;

    [Space(10)]
    [Header("Downloaded Sprite")]
    public URLSalon salon;
    public string imageName;
    public Sprite itemImage = null;
    public Vector3 imageScale = new Vector3(1f, 1f, 1f);

    [Space(10)]
    [Header("Eventos")]
    public UnityEvent onDisplayStart;
    public UnityEvent onDisplayEnd;

    private bool isUIOpen = false;
    private bool _filled = false;
    private bool _refreshing = false;

    private HUDManager hudManager;
    private Coroutine _waiter;

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
        hudManager = FindFirstObjectByType<HUDManager>();

        if (GameManager.Instance != null && GameManager.Instance.Config != null && !_filled)
        {
            Debug.Log($"[PaintingDisplay:{name}] config lista en Start");
            FillOnceAtPlay();
            _filled = true;
        }
    }

    private void TrySubscribeToConfigReady()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnConfigReady -= HandleConfigReady;
            GameManager.Instance.OnConfigReady += HandleConfigReady;
        }
        else
        {
            _waiter = StartCoroutine(WaitForGMThenSubscribe());
        }
    }

    private IEnumerator WaitForGMThenSubscribe()
    {
        while (GameManager.Instance == null)
            yield return null;

        GameManager.Instance.OnConfigReady -= HandleConfigReady;
        GameManager.Instance.OnConfigReady += HandleConfigReady;

        if (GameManager.Instance.Config != null && !_filled)
            HandleConfigReady();
    }

    private void HandleConfigReady()
    {
        if (_filled) return;

        Debug.Log($"[PaintingDisplay:{name}] OnConfigReady");
        FillOnceAtPlay();
        _filled = true;

        GameManager.Instance.OnConfigReady -= HandleConfigReady;
    }

    // ---------- Config ----------
    private bool TryGetMyConfig(out InteractableEntry config)
    {
        config = null;

        var autofiller = gameObject.GetComponent<InteractuableAutofiller>();
        if (autofiller.IsUnityNull())
        {
            Debug.LogWarning($"[PaintingDisplay:{name}] sin InteractuableAutofiller");
            return false;
        }

        var objeto = autofiller.objeto;
        if (GameManager.Instance != null && GameManager.Instance.TryGetObjetoByName(objeto, out config))
            return true;

        Debug.LogWarning($"[PaintingDisplay:{name}] no se encontró '{objeto}'");
        return false;
    }

    private void ApplyFromConfig(InteractableEntry config)
    {
        objectName = config.identifier;
        eyeOffset = config.eyeOffset;
        isInCarrousel = config.isInCarrousel;
        indexInCarrousel = config.carrouselIndex;
        imageName = config.imageName;
        imageScale = config.videoScale;

        if (Enum.TryParse<URLSalon>(config.salonDescarga, out var parsedSalon))
            salon = parsedSalon;
    }

    public void FillOnceAtPlay()
    {
        if (TryGetMyConfig(out var config))
        {
            Debug.Log($"[PaintingDisplay:{name}] aplicando '{config.identifier}'");
            ApplyFromConfig(config);
        }
    }

    // ---------- Interacción con refresh ----------
    public void OnInteract()
    {
        if (!_refreshing)
            StartCoroutine(RefreshApplyAndToggle());
    }

    private IEnumerator RefreshApplyAndToggle()
    {
        _refreshing = true;

        bool ok = false;
        yield return StartCoroutine(GameManager.Instance.ReloadConfigCoroutine(done => ok = done));

        if (ok && TryGetMyConfig(out var cfg))
        {
            Debug.Log($"[PaintingDisplay:{name}] re-aplicando config");
            ApplyFromConfig(cfg);
        }

        if (!isUIOpen) ShowItemUI();
        else CloseItemUI();

        _refreshing = false;
    }

    // ---------- UI ----------
    public void ShowItemUI()
    {
        string name1 = itemName1.GetLocalizedString();
        string subTitle1 = itemSubTitle1.GetLocalizedString();

        if (itemImage != null) ShowImage(name1, subTitle1);
        else
        {
            StartCoroutine(GameManager.Instance.DownloadImageSprite(
                salon, imageName,
                sprite => { itemImage = sprite; ShowImage(name1, subTitle1); },
                err => { Debug.LogError(err); UIIngameManager.Instance.ShowPaintingLoader(false); }
            ));
        }
    }

    private void ShowImage(string name, string description)
    {
        UIIngameManager.Instance.ShowPaintingPanel(name, description, itemImage, imageScale);
        isUIOpen = true;
        onDisplayStart?.Invoke();

        CarouselManager carouselManager = FindFirstObjectByType<CarouselManager>();
        if (carouselManager != null)
            carouselManager.OpenCarouselAtIndex(indexInCarrousel);
    }

    private void CloseItemUI()
    {
        isUIOpen = false;
        UIIngameManager.Instance.HidePaintingPanel();
        onDisplayEnd?.Invoke();
    }
}
