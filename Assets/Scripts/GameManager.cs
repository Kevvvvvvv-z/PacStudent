using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    public enum GameState
    {
        Waiting,
        Countdown,
        Playing,
        Dying,
        Won,
        Lost
    }

    [Header("Actors")]
    [SerializeField] private PacStudentController player;
    [SerializeField] private GhostController[] ghosts;

    [Header("Rules")]
    [SerializeField, Min(1)] private int startingLives = 3;
    [SerializeField, Min(1f)] private float powerDuration = 10f;
    [SerializeField, Min(0.1f)] private float collisionDistance = 0.48f;

    [SerializeField] private CherryController cherries;

    private GameState state;
    private int score;
    private int lives;
    private float powerEndTime;
    private string countdownText;
    private float elapsedTime;
    private int globalGhostPhase;
    public string LevelKey => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
    public bool HasDecoy => DecoyAbility.Instance != null && DecoyAbility.Instance.IsActive;
    public Vector2 GhostTargetPosition => HasDecoy ? DecoyAbility.Instance.Target : (Vector2)player.transform.position;

    public static GameManager Instance { get; private set; }
    public bool ActorsCanMove => state == GameState.Playing;
    public bool IsPowerActive => state == GameState.Playing && Time.time < powerEndTime;
    public float PowerTimeRemaining => Mathf.Max(0f, powerEndTime - Time.time);
    public PacStudentController Player => player;
    public GameState State => state;
    public int Score => score;
    public int Lives => lives;
    public float ElapsedTime => elapsedTime;
    public string CountdownText => countdownText;
    public bool IsGameOver => state == GameState.Won || state == GameState.Lost;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<PacStudentController>();
        }

        if (ghosts == null || ghosts.Length == 0)
        {
            ghosts = FindObjectsByType<GhostController>();
        }

        lives = startingLives;
        PrepareActors();
        StartCoroutine(StartCountdown());
    }

    private void Update()
    {
        ReadStateInput();
        if (state == GameState.Playing || state == GameState.Dying) elapsedTime += Time.deltaTime;

        if (state != GameState.Playing)
        {
            return;
        }

        if (!IsPowerActive && powerEndTime > 0f)
        {
            powerEndTime = 0f;

        }
        int ghostPhase = !IsPowerActive ? 0 : PowerTimeRemaining <= 3f ? 2 : 1;
        if (ghostPhase != globalGhostPhase)
        {
            globalGhostPhase = ghostPhase;
            foreach (var ghost in ghosts) if (ghost != null) ghost.RefreshStateAnimation();
        }

        UpdateMusic();
        CheckGhostCollisions();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Configure(PacStudentController assignedPlayer, GhostController[] assignedGhosts, Sprite assignedBonusFruit)
    {
        player = assignedPlayer;
        ghosts = assignedGhosts;
    }

    private IEnumerator StartCountdown()
    {
        state = GameState.Countdown;
        AudioManager.Instance?.PlayIntro(4f);
        foreach (string value in new[] { "3", "2", "1", "GO!" })
        {
            countdownText = value;
            yield return new WaitForSeconds(1f);
        }
        state = GameState.Waiting;
        BeginGame();
    }

    public void BeginGame()
    {
        if (state != GameState.Waiting)
        {
            return;
        }

        state = GameState.Playing;
        player?.BeginRound();
        AudioManager.Instance?.FinishIntro();
        UpdateMusic();
    }

    public void RestartGame()
    {
        DecoyAbility.Instance?.ResetRound();
        StopAllCoroutines();
        cherries?.ResetRound();
        score = 0;
        elapsedTime = 0f;
        lives = startingLives;
        powerEndTime = 0f;


        LevelGenerator.Instance?.GenerateLevel();
        PrepareActors();
        StartCoroutine(StartCountdown());
        AudioManager.Instance?.SetGhostMode(AudioManager.GhostMusicMode.Normal);
    }

    public void NotifyPelletCollected(bool isPowerPellet)
    {
        if (state != GameState.Playing)
        {
            return;
        }

        score += isPowerPellet ? 50 : 10;
        AudioManager.Instance?.PlayPellet();

        if (isPowerPellet)
        {
            powerEndTime = Time.time + powerDuration;

            foreach (GhostController ghost in ghosts)
            {
                if (ghost != null && !ghost.IsDead)
                {
                    ghost.RefreshStateAnimation();
                }
            }
        }

        int remaining = LevelGenerator.Instance == null ? 0 : LevelGenerator.Instance.RemainingPellets;
        if (remaining == 0)
        {
            state = GameState.Won;
            SaveResult();
            StartCoroutine(ReturnAfterGameOver());
            AudioManager.Instance?.SetGhostMode(AudioManager.GhostMusicMode.Normal);
            return;
        }

    }

    public void NotifyWallHit()
    {
        if (state == GameState.Playing)
        {
            AudioManager.Instance?.PlayWallHit();
        }
    }

    private void ReadStateInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            ExitToMenu();
            return;
        }

        if (state == GameState.Waiting &&
            (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
        {
            BeginGame();
        }
    }

    private void CheckGhostCollisions()
    {
        if (player == null || ghosts == null)
        {
            return;
        }

        foreach (GhostController ghost in ghosts)
        {
            if (ghost == null || ghost.IsDead)
            {
                continue;
            }

            if (Vector3.Distance(player.transform.position, ghost.transform.position) > collisionDistance)
            {
                continue;
            }

            if (ghost.IsFrightened)
            {
                score += 300;
                ghost.BecomeDead();
                AudioManager.Instance?.PlayGhostEaten();
            }
            else
            {
                StartCoroutine(HandlePlayerDeath());
                return;
            }
        }
    }

    private IEnumerator HandlePlayerDeath()
    {
        state = GameState.Dying;
        lives--;
        powerEndTime = 0f;

        cherries?.ResetRound();
        player?.PlayDeathAnimation();
        AudioManager.Instance?.PlayDeath();
        yield return new WaitForSeconds(player == null ? 1f : player.DeathDuration);

        if (lives <= 0)
        {
            state = GameState.Lost;
            SaveResult();
            StartCoroutine(ReturnAfterGameOver());
        }
        else
        {
            PrepareActors();
            state = GameState.Playing;
        }

        AudioManager.Instance?.SetGhostMode(AudioManager.GhostMusicMode.Normal);
    }

    private void PrepareActors()
    {
        player?.PrepareRound();
        if (ghosts == null)
        {
            return;
        }

        foreach (GhostController ghost in ghosts)
        {
            ghost?.ResetToStart();
        }
    }

    private void UpdateMusic()
    {
        bool anyDeadGhost = ghosts != null && ghosts.Any(ghost => ghost != null && ghost.IsDead);
        AudioManager.GhostMusicMode mode = anyDeadGhost
            ? AudioManager.GhostMusicMode.Dead
            : IsPowerActive ? AudioManager.GhostMusicMode.Scared : AudioManager.GhostMusicMode.Normal;
        AudioManager.Instance?.SetGhostMode(mode);
    }

    public void NotifyCherryCollected()
    {
        if (!ActorsCanMove) return;
        score += 100;
        AudioManager.Instance?.PlayBonusFruit();
        DecoyAbility.Instance?.RefillFromCherry();
    }

    public void ExitToMenu()
    {
        if (IsGameOver || state == GameState.Countdown) return;
        UnityEngine.SceneManagement.SceneManager.LoadScene("StartScene");
    }

    private IEnumerator ReturnAfterGameOver()
    {
        yield return new WaitForSeconds(3f);
        UnityEngine.SceneManagement.SceneManager.LoadScene("StartScene");
    }
    private void SaveResult() => ScoreStore.SaveResult(LevelKey, score, Mathf.FloorToInt(elapsedTime * 100f));
}
