using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnhanceStatusController : MonoBehaviour
{
    [SerializeField] private GameObject statusButtonRoot;
    [SerializeField] private Button statusButton;
    [SerializeField] private TextMeshProUGUI statusButtonLabel;
    [SerializeField] private GameObject statusPanel;
    [SerializeField] private Button closeButton;
    [SerializeField] private string buttonLabelText = "강화";

    public static EnhanceStatusController Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveReferences();

        if (statusButton != null)
        {
            statusButton.onClick.RemoveListener(TogglePanel);
            statusButton.onClick.AddListener(TogglePanel);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePanel);
            closeButton.onClick.AddListener(ClosePanel);
        }

        if (statusButtonLabel != null)
            statusButtonLabel.text = buttonLabelText;

        if (statusPanel != null)
            statusPanel.SetActive(false);

        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (statusButton != null)
            statusButton.onClick.RemoveListener(TogglePanel);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(ClosePanel);

        if (Instance == this)
        {
            if (GameSpeedController.Instance != null)
                GameSpeedController.Instance.SetOverlayPaused(false);

            Instance = null;
        }
    }

    public void SetVisible(bool active)
    {
        ResolveReferences();

        if (statusButtonRoot != null)
            statusButtonRoot.SetActive(active);

        if (!active)
            SetPanelOpen(false);
    }

    public void ClosePanel()
    {
        SetPanelOpen(false);
    }

    private void TogglePanel()
    {
        ResolveReferences();
        if (statusPanel == null) return;

        SetPanelOpen(!statusPanel.activeSelf);
    }

    // Opening the status window pauses the game; closing it resumes at the previously selected speed.
    private void SetPanelOpen(bool open)
    {
        if (statusPanel != null)
        {
            statusPanel.SetActive(open);
            if (open)
                statusPanel.transform.SetAsLastSibling();
        }

        if (GameSpeedController.Instance != null)
            GameSpeedController.Instance.SetOverlayPaused(open && statusPanel != null);
    }

    private void ResolveReferences()
    {
        Transform layout = null;
        GameObject layoutObject = GameObject.Find("UILayout");
        if (layoutObject != null)
            layout = layoutObject.transform;

        if (statusButtonRoot == null && layout != null)
        {
            Transform found = layout.Find("EnhanceStatusButton");
            if (found != null)
                statusButtonRoot = found.gameObject;
        }

        if (statusPanel == null && layout != null)
        {
            Transform found = layout.Find("EnhanceStatus");
            if (found != null)
                statusPanel = found.gameObject;
        }

        if (closeButton == null && statusPanel != null)
        {
            Transform found = statusPanel.transform.Find("ReturnButton");
            if (found != null)
                closeButton = found.GetComponent<Button>();
        }

        if (statusButtonRoot != null)
        {
            if (statusButton == null)
                statusButton = statusButtonRoot.GetComponent<Button>();
            if (statusButtonLabel == null)
                statusButtonLabel = statusButtonRoot.GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }
}
