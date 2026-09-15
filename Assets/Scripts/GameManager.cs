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
        Playing,
        Dying,
        Won,
        Lost
    }

    [Header("Actors")]
    [SerializeField] private PacStudentController player;
    [SerializeField] private GhostAgent[] ghosts;

    [Header("Rules")]
    [SerializeField, Min(1)] private int startingLives = 3;
    [SerializeField, Min(1f)] private float powerDuration = 8f;
    [SerializeField, Min(0.1f)] private float collisionDistance = 0.48f;

    [Header("Bonus fruit")]
    [SerializeField] private Sprite bonusFruitSprite;
    [SerializeField] private Vector2Int bonusFruitCell = new Vector2Int(13, 23);
    [SerializeField, Min(1f)] private float bonusFruitLifetime = 12f;

    private GameState state;
    private int score;
    private int lives;
    private int initialPelletCount;
    private float powerEndTime;
    private int ghostEatChain;
    private bool bonusFruitWasSpawned;
    private GameObject bonusFruit;
    private float bonusFruitEndTime;
    private GUIStyle hudStyle;
    private GUIStyle centreStyle;

    public static GameManager Instance { get; private set; }
    public bool ActorsCanMove => state == GameState.Playing;
    public bool IsPowerActive => state == GameState.Playing && Time.time < powerEndTime;
    public float PowerTimeRemaining => Mathf.Max(0f, powerEndTime - Time.time);
    public PacStudentController Player => player;
    public GameState State => state;
    public int Score => score;
    public int Lives => lives;

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
            ghosts = FindObjectsByType<GhostAgent>();
        }

        lives = startingLives;
        initialPelletCount = LevelGenerator.Instance == null ? 0 : LevelGenerator.Instance.RemainingPellets;
        PrepareActors();
        state = GameState.Waiting;
    }

    private void Update()
    {
        ReadStateInput();

        if (state != GameState.Playing)
        {
            return;
        }

        if (!IsPowerActive && powerEndTime > 0f)
        {
            powerEndTime = 0f;
            ghostEatChain = 0;
        }

        UpdateBonusFruit();
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

    public void Configure(PacStudentController assignedPlayer, GhostAgent[] assignedGhosts, Sprite assignedBonusFruit)
    {
        player = assignedPlayer;
        ghosts = assignedGhosts;
        bonusFruitSprite = assignedBonusFruit;
    }

    public void BeginGame()
    {
        if (state != GameState.Waiting)
        {
            return;
        }

        state = GameState.Playing;
        player?.BeginRound();
        UpdateMusic();
    }

    public void RestartGame()
    {
        StopAllCoroutines();
        DestroyBonusFruit();
        score = 0;
        lives = startingLives;
        powerEndTime = 0f;
        ghostEatChain = 0;
        bonusFruitWasSpawned = false;

        LevelGenerator.Instance?.GenerateLevel();
        initialPelletCount = LevelGenerator.Instance == null ? 0 : LevelGenerator.Instance.RemainingPellets;
        PrepareActors();
        state = GameState.Waiting;
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
            ghostEatChain = 0;
            foreach (GhostAgent ghost in ghosts)
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
            AudioManager.Instance?.SetGhostMode(AudioManager.GhostMusicMode.Normal);
            return;
        }

        if (!bonusFruitWasSpawned && initialPelletCount > 0 && remaining <= initialPelletCount / 2)
        {
            SpawnBonusFruit();
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

        if (state == GameState.Waiting &&
            (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
        {
            BeginGame();
        }
        else if ((state == GameState.Won || state == GameState.Lost) && keyboard.rKey.wasPressedThisFrame)
        {
            RestartGame();
        }
    }

    private void CheckGhostCollisions()
    {
        if (player == null || ghosts == null)
        {
            return;
        }

        foreach (GhostAgent ghost in ghosts)
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
                ghostEatChain++;
                score += 200 * (1 << Mathf.Min(ghostEatChain - 1, 3));
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
        powerEndTime = 0f;
        ghostEatChain = 0;
        DestroyBonusFruit();
        player?.PlayDeathAnimation();
        AudioManager.Instance?.PlayDeath();
        yield return new WaitForSeconds(1.2f);

        lives--;
        if (lives <= 0)
        {
            state = GameState.Lost;
        }
        else
        {
            PrepareActors();
            state = GameState.Waiting;
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

        foreach (GhostAgent ghost in ghosts)
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

    private void SpawnBonusFruit()
    {
        bonusFruitWasSpawned = true;
        if (bonusFruitSprite == null || LevelGenerator.Instance == null)
        {
            return;
        }

        Vector2Int cell = LevelGenerator.Instance.FindNearestWalkable(bonusFruitCell);
        bonusFruit = new GameObject("BonusFruit");
        var renderer = bonusFruit.AddComponent<SpriteRenderer>();
        renderer.sprite = bonusFruitSprite;
        renderer.sortingOrder = 6;
        bonusFruit.transform.position = LevelGenerator.Instance.CellToWorld(cell, -0.2f);
        bonusFruitEndTime = Time.time + bonusFruitLifetime;
        bonusFruitCell = cell;
    }

    private void UpdateBonusFruit()
    {
        if (bonusFruit == null)
        {
            return;
        }

        if (Time.time >= bonusFruitEndTime)
        {
            DestroyBonusFruit();
            return;
        }

        if (player != null && player.CurrentCell == bonusFruitCell)
        {
            score += 500;
            AudioManager.Instance?.PlayBonusFruit();
            DestroyBonusFruit();
        }
    }

    private void DestroyBonusFruit()
    {
        if (bonusFruit != null)
        {
            Destroy(bonusFruit);
            bonusFruit = null;
        }
    }

    private void OnGUI()
    {
        if (hudStyle == null)
        {
            hudStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(18, Screen.height / 36),
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            centreStyle = new GUIStyle(hudStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Max(24, Screen.height / 24)
            };
        }

        GUI.Label(new Rect(18f, 10f, 520f, 40f), $"SCORE  {score:000000}     LIVES  {lives}", hudStyle);
        GUI.Label(new Rect(18f, Screen.height - 42f, 600f, 34f), "Move: Arrow keys / WASD", hudStyle);

        string message = state switch
        {
            GameState.Waiting => "READY!\nPress ENTER or SPACE",
            GameState.Dying => "OUCH!",
            GameState.Won => $"YOU CLEARED THE MAZE!\nScore: {score}\nPress R to restart",
            GameState.Lost => $"GAME OVER\nScore: {score}\nPress R to restart",
            _ => string.Empty
        };

        if (!string.IsNullOrEmpty(message))
        {
            GUI.Box(new Rect(Screen.width / 2f - 210f, Screen.height / 2f - 78f, 420f, 156f), GUIContent.none);
            GUI.Label(new Rect(Screen.width / 2f - 200f, Screen.height / 2f - 70f, 400f, 140f), message, centreStyle);
        }
    }
}
