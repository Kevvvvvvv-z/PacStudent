using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    [SerializeField] private AudioClip introBGM;
    [SerializeField] private AudioClip ghostNormalBGM;

    private AudioSource musicSource;
    private float introEndTime;
    private bool introPlaying;

    private void Start()
    {
        musicSource = GetComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;

        if (ghostNormalBGM == null)
        {
            Debug.LogError("Assign GhostNormalBGM on AudioManager.", this);
            return;
        }

        if (introBGM == null)
        {
            PlayNormalMusic();
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
        // Switch on the first frame at the deadline, rather than counting frames.
        if (introPlaying && Time.time >= introEndTime)
        {
            PlayNormalMusic();
        }
    }

    private void PlayNormalMusic()
    {
        introPlaying = false;
        musicSource.Stop();
        musicSource.clip = ghostNormalBGM;
        musicSource.loop = true;
        musicSource.Play();
    }
}
