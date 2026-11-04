using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인게임 설정 패널. 전투 중에만 버튼을 표시하고, 열리면 오버레이 일시정지한다.
/// </summary>
public class GameSettingsUI : MonoBehaviour
{
    [Header("Open")]
    [SerializeField] private GameObject openButtonRoot;
    [SerializeField] private Button openButton;

    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button closeButton;

    [Header("Toggles")]
    [SerializeField] private Button bgmToggleButton;
    [SerializeField] private Image bgmToggleImage;
    [SerializeField] private Button sfxToggleButton;
    [SerializeField] private Image sfxToggleImage;

    [Header("Toggle Sprites")]
    [SerializeField] private Sprite onSprite;
    [SerializeField] private Sprite offSprite;

    public static GameSettingsUI Instance { get; private set; }

    private bool isHudVisible;
    private bool isOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ResolveReferences();

        if (openButton != null)
        {
            openButton.onClick.RemoveListener(Open);
            openButton.onClick.AddListener(Open);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }

        if (bgmToggleButton != null)
        {
            bgmToggleButton.onClick.RemoveListener(OnBgmToggleClicked);
            bgmToggleButton.onClick.AddListener(OnBgmToggleClicked);
        }

        if (sfxToggleButton != null)
        {
            sfxToggleButton.onClick.RemoveListener(OnSfxToggleClicked);
            sfxToggleButton.onClick.AddListener(OnSfxToggleClicked);
        }

        ResolveToggleImages();

        if (panelRoot != null)
            panelRoot.SetActive(false);

        RefreshTogglesFromSettings();
        SetVisible(false);
    }

    private void OnEnable()
    {
        GameAudioSettings.Changed += RefreshTogglesFromSettings;
    }

    private void OnDisable()
    {
        GameAudioSettings.Changed -= RefreshTogglesFromSettings;
        if (isOpen)
            Close();
    }

    private void OnDestroy()
    {
        if (openButton != null)
            openButton.onClick.RemoveListener(Open);
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);
        if (bgmToggleButton != null)
            bgmToggleButton.onClick.RemoveListener(OnBgmToggleClicked);
        if (sfxToggleButton != null)
            sfxToggleButton.onClick.RemoveListener(OnSfxToggleClicked);

        if (Instance == this)
        {
            if (isOpen && GameSpeedController.Instance != null)
                GameSpeedController.Instance.SetOverlayPaused(false);

            Instance = null;
        }
    }

    public void SetVisible(bool active)
    {
        isHudVisible = active;
        ResolveReferences();

        if (!active)
            Close();
        else
            ApplyButtonVisibility();
    }

    public void Open()
    {
        if (panelRoot == null)
            return;

        RefreshTogglesFromSettings();
        panelRoot.SetActive(true);
        panelRoot.transform.SetAsLastSibling();
        isOpen = true;
        ApplyButtonVisibility();

        if (GameSpeedController.Instance != null)
            GameSpeedController.Instance.SetOverlayPaused(true);
    }

    public void Close()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        isOpen = false;
        ApplyButtonVisibility();

        if (GameSpeedController.Instance != null)
            GameSpeedController.Instance.SetOverlayPaused(false);
    }

    private void ApplyButtonVisibility()
    {
        bool showButton = isHudVisible && !isOpen;
        if (openButtonRoot != null)
            openButtonRoot.SetActive(showButton);
    }

    private void OnBgmToggleClicked()
    {
        GameAudioSettings.ToggleBgm();
        RefreshTogglesFromSettings();
    }

    private void OnSfxToggleClicked()
    {
        GameAudioSettings.ToggleSfx();
        RefreshTogglesFromSettings();
    }

    private void RefreshTogglesFromSettings()
    {
        ApplyToggleVisual(bgmToggleImage, GameAudioSettings.BgmEnabled);
        ApplyToggleVisual(sfxToggleImage, GameAudioSettings.SfxEnabled);
    }

    private void ApplyToggleVisual(Image image, bool enabled)
    {
        if (image == null)
            return;

        if (enabled)
        {
            if (onSprite != null)
                image.sprite = onSprite;
            SetFlipX(image.rectTransform, false);
        }
        else
        {
            if (offSprite != null)
                image.sprite = offSprite;
            SetFlipX(image.rectTransform, true);
        }
    }

    private static void SetFlipX(RectTransform rect, bool flip)
    {
        if (rect == null)
            return;

        Vector3 scale = rect.localScale;
        float absX = Mathf.Abs(scale.x) < 0.0001f ? 1f : Mathf.Abs(scale.x);
        scale.x = flip ? -absX : absX;
        rect.localScale = scale;
    }

    private void ResolveToggleImages()
    {
        if (bgmToggleImage == null && bgmToggleButton != null)
            bgmToggleImage = bgmToggleButton.GetComponent<Image>();
        if (sfxToggleImage == null && sfxToggleButton != null)
            sfxToggleImage = sfxToggleButton.GetComponent<Image>();
    }

    private void ResolveReferences()
    {
        Transform layout = null;
        GameObject layoutObject = GameObject.Find("UILayout");
        if (layoutObject != null)
            layout = layoutObject.transform;

        if (openButtonRoot == null && layout != null)
        {
            Transform found = layout.Find("Setting");
            if (found != null)
                openButtonRoot = found.gameObject;
        }

        if (panelRoot == null && layout != null)
        {
            Transform found = layout.Find("SettingsPanel");
            if (found != null)
                panelRoot = found.gameObject;
        }

        if (openButtonRoot != null && openButton == null)
            openButton = openButtonRoot.GetComponent<Button>();

        if (closeButton == null && panelRoot != null)
        {
            Transform found = panelRoot.transform.Find("Panel/CloseButton");
            if (found != null)
                closeButton = found.GetComponent<Button>();
        }

        if (bgmToggleButton == null && panelRoot != null)
        {
            Transform found = panelRoot.transform.Find("Panel/배경음Toggle");
            if (found != null)
                bgmToggleButton = found.GetComponent<Button>();
        }

        if (sfxToggleButton == null && panelRoot != null)
        {
            Transform found = panelRoot.transform.Find("Panel/효과음Toggle");
            if (found != null)
                sfxToggleButton = found.GetComponent<Button>();
        }
    }
}
