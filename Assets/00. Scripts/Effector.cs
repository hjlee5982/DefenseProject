using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Effector : MonoBehaviour
{
    [SerializeField] private Sprite[] sprites;
    [SerializeField] private float duration = 0.5f;
    [SerializeField] private bool loop;
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool destroyWhenFinished;

    private SpriteRenderer spriteRenderer;
    private Image image;
    private Animator animator;

    private float elapsed;
    private bool playing;
    private bool useAnimator;
    private bool ending;
    private bool resolved;

    private static readonly int ArrivedHash = Animator.StringToHash("Arrived");
    private const string MoveStateName = "Move";
    private const string EndStateName = "End";

    private void Awake()
    {
        ResolveVisualComponents();
        SuppressChildEffectors();
        useAnimator = (sprites == null || sprites.Length == 0) && animator != null;
    }

    private void OnEnable()
    {
        if (!playOnEnable || ending) return;

        if (useAnimator) PlayMoveLoop();
        else Play();
    }

    public bool UseAnimator
    {
        get
        {
            EnsureResolved();
            return useAnimator;
        }
    }

    public void Play()
    {
        EnsureResolved();
        if (useAnimator || ending) return;
        if (sprites == null || sprites.Length == 0) return;

        enabled = true;
        elapsed = 0f;
        playing = true;
        ApplySprite(0);
    }

    public void PlayMoveLoop()
    {
        EnsureResolved();
        if (!useAnimator || animator == null || ending) return;

        enabled = true;
        animator.ResetTrigger(ArrivedHash);
        animator.Play(MoveStateName, 0, 0f);
    }

    public bool IsPlaying
    {
        get
        {
            EnsureResolved();
            return useAnimator
                ? (animator != null && animator.enabled && !HasAnimatorClipFinished())
                : playing;
        }
    }

    public void PlayOnce(bool destroyWhenDone = true)
    {
        EnsureResolved();
        if (useAnimator)
        {
            PlayEnd(destroyWhenDone);
            return;
        }

        if (sprites == null || sprites.Length == 0) return;

        enabled = true;
        elapsed = 0f;
        playing = true;
        loop = false;
        destroyWhenFinished = destroyWhenDone;
        ApplySprite(0);
    }

    public void PlayEnd(bool destroyWhenDone = true)
    {
        if (ending) return;
        ending = true;
        EnsureResolved();

        if (!useAnimator || animator == null)
        {
            if (destroyWhenDone)
                DestroyEffectRoot();
            return;
        }

        enabled = true;
        animator.ResetTrigger(ArrivedHash);
        animator.Play(EndStateName, 0, 0f);
        if (destroyWhenDone)
            StartCoroutine(DestroyAfterEndAnimation());
    }

    public IEnumerator WaitUntilFinished(float timeout = 5f)
    {
        yield return null;

        float remaining = timeout;
        while (remaining > 0f)
        {
            if (this == null) yield break;

            if (useAnimator)
            {
                if (HasAnimatorClipFinished())
                    yield break;
            }
            else if (!playing)
            {
                yield break;
            }

            remaining -= Time.deltaTime;
            yield return null;
        }
    }

    private void EnsureResolved()
    {
        if (resolved) return;
        ResolveVisualComponents();
        SuppressChildEffectors();
        useAnimator = (sprites == null || sprites.Length == 0) && animator != null;
        resolved = true;
    }

    private void ResolveVisualComponents()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null || !spriteRenderer.enabled)
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].enabled)
                {
                    spriteRenderer = renderers[i];
                    break;
                }
            }

            if (spriteRenderer == null && renderers.Length > 0)
                spriteRenderer = renderers[0];
        }

        image = GetComponent<Image>();
        if (image == null)
            image = GetComponentInChildren<Image>(true);

        animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        resolved = true;
    }

    private void SuppressChildEffectors()
    {
        // Nested effect prefabs may have Effector on both root and visual child.
        // Only the outermost Effector should drive playback/destruction.
        if (transform.parent != null && transform.parent.GetComponent<Effector>() != null)
            return;

        Effector[] nested = GetComponentsInChildren<Effector>(true);
        for (int i = 0; i < nested.Length; i++)
        {
            if (nested[i] != null && nested[i] != this)
                nested[i].enabled = false;
        }
    }

    private bool HasAnimatorClipFinished()
    {
        if (animator == null) return true;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        return info.IsName(EndStateName) && info.normalizedTime >= 1f && !animator.IsInTransition(0);
    }

    private IEnumerator DestroyAfterEndAnimation()
    {
        yield return WaitUntilFinished(3f);

        if (animator != null)
            animator.enabled = false;
        DestroyEffectRoot();
    }

    private void Update()
    {
        if (useAnimator || ending) return;
        if (!playing) return;
        if (sprites == null || sprites.Length == 0) return;

        elapsed += Time.deltaTime;

        float totalDuration = Mathf.Max(0.0001f, duration);
        float normalized = elapsed / totalDuration;

        if (normalized >= 1f)
        {
            if (loop)
            {
                elapsed %= totalDuration;
                normalized = elapsed / totalDuration;
            }
            else
            {
                ApplySprite(sprites.Length - 1);
                playing = false;
                if (destroyWhenFinished) DestroyEffectRoot();
                return;
            }
        }

        int index = Mathf.Min(sprites.Length - 1, Mathf.FloorToInt(normalized * sprites.Length));
        ApplySprite(index);
    }

    private void ApplySprite(int index)
    {
        if (sprites == null || index < 0 || index >= sprites.Length) return;

        Sprite sprite = sprites[index];
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            spriteRenderer.sprite = sprite;
        }

        if (image != null)
        {
            image.enabled = true;
            image.sprite = sprite;
        }
    }

    private void DestroyEffectRoot()
    {
        Transform root = transform;
        while (root.parent != null && root.parent.GetComponent<Effector>() != null)
            root = root.parent;

        Destroy(root.gameObject);
    }
}
