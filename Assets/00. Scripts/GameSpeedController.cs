using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameSpeedController : MonoBehaviour
{
    private const float SpeedNormal = 1f;
    private const float SpeedFast = 2f;

    [SerializeField] private GameObject speedControlsRoot;
    [SerializeField] private Button speedButton;
    [SerializeField] private TextMeshProUGUI speedLabel;

    private float desiredSpeed = SpeedNormal;
    private bool simulationPaused;
    private bool overlayPaused;

    public static GameSpeedController Instance { get; private set; }
    public float DesiredSpeed => desiredSpeed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveReferences();

        if (speedButton != null)
        {
            speedButton.onClick.RemoveListener(ToggleSpeed);
            speedButton.onClick.AddListener(ToggleSpeed);
        }

        RefreshLabel();
        SetVisible(false);
        ApplyTimeScale();
    }

    private void OnDestroy()
    {
        if (speedButton != null)
            speedButton.onClick.RemoveListener(ToggleSpeed);

        if (Instance == this)
            Instance = null;

        if (!simulationPaused)
            Time.timeScale = 1f;
    }

    public void SetDesiredSpeed(float speed)
    {
        desiredSpeed = Mathf.Approximately(speed, SpeedFast) ? SpeedFast : SpeedNormal;
        RefreshLabel();
        ApplyTimeScale();
    }

    public void SetSimulationPaused(bool paused)
    {
        simulationPaused = paused;
        ApplyTimeScale();
    }

    // Independent pause source used by the status window, so it never clobbers the game flow's pause state.
    public void SetOverlayPaused(bool paused)
    {
        overlayPaused = paused;
        ApplyTimeScale();
    }

    public void ApplyTimeScale()
    {
        Time.timeScale = (simulationPaused || overlayPaused) ? 0f : desiredSpeed;
    }

    public void SetVisible(bool active)
    {
        ResolveReferences();
        if (speedControlsRoot != null)
            speedControlsRoot.SetActive(active);
    }

    private void ResolveReferences()
    {
        if (speedControlsRoot == null)
        {
            Transform layout = null;
            GameObject layoutObject = GameObject.Find("UILayout");
            if (layoutObject != null)
                layout = layoutObject.transform;

            if (layout != null)
            {
                Transform found = layout.Find("SpeedControls");
                if (found != null)
                    speedControlsRoot = found.gameObject;
            }
        }

        if (speedControlsRoot == null) return;

        if (speedButton == null)
            speedButton = speedControlsRoot.GetComponent<Button>();

        if (speedLabel == null)
            speedLabel = speedControlsRoot.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void ToggleSpeed()
    {
        SetDesiredSpeed(Mathf.Approximately(desiredSpeed, SpeedFast) ? SpeedNormal : SpeedFast);
    }

    private void RefreshLabel()
    {
        if (speedLabel != null)
            speedLabel.text = Mathf.Approximately(desiredSpeed, SpeedFast) ? "x2" : "x1";
    }
}
