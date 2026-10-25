using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PacStudentController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource movementAudioSource;
    [SerializeField] private AudioClip eatingMovementClip;
    [SerializeField] private AudioClip emptyMovementClip;
    [SerializeField] private ParticleSystem movementDust;
    [SerializeField, Min(0.01f)] private float moveSpeed = 5f;
    [SerializeField] private Vector2Int gameplayStartCell = new Vector2Int(1, 1);
    private Vector2Int currentCell, targetCell;
    private Vector2Int lastInput, currentInput;
    private Vector2Int lastBlockedInput;
    private Vector3 segmentStart, segmentEnd;
    private float segmentElapsed, segmentDuration;
    private bool gridSegmentActive;
    public Vector2Int CurrentCell => currentCell;
    public float MoveSpeed => moveSpeed;
    public Vector2Int FacingDirection => currentInput == Vector2Int.zero ? Vector2Int.right : currentInput;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (movementAudioSource == null) movementAudioSource = GetComponent<AudioSource>();
    }
    private void Start() => PrepareRound();
    private void OnDisable() { SetMovementAudio(false); SetDust(false); }
    public void PrepareRound()
    {
        currentCell = gameplayStartCell;
        targetCell = currentCell;
        lastInput = currentInput = Vector2Int.zero;
        lastBlockedInput = Vector2Int.zero;
        gridSegmentActive = false;
        transform.position = LevelGenerator.Instance.CellToWorld(currentCell);
        PlayDirectionalAnimation(Vector2Int.right);
        StopWalking();
    }
    public void BeginRound() { }
    public void SetDesiredDirection(Vector2Int direction)
    {
        if (Mathf.Abs(direction.x) + Mathf.Abs(direction.y) == 1) lastInput = direction;
    }
    public void PlayDeathAnimation()
    {
        gridSegmentActive = false;
        SetMovementAudio(false);
        SetDust(false);
        if (animator != null) { animator.speed = 1f; animator.Play("Dead", 0, 0f); }
    }
    private void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.ActorsCanMove)
        {
            SetMovementAudio(false);
            SetDust(false);
            return;
        }
        ReadKeyboardInput();
        AdvanceGridMovement(Time.deltaTime);
    }
    private void ReadKeyboardInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame) lastInput = new Vector2Int(0,-1);
        if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame) lastInput = Vector2Int.left;
        if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame) lastInput = new Vector2Int(0,1);
        if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame) lastInput = Vector2Int.right;
    }
    private void AdvanceGridMovement(float deltaTime)
    {
        float remaining = deltaTime;
        int completed = 0;
        while (remaining > 0f && completed < 8)
        {
            if (!gridSegmentActive && !TryBeginGridSegment()) { StopWalking(); return; }
            float consumed = Mathf.Min(remaining, segmentDuration - segmentElapsed);
            segmentElapsed += consumed; remaining -= consumed;
            transform.position = Vector3.Lerp(segmentStart, segmentEnd, Mathf.Clamp01(segmentElapsed / segmentDuration));
            if (segmentElapsed + Mathf.Epsilon < segmentDuration) return;
            currentCell = targetCell;
            transform.position = LevelGenerator.Instance.CellToWorld(currentCell);
            gridSegmentActive = false;
            if (LevelGenerator.Instance.TryCollectPellet(currentCell, out bool power)) GameManager.Instance.NotifyPelletCollected(power);
            if (!GameManager.Instance.ActorsCanMove) { StopWalking(); return; }
            completed++;
        }
    }
    private bool TryBeginGridSegment()
    {
        var level = LevelGenerator.Instance;
        if (level.TryGetPlayerStep(currentCell,lastInput,out targetCell)) currentInput = lastInput;
        else if (!level.TryGetPlayerStep(currentCell,currentInput,out targetCell))
        {
            if (lastInput != Vector2Int.zero && lastBlockedInput != lastInput)
            {
                GameManager.Instance.NotifyWallHit();
                lastBlockedInput = lastInput;
            }
            return false;
        }
        lastBlockedInput = Vector2Int.zero;
        segmentStart = level.CellToWorld(currentCell);
        // Tween out through the tunnel edge, then wrap onto the opposite edge.
        segmentEnd = level.CellToWorld(currentCell + currentInput);
        segmentElapsed = 0f;
        segmentDuration = Vector3.Distance(segmentStart,segmentEnd)/moveSpeed;
        gridSegmentActive = true;
        PlayDirectionalAnimation(currentInput);
        if (animator != null) animator.speed = 1f;
        if (movementAudioSource != null)
        {
            AudioClip desiredClip = level.HasPellet(targetCell) ? eatingMovementClip : emptyMovementClip;
            if (movementAudioSource.clip != desiredClip) { movementAudioSource.Stop(); movementAudioSource.clip = desiredClip; }
        }
        SetMovementAudio(true);
        SetDust(true);
        return true;
    }
    private void StopWalking()
    {
        SetMovementAudio(false);
        SetDust(false);
        if (animator != null) animator.speed = 0f;
    }
    private void PlayDirectionalAnimation(Vector2Int direction)
    {
        if (animator == null) return;
        string state = direction.x > 0 ? "Walk_Right" : direction.x < 0 ? "Walk_Left" : direction.y < 0 ? "Walk_Up" : "Walk_Down";
        animator.Play(state,0,0f);
    }
    private void SetMovementAudio(bool playing)
    {
        if (movementAudioSource == null || movementAudioSource.clip == null) return;
        movementAudioSource.loop = true;
        if (playing && !movementAudioSource.isPlaying) movementAudioSource.Play();
        else if (!playing && movementAudioSource.isPlaying) movementAudioSource.Stop();
    }
    private void SetDust(bool moving)
    {
        if (movementDust == null) return;
        if (moving && !movementDust.isPlaying) movementDust.Play();
        else if (!moving && movementDust.isPlaying) movementDust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}
