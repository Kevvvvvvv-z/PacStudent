using UnityEngine;

// One cherry at a time; each departure or collection starts a new five-second interval.
public class CherryController : MonoBehaviour
{
    [SerializeField] private Sprite cherrySprite;
    [SerializeField] private Sprite maskSprite;
    [SerializeField, Min(.1f)] private float speed = 4f;
    private GameObject cherry;
    private Vector3 start, end;
    private float elapsed, duration, spawnAt;
    private int previousSide = -1;
    public GameObject ActiveCherry => cherry;

    private void Start()
    {
        var bounds = LevelGenerator.Instance.MapBounds;
        var mask = new GameObject("CherryBoundsMask").AddComponent<SpriteMask>();
        mask.transform.SetParent(transform, false);
        mask.transform.position = bounds.center;
        mask.sprite = maskSprite;
        mask.transform.localScale = new Vector3(bounds.size.x / maskSprite.bounds.size.x, bounds.size.y / maskSprite.bounds.size.y, 1);
        mask.isCustomRangeActive = true;
        mask.backSortingOrder = 49;
        mask.frontSortingOrder = 51;
        ResetRound();
    }

    public void ResetRound()
    {
        if (cherry != null) Destroy(cherry);
        cherry = null;
        spawnAt = Time.time + 5f;
    }

    private void Update()
    {
        var manager = GameManager.Instance;
        if (manager == null || !manager.ActorsCanMove) return;
        if (cherry == null) { if (Time.time >= spawnAt) Spawn(); return; }
        Advance(Time.deltaTime);
    }

    private void Spawn()
    {
        Bounds bounds = LevelGenerator.Instance.MapBounds;
        // A different side guarantees a different starting position on successive passes.
        int side = previousSide < 0 ? Random.Range(0, 4) : (previousSide + Random.Range(1, 4)) % 4;
        previousSide = side;
        Vector3 half = bounds.extents + new Vector3(1, 1, 0);
        Vector3 offset = side < 2
            ? new Vector3(side == 0 ? -half.x : half.x, Random.Range(-half.y, half.y), 0)
            : new Vector3(Random.Range(-half.x, half.x), side == 2 ? -half.y : half.y, 0);
        start = bounds.center + offset;
        // Extend the opposite end beyond the camera, even on wide displays.
        var camera = Camera.main;
        float exitRadius = camera == null ? half.magnitude + 2f : camera.orthographicSize * Mathf.Sqrt(1 + camera.aspect * camera.aspect) + 2f;
        end = bounds.center - offset.normalized * Mathf.Max(exitRadius, half.magnitude + 2f);
        start.z = end.z = -.25f;
        duration = Vector3.Distance(start, end) / speed;
        elapsed = 0;
        cherry = new GameObject("BonusCherry");
        cherry.transform.SetParent(transform, false);
        cherry.transform.position = start;
        var renderer = cherry.AddComponent<SpriteRenderer>();
        renderer.sprite = cherrySprite;
        renderer.sortingOrder = 50;
        renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
    }

    private void Advance(float deltaTime)
    {
        Vector3 before = cherry.transform.position;
        elapsed += deltaTime;
        Vector3 after = Vector3.Lerp(start, end, Mathf.Clamp01(elapsed / duration));
        cherry.transform.position = after;
        var manager = GameManager.Instance;
        Vector2 segment = (Vector2)(after - before);
        float t = segment.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot((Vector2)(manager.Player.transform.position - before), segment) / segment.sqrMagnitude) : 0;
        Vector2 closest = (Vector2)before + segment * t;
        if (Vector2.Distance(closest, manager.Player.transform.position) <= .48f)
        {
            manager.NotifyCherryCollected();
            ResetRound();
        }
        else if (elapsed >= duration) ResetRound();
    }
}
