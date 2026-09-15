using UnityEngine;

[DisallowMultipleComponent]
public class PacStudentController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource movementAudioSource;
    [SerializeField, Min(0.01f)] private float moveSpeed = 3f;
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

    private int segmentStartIndex;
    private Vector3 segmentStart;
    private Vector3 segmentEnd;
    private float segmentElapsed;
    private float segmentDuration;
    private bool pathReady;

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
        if (waypoints == null || waypoints.Length < 2)
        {
            Debug.LogError("PacStudent needs at least two movement waypoints.", this);
            return;
        }

        transform.position = ToWorldPosition(waypoints[0]);
        segmentStartIndex = 0;
        BeginSegment();
        pathReady = true;

        if (movementAudioSource != null && movementAudioSource.clip != null)
        {
            movementAudioSource.loop = true;
            movementAudioSource.Play();
        }
    }

    private void Update()
    {
        if (!pathReady || Time.deltaTime <= 0f)
        {
            return;
        }

        AdvanceAlongPath(Time.deltaTime);
    }

    private void OnDisable()
    {
        if (movementAudioSource != null)
        {
            movementAudioSource.Stop();
        }
    }

    public void Configure(Animator assignedAnimator, AudioSource assignedAudioSource, Vector2[] assignedWaypoints, float assignedSpeed)
    {
        animator = assignedAnimator;
        movementAudioSource = assignedAudioSource;
        waypoints = assignedWaypoints;
        moveSpeed = Mathf.Max(0.01f, assignedSpeed);
    }

    private void AdvanceAlongPath(float deltaTime)
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
            BeginSegment();
            completedSegments++;
        }
    }

    private void BeginSegment()
    {
        int segmentEndIndex = (segmentStartIndex + 1) % waypoints.Length;
        segmentStart = ToWorldPosition(waypoints[segmentStartIndex]);
        segmentEnd = ToWorldPosition(waypoints[segmentEndIndex]);
        segmentElapsed = 0f;

        float distance = Vector3.Distance(segmentStart, segmentEnd);
        segmentDuration = distance / Mathf.Max(0.01f, moveSpeed);
        PlayDirectionalAnimation(segmentEnd - segmentStart);
    }

    private void PlayDirectionalAnimation(Vector3 direction)
    {
        if (animator == null)
        {
            return;
        }

        int state;
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            state = direction.x >= 0f ? WalkRightState : WalkLeftState;
        }
        else
        {
            state = direction.y >= 0f ? WalkUpState : WalkDownState;
        }

        animator.Play(state, 0, 0f);
    }

    private static Vector3 ToWorldPosition(Vector2 point)
    {
        return new Vector3(point.x, point.y, -0.1f);
    }
}
