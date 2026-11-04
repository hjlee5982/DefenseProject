using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 버튼 클릭 SFX. 씬에 하나 두고 모든 Button.onClick에 연결한다.
/// </summary>
public class UiClickSfx : MonoBehaviour
{
    [SerializeField] private AudioClip clickClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
    [SerializeField, Min(0f)] private float minInterval = 0.03f;
    [SerializeField, Min(1)] private int voiceCount = 4;

    private AudioSource[] voices;
    private int nextVoice;
    private float lastPlayTime = float.NegativeInfinity;

    public static UiClickSfx Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        EnsureVoices();
        BindAllButtonsInScene();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static void Play()
    {
        if (Instance != null)
            Instance.PlayInternal();
    }

    public void PlayInternal()
    {
        if (clickClip == null)
            return;

        if (GameAudioSettings.SfxVolume <= 0f)
            return;

        float now = Time.unscaledTime;
        if (now - lastPlayTime < minInterval)
            return;

        lastPlayTime = now;
        EnsureVoices();

        AudioSource source = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;
        source.Stop();
        source.clip = clickClip;
        source.volume = volume * GameAudioSettings.SfxVolume;
        source.pitch = 1f;
        source.Play();
    }

    private void EnsureVoices()
    {
        if (voices != null && voices.Length == voiceCount)
            return;

        voices = new AudioSource[voiceCount];
        for (int i = 0; i < voices.Length; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            voices[i] = source;
        }
    }

    private void BindAllButtonsInScene()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
                continue;

            // 중복 등록 방지: 동일 인스턴스 메서드는 Remove 후 Add.
            button.onClick.RemoveListener(PlayInternal);
            button.onClick.AddListener(PlayInternal);
        }
    }

    /// <summary>런타임에 생성된 버튼용.</summary>
    public static void Bind(Button button)
    {
        if (button == null || Instance == null)
            return;

        button.onClick.RemoveListener(Instance.PlayInternal);
        button.onClick.AddListener(Instance.PlayInternal);
    }
}
