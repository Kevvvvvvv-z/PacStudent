using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PacStudentController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource movementAudioSource;

    [Header("Playable mode")]
    [SerializeField] private bool automaticAssessmentLoop;
    [SerializeField, Min(0.01f)] private float moveSpeed = 5f;
    [SerializeField] private Vector2Int gameplayStartCell = new Vector2Int(1, 1);

    [Header("Assessment 3 automatic path")]
    [SerializeField] private Vector2[] waypoints =
    {
        new Vector2(1f, -1f),
        new Vector2(6f, -1f),
        new Vector2(6f, -5f),
        new Vector2(1f, -5f)
    };

    private static readonly int WalkRightState = Animator.StringToHash("Walk_Right");
    private static readonly int WalkDownState = Animator.StringToHash("Walk_Down");
    private static readonly int WalkLeftState = Animator.StringToHash("Walk_Left");
    private static readonly int WalkUpState = Animator.StringToHash("Walk_Up");
    private static readonly int DeadState = Animator.StringToHash("Dead");

    private int segmentStartIndex;
    private Vector3 segmentStart;
    private Vector3 segmentEnd;
    private float segmentElapsed;
    private float segmentDuration;
    private bool pathReady;

    private Vector2Int currentCell;
    private Vector2Int targetCell;
    private Vector2Int currentDirection;
    private Vector2Int desiredDirection;
    private bool gridSegmentActive;
    private bool collectedCurrentCell;

    public Vector2Int CurrentCell => currentCell;
    public Vector2Int FacingDirection => currentDirection == Vector2Int.zero ? desiredDirection : currentDirection;
    public bool AutomaticAssessmentLoop => automaticAssessmentLoop;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (movementAudioSource == null)
        {
            movementAudioSource = GetComponent<AudioSource>();
        }
    }

    private void Start()
    {
        if (automaticAssessmentLoop)
        {
            PrepareAssessmentPath();
        }
        else
        {
            PrepareRound();
        }
    }

    private void Update()
    {
        if (automaticAssessmentLoop)
        {
            if (pathReady && Time.deltaTime > 0f)
            {
                AdvanceAlongAssessmentPath(Time.deltaTime);
            }

            return;
        }

        ReadKeyboardInput();
        GameManager manager = GameManager.Instance;
        if (manager != null && !manager.ActorsCanMove)
        {
            SetMovementAudio(false);
            return;
        }

        if (!collectedCurrentCell)
        {
            CollectCurrentCell();
        }

        AdvanceGridMovement(Time.deltaTime);
    }

    private void OnDisable()
    {
        SetMovementAudio(false);
    }

    public void Configure(Animator assignedAnimator, AudioSource assignedAudioSource, Vector2[] assignedWaypoints, float assignedSpeed)
    {
        animator = assignedAnimator;
        movementAudioSource = assignedAudioSource;
        waypoints = assignedWaypoints;
        moveSpeed = Mathf.Max(0.01f, assignedSpeed);
    }

    public void ConfigureGameplay(bool useAutomaticAssessmentLoop, Vector2Int startCell, float speed)
    {
        automaticAssessmentLoop = useAutomaticAssessmentLoop;
        gameplayStartCell = startCell;
        moveSpeed = Mathf.Max(0.01f, speed);
    }

    public void PrepareRound()
    {
        LevelGenerator level = LevelGenerator.Instance;
        currentCell = level == null ? gameplayStartCell : level.FindNearestWalkable(gameplayStartCell);
        targetCell = currentCell;
        currentDirection = Vector2Int.zero;
        desiredDirection = Vector2Int.right;
        gridSegmentActive = false;
        collectedCurrentCell = false;
        transform.position = level == null
            ? new Vector3(currentCell.x, -currentCell.y, -0.1f)
            : level.CellToWorld(currentCell);
        PlayDirectionalAnimation(desiredDirection);
        SetMovementAudio(false);
    }

    public void BeginRound()
    {
        collectedCurrentCell = false;
    }

    public void PlayDeathAnimation()
    {
        gridSegmentActive = false;
        SetMovementAudio(false);
        if (animator != null)
        {
            animator.Play(DeadState, 0, 0f);
        }
    }

    public void SetDesiredDirection(Vector2Int direction)
    {
        if (direction == Vector2Int.zero || Mathf.Abs(direction.x) + Mathf.Abs(direction.y) != 1)
        {
            return;
        }

        desiredDirection = direction;
    }

    private void ReadKeyboardInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        Vector2Int requested = Vector2Int.zero;
        if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
        {
            requested = new Vector2Int(0, -1);
        }
        else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
        {
            requested = new Vector2Int(0, 1);
        }
        else if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
        {
            requested = Vector2Int.left;
        }
        else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
        {
            requested = Vector2Int.right;
        }

        if (requested == Vector2Int.zero)
        {
            return;
        }

        desiredDirection = requested;
        if (!gridSegmentActive && LevelGenerator.Instance != null && !LevelGenerator.Instance.IsPlayerWalkable(currentCell + desiredDirection))
        {
            GameManager.Instance?.NotifyWallHit();
        }
    }

    private void AdvanceGridMovement(float deltaTime)
    {
        if (deltaTime <= 0f || LevelGenerator.Instance == null)
        {
            SetMovementAudio(false);
            return;
        }

        float remainingTime = deltaTime;
        int safety = 0;
        while (remainingTime > 0f && safety < 4)
        {
            if (!gridSegmentActive && !TryBeginGridSegment())
            {
                SetMovementAudio(false);
                return;
            }

            float timeUntilEnd = segmentDuration - segmentElapsed;
            float consumed = Mathf.Min(remainingTime, timeUntilEnd);
            segmentElapsed += consumed;
            remainingTime -= consumed;
            transform.position = Vector3.Lerp(segmentStart, segmentEnd, Mathf.Clamp01(segmentElapsed / segmentDuration));
            SetMovementAudio(true);

            if (segmentElapsed + Mathf.Epsilon < segmentDuration)
            {
                break;
            }

            transform.position = segmentEnd;
            currentCell = targetCell;
            gridSegmentActive = false;
            CollectCurrentCell();
            safety++;
        }
    }

    private bool TryBeginGridSegment()
    {
        LevelGenerator level = LevelGenerator.Instance;
        if (level.IsPlayerWalkable(currentCell + desiredDirection))
        {
            currentDirection = desiredDirection;
        }
        else if (!level.IsPlayerWalkable(currentCell + currentDirection))
        {
            currentDirection = Vector2Int.zero;
            return false;
        }

        targetCell = currentCell + currentDirection;
        segmentStart = level.CellToWorld(currentCell);
        segmentEnd = level.CellToWorld(targetCell);
        segmentElapsed = 0f;
        segmentDuration = Vector3.Distance(segmentStart, segmentEnd) / Mathf.Max(0.01f, moveSpeed);
        gridSegmentActive = true;
        PlayDirectionalAnimation(currentDirection);
        return true;
    }

    private void CollectCurrentCell()
    {
        collectedCurrentCell = true;
        LevelGenerator level = LevelGenerator.Instance;
        if (level != null && level.TryCollectPellet(currentCell, out bool isPowerPellet))
        {
            GameManager.Instance?.NotifyPelletCollected(isPowerPellet);
        }
    }

    private void PrepareAssessmentPath()
    {
        if (waypoints == null || waypoints.Length < 2)
        {
            Debug.LogError("PacStudent needs at least two movement waypoints.", this);
            return;
        }

        transform.position = ToWorldPosition(waypoints[0]);
        segmentStartIndex = 0;
        BeginAssessmentSegment();
        pathReady = true;
        SetMovementAudio(true);
    }

    private void AdvanceAlongAssessmentPath(float deltaTime)
    {
        float remainingTime = deltaTime;
        int completedSegments = 0;

        while (remainingTime > 0f && completedSegments <= waypoints.Length)
        {
            float timeUntilEnd = segmentDuration - segmentElapsed;
            float consumedTime = Mathf.Min(remainingTime, timeUntilEnd);
            segmentElapsed += consumedTime;
            remainingTime -= consumedTime;

            float interpolation = Mathf.Clamp01(segmentElapsed / segmentDuration);
            transform.position = Vector3.Lerp(segmentStart, segmentEnd, interpolation);

            if (segmentElapsed + Mathf.Epsilon < segmentDuration)
            {
                break;
            }

            transform.position = segmentEnd;
            segmentStartIndex = (segmentStartIndex + 1) % waypoints.Length;
            BeginAssessmentSegment();
            completedSegments++;
        }
    }

    private void BeginAssessmentSegment()
    {
        int segmentEndIndex = (segmentStartIndex + 1) % waypoints.Length;
        segmentStart = ToWorldPosition(waypoints[segmentStartIndex]);
        segmentEnd = ToWorldPosition(waypoints[segmentEndIndex]);
        segmentElapsed = 0f;

        float distance = Vector3.Distance(segmentStart, segmentEnd);
        segmentDuration = distance / Mathf.Max(0.01f, moveSpeed);
        PlayDirectionalAnimation(WorldToGridDirection(segmentEnd - segmentStart));
    }

    private void PlayDirectionalAnimation(Vector2Int direction)
    {
        if (animator == null)
        {
            return;
        }

        int state;
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            state = direction.x >= 0 ? WalkRightState : WalkLeftState;
        }
        else
        {
            state = direction.y <= 0 ? WalkUpState : WalkDownState;
        }

        animator.Play(state, 0, 0f);
    }

    private void SetMovementAudio(bool shouldPlay)
    {
        if (movementAudioSource == null || movementAudioSource.clip == null)
        {
            return;
        }

        movementAudioSource.loop = true;
        if (shouldPlay && !movementAudioSource.isPlaying)
        {
            movementAudioSource.Play();
        }
        else if (!shouldPlay && movementAudioSource.isPlaying)
        {
            movementAudioSource.Stop();
        }
    }

    private static Vector2Int WorldToGridDirection(Vector3 worldDirection)
    {
        if (Mathf.Abs(worldDirection.x) > Mathf.Abs(worldDirection.y))
        {
            return worldDirection.x >= 0f ? Vector2Int.right : Vector2Int.left;
        }

        return worldDirection.y >= 0f ? new Vector2Int(0, -1) : new Vector2Int(0, 1);
    }

    private static Vector3 ToWorldPosition(Vector2 point)
    {
        return new Vector3(point.x, point.y, -0.1f);
    }
}
