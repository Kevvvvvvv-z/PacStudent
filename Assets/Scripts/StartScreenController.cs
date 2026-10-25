using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartScreenController : MonoBehaviour
{
    [SerializeField] private RectTransform border;
    [SerializeField] private RectTransform[] borderDots;
    [SerializeField] private Text levelTwoStatus;
    [SerializeField] private AudioSource effects;
    [SerializeField] private AudioClip clickSound;
    [SerializeField] private string levelOneScene = "Level01";
    [SerializeField] private string levelTwoScene = "InnovationScene";
    [SerializeField] private Text level1Score, level1Time, level2Score, level2Time;
    [SerializeField, Min(1f)] private float borderSpeed = 90f;
    private float travelled;
    private bool loading;
    private void Start()
    {
        ShowRecord("Level01", level1Score, level1Time);
        ShowRecord("InnovationScene", level2Score, level2Time);
    }
    private static void ShowRecord(string level, Text score, Text time)
    {
        if (score != null) score.text = "BEST  " + ScoreStore.BestScore(level).ToString("D6");
        if (time != null) time.text = "TIME  " + ScoreStore.FormatTime(ScoreStore.BestTime(level));
    }

    private void Update()
    {
        if (border == null || borderDots == null || borderDots.Length == 0) return;
        float width = border.rect.width;
        float height = border.rect.height;
        float perimeter = 2f * (width + height);
        if (perimeter <= 0f) return;

        travelled = Mathf.Repeat(travelled + borderSpeed * Time.unscaledDeltaTime, perimeter);
        for (int i = 0; i < borderDots.Length; i++)
        {
            float distance = Mathf.Repeat(travelled + i * perimeter / borderDots.Length, perimeter);
            Vector2 position;
            if (distance < width) position = new Vector2(distance, height);
            else if (distance < width + height) position = new Vector2(width, height - (distance - width));
            else if (distance < 2f * width + height) position = new Vector2(width - (distance - width - height), 0f);
            else position = new Vector2(0f, distance - 2f * width - height);
            borderDots[i].anchoredPosition = position;
        }
    }

    public void OpenLevelOne()
    {
        if (loading) return;
        loading = true;
        if (effects != null && clickSound != null) effects.PlayOneShot(clickSound);
        // Keep the menu alive briefly so the button feedback is audible.
        Invoke(nameof(LoadLevelOne), 0.15f);
    }

    private void LoadLevelOne() => SceneManager.LoadScene(levelOneScene);

    public void PreviewLevelTwo()
    {
        if (loading) return;
        loading = true;
        if (effects != null && clickSound != null) effects.PlayOneShot(clickSound);
        Invoke(nameof(LoadLevelTwo), .15f);
    }
    private void LoadLevelTwo() => SceneManager.LoadScene(levelTwoScene);
}
