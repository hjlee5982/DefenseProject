using UnityEngine;
using UnityEngine.UI;

public class TitleController : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private string gameSceneName = "GameScene";

    private void Awake()
    {
        startButton.onClick.AddListener(LoadGameScene);
    }

    private void LoadGameScene()
    {
        SceneFader.Load(gameSceneName);
    }
}
