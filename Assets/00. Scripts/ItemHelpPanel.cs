using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상점 단계의 아이템 도움말 패널. 상점 우상단의 작은 버튼으로 열고, 닫기 버튼으로 닫는다.
/// 닫기 버튼 좌우의 화살표 버튼으로 페이지를 넘긴다(1: 아이템 합치기 설명, 2: 아이템 목록).
/// 이 컴포넌트는 도움말 열기 버튼(상점 하위)에 붙는다. 상점이 숨겨지면 패널도 함께 닫힌다.
/// 도움말이 열려 있는 동안 지정된 다른 UI 패널은 숨긴다.
/// </summary>
public class ItemHelpPanel : MonoBehaviour
{
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private GameObject panel;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Pages")]
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private GameObject[] pages;
    [SerializeField] private string[] pageTitles;

    [Header("Hide While Open")]
    [Tooltip("도움말이 열려 있는 동안 SetActive(false) 할 UI. HelpButton 조상은 넣지 않는다.")]
    [SerializeField] private GameObject[] hideWhenOpen;

    [Tooltip("SetActive 대신 enabled만 끌 그래픽(상점/Main 배경 등). HelpButton 조상 오브젝트용.")]
    [SerializeField] private Behaviour[] disableWhenOpen;

    private int pageIndex;
    private bool hidingOthers;
    private readonly List<GameObject> hiddenObjects = new();
    private readonly List<bool> hiddenWasActive = new();
    private readonly List<Behaviour> disabledBehaviours = new();
    private readonly List<bool> disabledWasEnabled = new();

    private void Awake()
    {
        if (openButton == null) openButton = GetComponent<Button>();
        if (openButton != null) openButton.onClick.AddListener(Open);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (prevButton != null) prevButton.onClick.AddListener(() => ShowPage(pageIndex - 1));
        if (nextButton != null) nextButton.onClick.AddListener(() => ShowPage(pageIndex + 1));
        if (panel != null) panel.SetActive(false);
    }

    private void OnDisable()
    {
        Close();
    }

    public void Open()
    {
        if (panel == null) return;

        HideOthers();
        panel.SetActive(true);
        ShowPage(0);
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);
        RestoreOthers();
    }

    private void HideOthers()
    {
        if (hidingOthers)
            return;

        hiddenObjects.Clear();
        hiddenWasActive.Clear();
        disabledBehaviours.Clear();
        disabledWasEnabled.Clear();

        if (hideWhenOpen != null)
        {
            for (int i = 0; i < hideWhenOpen.Length; i++)
            {
                GameObject target = hideWhenOpen[i];
                if (target == null || IsProtected(target))
                    continue;

                RememberAndHide(target);
            }
        }

        if (disableWhenOpen != null)
        {
            for (int i = 0; i < disableWhenOpen.Length; i++)
            {
                Behaviour behaviour = disableWhenOpen[i];
                if (behaviour == null)
                    continue;

                RememberAndDisable(behaviour);
            }
        }

        hidingOthers = hiddenObjects.Count > 0 || disabledBehaviours.Count > 0;
    }

    private void RestoreOthers()
    {
        if (!hidingOthers)
            return;

        for (int i = 0; i < hiddenObjects.Count; i++)
        {
            GameObject target = hiddenObjects[i];
            if (target != null)
                target.SetActive(hiddenWasActive[i]);
        }

        for (int i = 0; i < disabledBehaviours.Count; i++)
        {
            Behaviour behaviour = disabledBehaviours[i];
            if (behaviour != null)
                behaviour.enabled = disabledWasEnabled[i];
        }

        hiddenObjects.Clear();
        hiddenWasActive.Clear();
        disabledBehaviours.Clear();
        disabledWasEnabled.Clear();
        hidingOthers = false;
    }

    private void RememberAndHide(GameObject target)
    {
        for (int i = 0; i < hiddenObjects.Count; i++)
        {
            if (hiddenObjects[i] == target)
                return;
        }

        hiddenObjects.Add(target);
        hiddenWasActive.Add(target.activeSelf);
        if (target.activeSelf)
            target.SetActive(false);
    }

    private void RememberAndDisable(Behaviour behaviour)
    {
        for (int i = 0; i < disabledBehaviours.Count; i++)
        {
            if (disabledBehaviours[i] == behaviour)
                return;
        }

        disabledBehaviours.Add(behaviour);
        disabledWasEnabled.Add(behaviour.enabled);
        if (behaviour.enabled)
            behaviour.enabled = false;
    }

    private bool IsProtected(GameObject target)
    {
        if (target == null)
            return true;

        if (target == gameObject || target == panel)
            return true;

        if (panel != null && target.transform.IsChildOf(panel.transform))
            return true;

        // HelpButton(이 컴포넌트)이 꺼지면 OnDisable → Close가 즉시 호출되므로 조상도 숨기지 않는다.
        if (transform.IsChildOf(target.transform))
            return true;

        return false;
    }

    private void ShowPage(int index)
    {
        int count = pages != null ? pages.Length : 0;
        if (count == 0) return;

        pageIndex = Mathf.Clamp(index, 0, count - 1);

        for (int i = 0; i < count; i++)
        {
            if (pages[i] != null) pages[i].SetActive(i == pageIndex);
        }

        if (titleText != null && pageTitles != null && pageIndex < pageTitles.Length)
            titleText.text = pageTitles[pageIndex];

        if (prevButton != null) prevButton.interactable = pageIndex > 0;
        if (nextButton != null) nextButton.interactable = pageIndex < count - 1;

        // 스크롤 목록 페이지가 열릴 때마다 맨 위부터 보이게 한다.
        if (scrollRect != null && pages[pageIndex] == scrollRect.gameObject)
            scrollRect.verticalNormalizedPosition = 1f;
    }
}
