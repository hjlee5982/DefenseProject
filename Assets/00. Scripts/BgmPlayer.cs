using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 배경음악 재생기. 씬이 바뀌어도 이어지도록 DontDestroyOnLoad로 유지되는 싱글톤이다.
/// 타이틀 씬과 게임 씬에 각각 하나씩 두면(같은 트랙 연결), 먼저 만들어진 것만 남고 나머지는 사라진다.
///
/// 트랙 0: 시작 ~ 밤 이전 단계, 트랙 1: 밤 단계부터.
/// 켜짐/꺼짐은 GameAudioSettings.BgmEnabled를 따른다(꺼져 있으면 볼륨 0으로 계속 재생해서 다시 켜면 바로 들린다).
/// </summary>
public class BgmPlayer : MonoBehaviour
{
    [Tooltip("0번: 게임 시작 ~ 밤 이전 단계, 1번: 밤 단계부터.")]
    [SerializeField] private AudioClip[] tracks;
    [SerializeField, Range(0f, 1f)] private float volume = 0.6f;
    [Tooltip("트랙을 바꾸거나 멈출 때 페이드 시간(초).")]
    [SerializeField, Min(0f)] private float fadeSeconds = 0.8f;

    private AudioSource source;
    private int currentTrack = -1;
    private Coroutine routine;
    private float fade = 1f;

    public static BgmPlayer Instance { get; private set; }

    public int CurrentTrack => currentTrack;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;

        GameAudioSettings.Changed += ApplyVolume;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyVolume();
    }

    private void Start()
    {
        // 타이틀 씬에서 시작한 경우. 게임 씬은 GameManager가 스테이지에 맞는 트랙을 정한다.
        if (Instance == this && GameManager.Instance == null)
            PlayTrack(0);
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        GameAudioSettings.Changed -= ApplyVolume;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 타이틀(게임 매니저가 없는 씬)로 돌아오면 처음 BGM부터 다시 시작한다.
        if (GameManager.Instance == null)
            PlayTrack(0);
    }

    /// <summary>0: 밤 이전, 1: 밤. 이미 같은 트랙이 재생 중이면 아무것도 하지 않는다.</summary>
    public static void SetTrack(int index)
    {
        if (Instance != null)
            Instance.PlayTrack(index);
    }

    /// <summary>BGM을 서서히 멈춘다(게임오버/클리어 등).</summary>
    public static void Stop()
    {
        if (Instance != null)
            Instance.StopTrack();
    }

    private void PlayTrack(int index)
    {
        if (tracks == null || index < 0 || index >= tracks.Length || tracks[index] == null) return;
        if (index == currentTrack && (source.isPlaying || routine != null)) return;

        currentTrack = index;
        if (routine != null)
            StopCoroutine(routine);
        routine = StartCoroutine(SwitchRoutine(tracks[index]));
    }

    private void StopTrack()
    {
        currentTrack = -1;
        if (routine != null)
            StopCoroutine(routine);
        routine = StartCoroutine(StopRoutine());
    }

    private IEnumerator SwitchRoutine(AudioClip clip)
    {
        if (source.isPlaying)
        {
            yield return FadeTo(0f);
            source.Stop();
        }

        source.clip = clip;
        source.loop = true;
        fade = 0f;
        // 실제 재생 시작은 ApplyVolume이 맡는다(BGM이 꺼져 있으면 시작하지 않고, 켜지는 순간 시작한다).
        ApplyVolume();
        yield return FadeTo(1f);
        routine = null;
    }

    private IEnumerator StopRoutine()
    {
        yield return FadeTo(0f);
        source.Stop();
        routine = null;
    }

    private IEnumerator FadeTo(float target)
    {
        float start = fade;
        float elapsed = 0f;
        while (elapsed < fadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            fade = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / fadeSeconds));
            ApplyVolume();
            yield return null;
        }

        fade = target;
        ApplyVolume();
    }

    private void ApplyVolume()
    {
        if (source == null) return;

        source.volume = volume * fade;
        if (currentTrack < 0 || source.clip == null) return;

        // 볼륨 0(또는 mute)으로 시작한 소스는 재생이 시작되지 않거나 isPlaying이 거짓으로 나오는 경우가 있다.
        // 그래서 "꺼짐"은 볼륨이 아니라 재생 자체를 멈추는 것으로 처리하고, 켜지면 이 자리에서 다시 시작한다.
        if (GameAudioSettings.BgmEnabled)
        {
            if (!source.isPlaying && fade > 0.001f)
                source.Play();
        }
        else if (source.isPlaying)
        {
            source.Stop();
        }
    }
}
