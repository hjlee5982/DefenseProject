using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameSpeedController : MonoBehaviour
{
    private static readonly float[] SpeedSteps = { 1f, 2f, 3f };

    [SerializeField] private GameObject speedControlsRoot;
    [SerializeField] private Button speedButton;
    [SerializeField] private TextMeshProUGUI speedLabel;

    private float desiredSpeed = SpeedSteps[0];
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
        desiredSpeed = NormalizeSpeed(speed);
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
        int index = IndexOfSpeed(desiredSpeed);
        int next = (index + 1) % SpeedSteps.Length;
        SetDesiredSpeed(SpeedSteps[next]);
    }

    private void RefreshLabel()
    {
        if (speedLabel == null)
            return;

        speedLabel.text = $"x{Mathf.RoundToInt(desiredSpeed)}";
    }

    private static float NormalizeSpeed(float speed)
    {
        int index = IndexOfSpeed(speed);
        return index >= 0 ? SpeedSteps[index] : SpeedSteps[0];
    }

    private static int IndexOfSpeed(float speed)
    {
        for (int i = 0; i < SpeedSteps.Length; i++)
        {
            if (Mathf.Approximately(speed, SpeedSteps[i]))
                return i;
        }

        return -1;
    }
}
