using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonPressContentOffset : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private Vector2 pressedOffset = new Vector2(0f, -12f);
    [Tooltip("비워두면 버튼의 모든 자식 RectTransform을 이동합니다.")]
    [SerializeField] private RectTransform contentRoot;

    private Button button;
    private bool isPressed;
    private readonly List<RectTransform> targets = new();
    private readonly List<Vector2> basePositions = new();

    private void Awake()
    {
        button = GetComponent<Button>();
        CacheTargets();
    }

    private void OnDisable()
    {
        SetPressed(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (button != null && !button.IsInteractable()) return;
        SetPressed(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        SetPressed(false);
    }

    private void CacheTargets()
    {
        targets.Clear();
        basePositions.Clear();

        if (contentRoot != null)
        {
            targets.Add(contentRoot);
            basePositions.Add(contentRoot.anchoredPosition);
            return;
        }

        for (int i = 0; i < transform.childCount; i++)
        {
            RectTransform child = transform.GetChild(i) as RectTransform;
            if (child == null) continue;

            targets.Add(child);
            basePositions.Add(child.anchoredPosition);
        }
    }

    private void SetPressed(bool pressed)
    {
        if (isPressed == pressed) return;
        isPressed = pressed;
        ApplyOffset();
    }

    private void ApplyOffset()
    {
        for (int i = 0; i < targets.Count; i++)
        {
            RectTransform target = targets[i];
            if (target == null) continue;

            target.anchoredPosition = isPressed
                ? basePositions[i] + pressedOffset
                : basePositions[i];
        }
    }
}
