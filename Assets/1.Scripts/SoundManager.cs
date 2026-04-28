using UnityEngine;
using Utility;

public class SoundManager : Singleton<SoundManager>
{
    [Header("BGM")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioClip bgm;

    [Header("사운드 이펙트")]
    [SerializeField] private AudioSource effectSource;
    [SerializeField] private AudioClip click;
    [SerializeField] private AudioClip placement;

    [SerializeField] private SerializableDic<string, AudioClip> sounds;
    
    // 사운드 재생할 오브젝트 생성 ->
    // 사운드 재생 후 삭제, or 오브젝트 풀
    // 오디어 믹서
    
    public void PlaySound(string key)
    {
        if (sounds.TryGetValue(key, out AudioClip clip))
        {
            PlayEffectSound(clip);
        }
    }
    private void Awake()
    {
        base.Awake();
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
    
    // public void PlayClick()     => PlayEffectSound(click);
    // public void PlayPlacement() => PlayEffectSound(placement);
}
