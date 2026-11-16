using UnityEngine;

/// <summary>
/// 게임 씬의 일반 효과음: 아이템 집기, 합성(단계별), 게임오버, 게임 클리어.
/// 씬에 하나 두고 클립을 연결한다. 켜짐/꺼짐은 GameAudioSettings.SfxEnabled를 따른다.
/// </summary>
public class GameSfx : MonoBehaviour
{
    [Header("Item")]
    [Tooltip("상점/가방에서 아이템을 집을 때.")]
    [SerializeField] private AudioClip pick;
    [SerializeField, Range(0f, 1f)] private float pickVolume = 0.7f;
    [SerializeField, Min(0f)] private float minPickInterval = 0.05f;
    [Tooltip("아이템을 가방에 배치하거나 제자리로 되돌릴 때.")]
    [SerializeField] private AudioClip drop;
    [SerializeField, Range(0f, 1f)] private float dropVolume = 0.7f;
    [SerializeField, Min(0f)] private float minDropInterval = 0.05f;

    [Header("Merge")]
    [Tooltip("2~3단계 합성.")]
    [SerializeField] private AudioClip merge1;
    [Tooltip("4단계 합성.")]
    [SerializeField] private AudioClip merge2;
    [Tooltip("5단계 합성.")]
    [SerializeField] private AudioClip merge3;
    [SerializeField, Range(0f, 1f)] private float mergeVolume = 0.9f;

    [Header("Result")]
    [SerializeField] private AudioClip gameOver;
    [SerializeField] private AudioClip clear;
    [SerializeField, Range(0f, 1f)] private float resultVolume = 1f;

    [SerializeField, Min(1)] private int voiceCount = 6;

    private AudioSource[] voices;
    private int nextVoice;
    private float lastPickTime = float.NegativeInfinity;
    private float lastDropTime = float.NegativeInfinity;

    public static GameSfx Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        voices = new AudioSource[voiceCount];
        for (int i = 0; i < voices.Length; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            // 게임오버/클리어 때 시간이 멈춰도 소리는 나야 한다.
            source.ignoreListenerPause = true;
            voices[i] = source;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static void PlayPick()
    {
        if (Instance != null)
            Instance.PlayPickInternal();
    }

    public static void PlayDrop()
    {
        if (Instance != null)
            Instance.PlayDropInternal();
    }

    /// <summary>합성으로 만들어진 아이템의 단계(2~5)에 맞는 소리를 낸다.</summary>
    public static void PlayMerge(int resultGrade)
    {
        if (Instance != null)
            Instance.PlayMergeInternal(resultGrade);
    }

    public static void PlayGameOver()
    {
        if (Instance != null)
            Instance.Play(Instance.gameOver, Instance.resultVolume);
    }

    public static void PlayClear()
    {
        if (Instance != null)
            Instance.Play(Instance.clear, Instance.resultVolume);
    }

    private void PlayPickInternal()
    {
        float now = Time.unscaledTime;
        if (now - lastPickTime < minPickInterval) return;
        lastPickTime = now;

        Play(pick, pickVolume);
    }

    private void PlayDropInternal()
    {
        float now = Time.unscaledTime;
        if (now - lastDropTime < minDropInterval) return;
        lastDropTime = now;

        Play(drop, dropVolume);
    }

    private void PlayMergeInternal(int resultGrade)
    {
        AudioClip clip;
        if (resultGrade >= 5) clip = merge3;
        else if (resultGrade == 4) clip = merge2;
        else clip = merge1;

        Play(clip, mergeVolume);
    }

    private void Play(AudioClip clip, float volume)
    {
        if (clip == null || voices == null) return;
        if (GameAudioSettings.SfxVolume <= 0f) return;

        AudioSource source = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;
        source.Stop();
        source.clip = clip;
        source.volume = volume * GameAudioSettings.SfxVolume;
        source.pitch = 1f;
        source.Play();
    }
}
