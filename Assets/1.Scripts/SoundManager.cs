using UnityEngine;

public class SoundManager : MonoBehaviour
{
    private static SoundManager _instance;
    public static SoundManager Instance
    {
        get
        {
            if(_instance == null)
            {
                GameObject SoundManager = new GameObject("SoundManager");
                _instance = SoundManager.AddComponent<SoundManager>();
                DontDestroyOnLoad(SoundManager);
            }
            return _instance;
        }
    }

    [Header("BGM")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioClip bgm;

    [Header("사운드 이펙트")]
    [SerializeField] private AudioSource effectSource;
    [SerializeField] private AudioClip click;
    [SerializeField] private AudioClip placement;

    private void Awake()
    {
        if(_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeBGM();
    }

    private void InitializeBGM()
    {
        // BGM AudioSource 설정
        if(bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
        }

        if(effectSource == null)
        {
            effectSource = gameObject.AddComponent<AudioSource>();
        }

        PlayBGM();
    }

    /// <summary>
    /// 배경음악 재생
    /// </summary>
    private void PlayBGM()
    {
        if(bgm == null)
        {
            Debug.Log("[SoundManager] BGM 클립이 연결되지 않았습니다.");
            return;
        }

        bgmSource.clip = bgm;
        bgmSource.Play();
    }

    /// <summary>
    /// 이펙트 사운드 효과 재생
    /// </summary>
    public void PlayEffectSound(AudioClip clip)
    {
        if(clip == null)
        {
            Debug.Log("[SoundManager] Clip 클립이 연결되지 않았습니다.");
            return;
        }

        effectSource.PlayOneShot(clip);
    }

    public void PlayClick()     => PlayEffectSound(click);
    public void PlayPlacement() => PlayEffectSound(placement);
}
