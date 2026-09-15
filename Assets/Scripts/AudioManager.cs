using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    public enum GhostMusicMode
    {
        Normal,
        Scared,
        Dead
    }

    [Header("Music")]
    [SerializeField] private AudioClip introBGM;
    [SerializeField] private AudioClip ghostNormalBGM;
    [SerializeField] private AudioClip ghostScaredBGM;
    [SerializeField] private AudioClip ghostDeadBGM;

    [Header("Sound effects")]
    [SerializeField] private AudioClip eatPelletSFX;
    [SerializeField] private AudioClip eatGhostSFX;
    [SerializeField] private AudioClip eatBonusFruitSFX;
    [SerializeField] private AudioClip hitWallSFX;
    [SerializeField] private AudioClip deathSFX;

    private AudioSource musicSource;
    private AudioSource soundEffectSource;
    private float introEndTime;
    private bool introPlaying;
    private GhostMusicMode requestedMode;
    private GhostMusicMode playingMode;

    public static AudioManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        musicSource = GetComponent<AudioSource>();
        soundEffectSource = gameObject.AddComponent<AudioSource>();
        soundEffectSource.playOnAwake = false;
        soundEffectSource.loop = false;
        soundEffectSource.spatialBlend = 0f;
        soundEffectSource.volume = 0.75f;
    }

    private void Start()
    {
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;
        requestedMode = GhostMusicMode.Normal;

        if (ghostNormalBGM == null)
        {
            Debug.LogError("Assign GhostNormalBGM on AudioManager.", this);
            return;
        }

        if (introBGM == null)
        {
            PlayRequestedMusic();
            return;
        }

        musicSource.clip = introBGM;
        musicSource.loop = false;
        musicSource.Play();
        introEndTime = Time.time + Mathf.Min(introBGM.length, 3f);
        introPlaying = true;
    }

    private void Update()
    {
        if (introPlaying && Time.time >= introEndTime)
        {
            introPlaying = false;
            PlayRequestedMusic();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Configure(
        AudioClip intro,
        AudioClip normal,
        AudioClip scared,
        AudioClip dead,
        AudioClip pellet,
        AudioClip ghostEaten,
        AudioClip bonusFruit,
        AudioClip wallHit,
        AudioClip death)
    {
        introBGM = intro;
        ghostNormalBGM = normal;
        ghostScaredBGM = scared;
        ghostDeadBGM = dead;
        eatPelletSFX = pellet;
        eatGhostSFX = ghostEaten;
        eatBonusFruitSFX = bonusFruit;
        hitWallSFX = wallHit;
        deathSFX = death;
    }

    public void SetGhostMode(GhostMusicMode mode)
    {
        requestedMode = mode;
        if (!introPlaying && requestedMode != playingMode)
        {
            PlayRequestedMusic();
        }
    }

    public void PlayPellet() => PlaySound(eatPelletSFX);
    public void PlayGhostEaten() => PlaySound(eatGhostSFX);
    public void PlayBonusFruit() => PlaySound(eatBonusFruitSFX);
    public void PlayWallHit() => PlaySound(hitWallSFX);
    public void PlayDeath() => PlaySound(deathSFX);

    private void PlayRequestedMusic()
    {
        AudioClip requestedClip = requestedMode switch
        {
            GhostMusicMode.Scared => ghostScaredBGM,
            GhostMusicMode.Dead => ghostDeadBGM,
            _ => ghostNormalBGM
        };

        if (requestedClip == null)
        {
            requestedClip = ghostNormalBGM;
        }

        if (requestedClip == null)
        {
            return;
        }

        playingMode = requestedMode;
        musicSource.Stop();
        musicSource.clip = requestedClip;
        musicSource.loop = true;
        musicSource.Play();
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && soundEffectSource != null)
        {
            soundEffectSource.PlayOneShot(clip);
        }
    }
}
