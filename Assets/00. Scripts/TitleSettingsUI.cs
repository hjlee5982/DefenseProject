using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이틀 씬 설정 패널. 배경음/효과음 켜고 끄기를 토글로 조절한다.
/// </summary>
public class TitleSettingsUI : MonoBehaviour
{
    [Header("Open")]
    [SerializeField] private Button openButton;

    [Header("Main UI")]
    [Tooltip("설정 패널이 열릴 때 숨길 타이틀 화면 UI.")]
    [SerializeField] private GameObject[] mainUiRoots;

    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button closeButton;

    [Header("Toggles")]
    [SerializeField] private Button bgmToggleButton;
    [SerializeField] private Image bgmToggleImage;
    [SerializeField] private Button sfxToggleButton;
    [SerializeField] private Image sfxToggleImage;

    [Header("Toggle Sprites")]
    [Tooltip("켜짐 상태 스프라이트 (UI_79).")]
    [SerializeField] private Sprite onSprite;
    [Tooltip("꺼짐 상태 스프라이트 (UI_78). 표시 시 좌우 반전한다.")]
    [SerializeField] private Sprite offSprite;

    private void Awake()
    {
        if (openButton != null)
            openButton.onClick.AddListener(Open);
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (bgmToggleButton != null)
            bgmToggleButton.onClick.AddListener(OnBgmToggleClicked);
        if (sfxToggleButton != null)
            sfxToggleButton.onClick.AddListener(OnSfxToggleClicked);

        ResolveToggleImages();

        if (panelRoot != null)
            panelRoot.SetActive(false);

        RefreshTogglesFromSettings();
    }

    private void OnEnable()
    {
        GameAudioSettings.Changed += RefreshTogglesFromSettings;
    }

    private void OnDisable()
    {
        GameAudioSettings.Changed -= RefreshTogglesFromSettings;
        if (panelRoot != null && panelRoot.activeSelf)
            Close();
    }

    public void Open()
    {
        RefreshTogglesFromSettings();
        SetMainUiVisible(false);
        if (panelRoot != null)
            panelRoot.SetActive(true);
    }

    public void Close()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
        SetMainUiVisible(true);
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

    private void SetMainUiVisible(bool visible)
    {
        if (mainUiRoots == null)
            return;

        for (int i = 0; i < mainUiRoots.Length; i++)
        {
            if (mainUiRoots[i] != null)
                mainUiRoots[i].SetActive(visible);
        }
    }
}
