using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class GhostController : MonoBehaviour
{
    [SerializeField, Range(0, 3)] private int ghostIndex;
    [SerializeField] private Animator animator;
    [SerializeField] private Vector2Int startCell = new Vector2Int(12, 14);
    [SerializeField, Min(.01f)] private float moveSpeed = 4.5f;
    private static readonly Vector2Int[] Directions = { new Vector2Int(0,-1), Vector2Int.right, new Vector2Int(0,1), Vector2Int.left };
    private Vector2Int currentCell, targetCell, currentDirection;
    private Vector3 segmentStart, segmentEnd;
    private float segmentElapsed, segmentDuration;
    private bool segmentActive, dead, leavingPen;
    private int playingState;
    private readonly List<Vector2Int> outerCircuit = new List<Vector2Int>();
    private int circuitIndex = -1;
    public bool IsDead => dead;
    public bool IsFrightened => !dead && GameManager.Instance != null && GameManager.Instance.IsPowerActive;
    public Vector2Int CurrentCell => currentCell;
    public float NormalSpeed => GameManager.Instance != null && GameManager.Instance.Player != null ? GameManager.Instance.Player.MoveSpeed * .9f : moveSpeed;
    public float StateSpeed => NormalSpeed * (dead || IsFrightened ? .5f : 1f);

    private void Awake() { if (animator == null) animator = GetComponent<Animator>(); }
    private void Start() => ResetToStart();
    private void Update()
    {
        var manager = GameManager.Instance;
        bool moving = manager != null && manager.ActorsCanMove && LevelGenerator.Instance != null;
        if (animator != null) animator.speed = moving ? 1f : 0f;
        if (!moving) return;
        PlayAnimation(currentDirection);
        if (dead) ReturnToPen(Time.deltaTime); else AdvanceMovement(Time.deltaTime);
    }
    public void Configure(int index, Vector2Int spawnCell, float speed)
    {
        ghostIndex = Mathf.Clamp(index,0,3);startCell=spawnCell;moveSpeed=speed;animator=GetComponent<Animator>();
    }
    public void ResetToStart()
    {
        currentCell=targetCell=startCell;
        currentDirection=ghostIndex%2==0?Vector2Int.left:Vector2Int.right;
        segmentActive=dead=false;leavingPen=true;circuitIndex=-1;
        transform.position=LevelGenerator.Instance.CellToWorld(startCell,-.15f);
        playingState=0;PlayAnimation(currentDirection);
    }
    public void BecomeDead() { dead=true;segmentActive=false;circuitIndex=-1;PlayAnimation(currentDirection); }
    public void RefreshStateAnimation() => PlayAnimation(currentDirection);

    private void AdvanceMovement(float deltaTime)
    {
        float remaining = deltaTime;
        int completed = 0;
        while (remaining > 0 && completed < 16)
        {
            if (!segmentActive && !BeginNextSegment()) return;
            // Preserve position when a power pellet changes speed during a tween.
            float progress = segmentDuration > 0 ? segmentElapsed / segmentDuration : 0;
            segmentDuration = Vector3.Distance(segmentStart,segmentEnd) / StateSpeed;
            segmentElapsed = progress * segmentDuration;
            float consumed = Mathf.Min(remaining,segmentDuration-segmentElapsed);
            segmentElapsed += consumed; remaining -= consumed;
            transform.position = Vector3.Lerp(segmentStart,segmentEnd,Mathf.Clamp01(segmentElapsed/segmentDuration));
            if (segmentElapsed + .000001f < segmentDuration) break;
            transform.position=segmentEnd;currentCell=targetCell;segmentActive=false;completed++;
        }
    }
    private bool BeginNextSegment()
    {
        if (leavingPen)
        {
            int column=ghostIndex%2==0?13:14, row=ghostIndex%2==0?11:17;
            if(currentCell.y==row)leavingPen=false;
            else
            {
                Vector2Int next=currentCell.x!=column?currentCell+new Vector2Int(column>currentCell.x?1:-1,0):currentCell+new Vector2Int(0,row>currentCell.y?1:-1);
                BeginSegment(next);return true;
            }
        }
        var available=GetAvailableDirections();
        if(available.Count==0)return false;
        if(available.Count>1)available.Remove(-currentDirection);
        BeginSegment(currentCell+ChooseDirection(available));return true;
    }
    private void BeginSegment(Vector2Int next)
    {
        currentDirection=next-currentCell;targetCell=next;
        segmentStart=LevelGenerator.Instance.CellToWorld(currentCell,-.15f);segmentEnd=LevelGenerator.Instance.CellToWorld(next,-.15f);
        segmentElapsed=0;segmentDuration=Vector3.Distance(segmentStart,segmentEnd)/StateSpeed;segmentActive=true;PlayAnimation(currentDirection);
    }
    private void ReturnToPen(float deltaTime)
    {
        var destination=LevelGenerator.Instance.CellToWorld(startCell,-.15f);
        float distance=Vector3.Distance(transform.position,destination),travel=StateSpeed*deltaTime;
        if(distance>travel){transform.position=Vector3.Lerp(transform.position,destination,travel/distance);return;}
        transform.position=destination;currentCell=targetCell=startCell;segmentActive=dead=false;leavingPen=true;circuitIndex=-1;PlayAnimation(currentDirection);
    }
    private List<Vector2Int> GetAvailableDirections()
    {
        var choices=new List<Vector2Int>(4);
        foreach(var direction in Directions)if(LevelGenerator.Instance.IsGhostWalkable(currentCell+direction))choices.Add(direction);
        return choices;
    }
    private Vector2Int ChooseDirection(List<Vector2Int> available)
    {
        if(IsFrightened){circuitIndex=-1;return DistanceChoice(available,false);}
        if(GameManager.Instance.HasDecoy){circuitIndex=-1;return DistanceChoice(available,true);}
        if(ghostIndex==0)return DistanceChoice(available,false);
        if(ghostIndex==1)return DistanceChoice(available,true);
        if(ghostIndex==2)return available[Random.Range(0,available.Count)];
        return ClockwiseChoice(available);
    }
    private Vector2Int DistanceChoice(List<Vector2Int> available,bool approach)
    {
        Vector2 target=IsFrightened ? (Vector2)GameManager.Instance.Player.transform.position : GameManager.Instance.GhostTargetPosition;
        float currentDistance=((Vector2)LevelGenerator.Instance.CellToWorld(currentCell)-target).sqrMagnitude;
        var preferred=new List<Vector2Int>();
        foreach(var direction in available)
        {
            float distance=((Vector2)LevelGenerator.Instance.CellToWorld(currentCell+direction)-target).sqrMagnitude;
            if(approach?distance<=currentDistance:distance>=currentDistance)preferred.Add(direction);
        }
        // Keep moving without reversing when every legal exit violates the distance preference.
        var choices=preferred.Count>0?preferred:available;
        return choices[Random.Range(0,choices.Count)];
    }
    private void BuildOuterCircuit()
    {
        outerCircuit.Clear();
        var cell=new Vector2Int(1,1);var direction=Vector2Int.right;
        var visited=new HashSet<(Vector2Int,Vector2Int)>();
        while(true)
        {
            // Keeping the outside wall on the left traces its clockwise boundary.
            var left=new Vector2Int(direction.y,-direction.x);
            foreach(var candidate in new[]{left,direction,-left,-direction})
                if(LevelGenerator.Instance.IsGhostWalkable(cell+candidate)){direction=candidate;break;}
            if(!visited.Add((cell,direction)))break;
            outerCircuit.Add(cell);
            cell+=direction;
        }
    }
    private Vector2Int ClockwiseChoice(List<Vector2Int> available)
    {
        if(outerCircuit.Count==0)BuildOuterCircuit();
        if(circuitIndex>=0 && outerCircuit[circuitIndex]!=currentCell)circuitIndex=-1;
        if(circuitIndex<0)
        {
            for(int i=0;i<outerCircuit.Count;i++)
                if(outerCircuit[i]==currentCell && available.Contains(outerCircuit[(i+1)%outerCircuit.Count]-currentCell)){circuitIndex=i;break;}
        }
        if(circuitIndex>=0)
        {
            int next=(circuitIndex+1)%outerCircuit.Count;var direction=outerCircuit[next]-currentCell;
            if(available.Contains(direction)){circuitIndex=next;return direction;}
            circuitIndex=-1;
        }
        // A breadth-first search joins the boundary after leaving the pen or recovering.
        var boundary=new HashSet<Vector2Int>(outerCircuit);
        var queue=new Queue<(Vector2Int cell,Vector2Int first)>();var seen=new HashSet<Vector2Int>{currentCell};
        foreach(var direction in available){var cell=currentCell+direction;queue.Enqueue((cell,direction));seen.Add(cell);}
        while(queue.Count>0)
        {
            var item=queue.Dequeue();if(boundary.Contains(item.cell))return item.first;
            foreach(var direction in Directions){var next=item.cell+direction;if(LevelGenerator.Instance.IsGhostWalkable(next)&&seen.Add(next))queue.Enqueue((next,item.first));}
        }
        return available[0];
    }
    private void PlayAnimation(Vector2Int direction)
    {
        if(animator==null)return;
        string name;
        if(dead)name="Dead";
        else if(IsFrightened && GameManager.Instance.PowerTimeRemaining<=3f)name="Recovering";
        else
        {
            string prefix=IsFrightened?"Scared_":"Normal_";
            name=prefix+(direction.x>0?"Right":direction.x<0?"Left":direction.y<0?"Up":"Down");
        }
        int hash=Animator.StringToHash(name);if(hash==playingState)return;playingState=hash;animator.Play(hash,0,0);
    }
}
