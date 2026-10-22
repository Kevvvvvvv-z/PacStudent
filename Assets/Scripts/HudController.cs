using UnityEngine;
using UnityEngine.UI;

public class HudController : MonoBehaviour
{
    [SerializeField] private Text scoreText;
    [SerializeField] private Text timerText;
    [SerializeField] private Text scaredTimerText;
    [SerializeField] private Image[] lifeIcons;
    [SerializeField] private Button exitButton;
    [SerializeField] private GameObject overlay;
    [SerializeField] private Text overlayText;

    private void LateUpdate()
    {
        var manager = GameManager.Instance;
        if (manager == null) return;
        scoreText.text = manager.Score.ToString("D6");
        int hundredths = Mathf.FloorToInt(manager.ElapsedTime * 100f);
        timerText.text = $"{hundredths / 6000:00}:{hundredths / 100 % 60:00}:{hundredths % 100:00}";
        for (int i = 0; i < lifeIcons.Length; i++) lifeIcons[i].enabled = i < manager.Lives;
        bool scared = manager.IsPowerActive && manager.PowerTimeRemaining > 0f;
        scaredTimerText.gameObject.SetActive(scared);
        if (scared) scaredTimerText.text = "GHOSTS SCARED  " + Mathf.CeilToInt(manager.PowerTimeRemaining);
        bool countdown = manager.State == GameManager.GameState.Countdown;
        overlay.SetActive(countdown || manager.IsGameOver);
        overlayText.text = countdown ? manager.CountdownText : manager.IsGameOver ? "GAME OVER" : "";
        exitButton.interactable = !countdown && !manager.IsGameOver;
    }
}
