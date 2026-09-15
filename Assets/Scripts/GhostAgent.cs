using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class GhostAgent : MonoBehaviour
{
    [SerializeField, Range(0, 3)] private int ghostIndex;
    [SerializeField] private Animator animator;
    [SerializeField] private Vector2Int startCell = new Vector2Int(6, 14);
    [SerializeField, Min(0.01f)] private float moveSpeed = 4f;

    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(0, -1),
        Vector2Int.left,
        new Vector2Int(0, 1),
        Vector2Int.right
    };

    private Vector2Int currentCell;
    private Vector2Int targetCell;
    private Vector2Int currentDirection;
    private Vector3 segmentStart;
    private Vector3 segmentEnd;
    private float segmentElapsed;
    private float segmentDuration;
    private bool segmentActive;
    private bool dead;
    private int playingState;

    public bool IsDead => dead;
    public bool IsFrightened => !dead && GameManager.Instance != null && GameManager.Instance.IsPowerActive;
    public Vector2Int CurrentCell => currentCell;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }

    private void Start()
    {
        ResetToStart();
    }

    private void Update()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null || !manager.ActorsCanMove || LevelGenerator.Instance == null)
        {
            return;
        }

        AdvanceMovement(Time.deltaTime);
    }

    public void Configure(int index, Vector2Int spawnCell, float speed)
    {
        ghostIndex = Mathf.Clamp(index, 0, 3);
        startCell = spawnCell;
        moveSpeed = Mathf.Max(0.01f, speed);
        animator = GetComponent<Animator>();
    }

    public void ResetToStart()
    {
        LevelGenerator level = LevelGenerator.Instance;
        currentCell = level == null ? startCell : level.FindNearestWalkable(startCell);
        targetCell = currentCell;
        currentDirection = ghostIndex % 2 == 0 ? Vector2Int.left : Vector2Int.right;
        segmentActive = false;
        dead = false;
        transform.position = level == null
            ? new Vector3(currentCell.x, -currentCell.y, -0.15f)
            : level.CellToWorld(currentCell, -0.15f);
        playingState = 0;
        PlayAnimation(currentDirection);
    }

    public void BecomeDead()
    {
        dead = true;
        segmentActive = false;
        playingState = 0;
        PlayAnimation(currentDirection);
    }

    public void RefreshStateAnimation()
    {
        playingState = 0;
        PlayAnimation(currentDirection);
    }

    private void AdvanceMovement(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        float remainingTime = deltaTime;
        int safety = 0;
        while (remainingTime > 0f && safety < 4)
        {
            if (!segmentActive && !BeginNextSegment())
            {
                return;
            }

            float timeUntilEnd = segmentDuration - segmentElapsed;
            float consumed = Mathf.Min(remainingTime, timeUntilEnd);
            segmentElapsed += consumed;
            remainingTime -= consumed;
            transform.position = Vector3.Lerp(segmentStart, segmentEnd, Mathf.Clamp01(segmentElapsed / segmentDuration));

            if (segmentElapsed + Mathf.Epsilon < segmentDuration)
            {
                break;
            }

            transform.position = segmentEnd;
            currentCell = targetCell;
            segmentActive = false;

            if (dead && currentCell == LevelGenerator.Instance.FindNearestWalkable(startCell))
            {
                dead = false;
                playingState = 0;
            }

            safety++;
        }
    }

    private bool BeginNextSegment()
    {
        List<Vector2Int> available = GetAvailableDirections();
        if (available.Count == 0)
        {
            return false;
        }

        Vector2Int reverse = -currentDirection;
        if (available.Count > 1)
        {
            available.Remove(reverse);
        }

        currentDirection = ChooseDirection(available);
        targetCell = currentCell + currentDirection;
        LevelGenerator level = LevelGenerator.Instance;
        segmentStart = level.CellToWorld(currentCell, -0.15f);
        segmentEnd = level.CellToWorld(targetCell, -0.15f);
        segmentElapsed = 0f;

        float stateSpeed = IsFrightened ? moveSpeed * 0.72f : dead ? moveSpeed * 1.45f : moveSpeed;
        segmentDuration = Vector3.Distance(segmentStart, segmentEnd) / Mathf.Max(0.01f, stateSpeed);
        segmentActive = true;
        PlayAnimation(currentDirection);
        return true;
    }

    private List<Vector2Int> GetAvailableDirections()
    {
        var available = new List<Vector2Int>(4);
        foreach (Vector2Int direction in Directions)
        {
            if (LevelGenerator.Instance.IsGhostWalkable(currentCell + direction))
            {
                available.Add(direction);
            }
        }

        return available;
    }

    private Vector2Int ChooseDirection(List<Vector2Int> available)
    {
        if (available.Count == 1)
        {
            return available[0];
        }

        if (IsFrightened)
        {
            Vector2Int playerCell = GameManager.Instance.Player.CurrentCell;
            int bestDistance = int.MinValue;
            Vector2Int best = available[0];
            foreach (Vector2Int direction in available)
            {
                Vector2Int candidate = currentCell + direction;
                int distance = Mathf.Abs(candidate.x - playerCell.x) + Mathf.Abs(candidate.y - playerCell.y);
                distance += Random.Range(0, 3);
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    best = direction;
                }
            }

            return best;
        }

        Vector2Int target = dead ? startCell : GetChaseTarget();
        target = LevelGenerator.Instance.FindNearestWalkable(target);
        int shortest = int.MaxValue;
        Vector2Int chosen = available[0];
        int offset = ghostIndex % available.Count;

        for (int index = 0; index < available.Count; index++)
        {
            Vector2Int direction = available[(index + offset) % available.Count];
            int distance = LevelGenerator.Instance.ShortestPathDistance(currentCell + direction, target);
            if (distance < shortest)
            {
                shortest = distance;
                chosen = direction;
            }
        }

        return chosen;
    }

    private Vector2Int GetChaseTarget()
    {
        PacStudentController player = GameManager.Instance.Player;
        Vector2Int playerCell = player.CurrentCell;

        switch (ghostIndex)
        {
            case 1:
                return playerCell + player.FacingDirection * 4;
            case 2:
                return new Vector2Int(
                    LevelGenerator.Instance.Width - 1 - playerCell.x,
                    LevelGenerator.Instance.Height - 1 - playerCell.y);
            case 3:
                bool scatter = Mathf.FloorToInt(Time.time / 7f) % 2 == 1;
                return scatter ? new Vector2Int(LevelGenerator.Instance.Width - 2, LevelGenerator.Instance.Height - 2) : playerCell;
            default:
                return playerCell;
        }
    }

    private void PlayAnimation(Vector2Int direction)
    {
        if (animator == null)
        {
            return;
        }

        string stateName;
        if (dead)
        {
            stateName = "Dead";
        }
        else if (IsFrightened && GameManager.Instance.PowerTimeRemaining <= 2f)
        {
            stateName = "Recovering";
        }
        else
        {
            string prefix = IsFrightened ? "Scared_" : "Normal_";
            if (direction.x > 0) stateName = prefix + "Right";
            else if (direction.x < 0) stateName = prefix + "Left";
            else if (direction.y < 0) stateName = prefix + "Up";
            else stateName = prefix + "Down";
        }

        int state = Animator.StringToHash(stateName);
        if (state == playingState)
        {
            return;
        }

        playingState = state;
        animator.Play(state, 0, 0f);
    }
}
