using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Level 2 trades a limited resource for a brief opening in the ghost patrols.
public class DecoyAbility : MonoBehaviour
{
    [SerializeField] private Sprite decoySprite;
    [SerializeField] private Text statusText;
    [SerializeField] private AudioClip deploySound;
    [SerializeField] private AudioSource effects;
    private GameObject decoy;
    private int charges = 3;
    private float expiresAt, readyAt;
    public static DecoyAbility Instance { get; private set; }
    public bool IsActive => decoy != null && Time.time < expiresAt;
    public Vector2 Target => decoy.transform.position;
    public int Charges => charges;

    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }
    private void Update()
    {
        var manager = GameManager.Instance;
        if (manager == null) return;
        if (!manager.ActorsCanMove) ClearDecoy();
        else
        {
            if (decoy != null && Time.time >= expiresAt) ClearDecoy();
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) TryDeploy();
            if (decoy != null) decoy.transform.localScale = Vector3.one * (1.3f + .15f * Mathf.Sin(Time.time * 8));
        }
        if (statusText != null)
        {
            string state = IsActive ? "ACTIVE" : Time.time < readyAt ? Mathf.CeilToInt(readyAt - Time.time) + "s" : "READY";
            statusText.text = "SPACE: DECOY " + charges + " / 3  " + state;
        }
    }
    public bool TryDeploy()
    {
        if (GameManager.Instance == null || !GameManager.Instance.ActorsCanMove || charges <= 0 || Time.time < readyAt) return false;
        ClearDecoy();charges--;readyAt=Time.time+6;expiresAt=Time.time+4;
        decoy=new GameObject("BeetleDecoy");decoy.transform.SetParent(transform,false);
        decoy.transform.position=LevelGenerator.Instance.CellToWorld(GameManager.Instance.Player.CurrentCell,-.3f);
        var renderer=decoy.AddComponent<SpriteRenderer>();renderer.sprite=decoySprite;renderer.color=new Color(1,.8f,.25f,.8f);renderer.sortingOrder=25;
        if(effects!=null && deploySound!=null)effects.PlayOneShot(deploySound);
        return true;
    }
    public void RefillFromCherry() => charges = Mathf.Min(3, charges + 1);
    public void ResetRound() { ClearDecoy();charges=3;readyAt=0; }
    private void ClearDecoy() { if(decoy!=null)Destroy(decoy);decoy=null; }
}
