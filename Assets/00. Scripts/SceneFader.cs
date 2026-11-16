using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 씬 전환 페이드. 화면이 검게 어두워진 뒤 씬을 불러오고, 새 씬이 뜨면 다시 밝아진다.
/// 씬에 따로 둘 필요 없이 게임이 시작될 때 자동으로 만들어지고, 씬이 바뀌어도 유지된다.
/// 전환은 SceneManager.LoadScene 대신 SceneFader.Load를 쓴다.
/// </summary>
public class SceneFader : MonoBehaviour
{
    private const float FadeOutSeconds = 0.35f;
    private const float FadeInSeconds = 0.45f;
    private static readonly Color FadeColor = Color.black;

    private CanvasGroup group;
    private Coroutine routine;
    private bool loading;

    public static SceneFader Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    private static SceneFader EnsureInstance()
    {
        if (Instance != null) return Instance;

        var go = new GameObject("SceneFader");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<SceneFader>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildOverlay();
        SceneManager.sceneLoaded += OnSceneLoaded;

        // 게임을 켜서 처음 뜨는 씬도 검은 화면에서 밝아지게 한다.
        group.alpha = 1f;
        PlayFadeIn();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    /// <summary>페이드 아웃 → 씬 로드 → 페이드 인. 전환 중에 다시 부르면 무시한다.</summary>
    public static void Load(string sceneName)
    {
        EnsureInstance().BeginLoad(sceneName);
    }

    private void BeginLoad(string sceneName)
    {
        if (loading) return;

        loading = true;
        if (routine != null)
            StopCoroutine(routine);
        routine = StartCoroutine(LoadRoutine(sceneName));
    }

    private IEnumerator LoadRoutine(string sceneName)
    {
        group.blocksRaycasts = true; // 전환 중에는 버튼을 눌러도 반응하지 않게 한다.
        yield return Fade(group.alpha, 1f, FadeOutSeconds);

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            // 불러올 수 없는 씬이면 화면만 원래대로 돌린다.
            loading = false;
            yield return Fade(1f, 0f, FadeInSeconds);
            group.blocksRaycasts = false;
            routine = null;
            yield break;
        }

        while (!operation.isDone)
            yield return null;
        // 이후는 OnSceneLoaded에서 페이드 인을 시작한다.
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        loading = false;
        PlayFadeIn();
    }

    private void PlayFadeIn()
    {
        if (routine != null)
            StopCoroutine(routine);
        routine = StartCoroutine(FadeInRoutine());
    }

    private IEnumerator FadeInRoutine()
    {
        // 새 씬의 첫 프레임(무거운 Awake/Start)이 지나간 뒤에 밝아지게 한다.
        yield return null;
        yield return Fade(group.alpha, 0f, FadeInSeconds);
        group.blocksRaycasts = false;
        routine = null;
    }

    private IEnumerator Fade(float from, float to, float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            // 게임오버 등으로 timeScale이 0이어도 페이드는 진행되어야 한다.
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds));
            yield return null;
        }

        group.alpha = to;
    }

    private void BuildOverlay()
    {
        var canvasObject = new GameObject("FadeCanvas");
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        canvasObject.AddComponent<GraphicRaycaster>();

        group = canvasObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        var imageObject = new GameObject("Fade");
        imageObject.transform.SetParent(canvasObject.transform, false);
        var image = imageObject.AddComponent<Image>();
        image.color = FadeColor;
        image.raycastTarget = true;
        var rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
