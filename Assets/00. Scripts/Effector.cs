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

    private static readonly int ArrivedHash = Animator.StringToHash("Arrived");
    private const string MoveStateName = "Move";
    private const string EndStateName = "End";

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        image = GetComponent<Image>();
        animator = GetComponent<Animator>();
        useAnimator = (sprites == null || sprites.Length == 0) && animator != null;
    }

    private void OnEnable()
    {
        if (!playOnEnable || ending) return;

        if (useAnimator) PlayMoveLoop();
        else Play();
    }

    public bool UseAnimator => useAnimator;

    public void Play()
    {
        if (useAnimator || ending) return;
        if (sprites == null || sprites.Length == 0) return;

        elapsed = 0f;
        playing = true;
        ApplySprite(0);
    }

    public void PlayMoveLoop()
    {
        if (!useAnimator || animator == null || ending) return;

        animator.ResetTrigger(ArrivedHash);
        animator.Play(MoveStateName, 0, 0f);
    }

    public void PlayEnd()
    {
        if (ending) return;
        ending = true;

        if (!useAnimator || animator == null)
        {
            Destroy(gameObject);
            return;
        }

        animator.ResetTrigger(ArrivedHash);
        animator.Play(EndStateName, 0, 0f);
        StartCoroutine(DestroyAfterEndAnimation());
    }

    private IEnumerator DestroyAfterEndAnimation()
    {
        yield return null;

        float timeout = 3f;
        while (timeout > 0f)
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.IsName(EndStateName) && info.normalizedTime >= 1f)
                break;

            timeout -= Time.deltaTime;
            yield return null;
        }

        animator.enabled = false;
        Destroy(gameObject);
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
                if (destroyWhenFinished) Destroy(gameObject);
                return;
            }
        }

        int index = Mathf.Min(sprites.Length - 1, Mathf.FloorToInt(normalized * sprites.Length));
        ApplySprite(index);
    }

    private void ApplySprite(int index)
    {
        Sprite sprite = sprites[index];
        if (spriteRenderer != null) spriteRenderer.sprite = sprite;
        if (image != null) image.sprite = sprite;
    }
}
