using UnityEngine;
using UnityEngine.Audio;
using Utility;

public class SoundManager : Singleton<SoundManager>
{
    [Header(":: BGM")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioClip bgm;
    [SerializeField] private AudioMixerGroup bgmGroup;

    [Header(":: SFX")]
    [SerializeField] private AudioSource effectSource;
    [SerializeField] private SerializableDic<string, AudioClip> sounds;
    [SerializeField] private AudioMixerGroup sfxGroup;
    
    // 사운드 재생할 오브젝트 생성 ->
    // 사운드 재생 후 삭제, or 오브젝트 풀
    // 오디오 믹서

    private void Awake()
    {
        base.Awake();
        Initialize();
    }

    private void Initialize()
    {
        // BGM AudioSource 설정
        if(bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.outputAudioMixerGroup = bgmGroup;
            bgmSource.loop = true;
        }

        if(effectSource == null)
        {
            effectSource = gameObject.AddComponent<AudioSource>();
            effectSource.outputAudioMixerGroup = sfxGroup;
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
            $"[SoundManager] BGM 클립이 연결되지 않았습니다.".Log();
            return;
        }

        bgmSource.clip = bgm;
        bgmSource.Play();
    }

    /// <summary>
    /// 이펙트 사운드 효과 재생
    /// </summary>
    public void PlaySound(string key)
    {
        if (sounds.TryGetValue(key, out AudioClip clip))
        {
            effectSource.PlayOneShot(clip);
        }
        else
        {
            $"[SoundManager] '{key}' 키를 찾을 수 없습니다.".Log();
            return;
        }
    }

    // public void PlayClick()     => PlayEffectSound(click);
    // public void PlayPlacement() => PlayEffectSound(placement);
}
