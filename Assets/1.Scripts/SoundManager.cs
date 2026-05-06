using UnityEngine;
using UnityEngine.Audio;
using Utility;

public class SoundManager : Singleton<SoundManager>
{
    private const float DEFAULT_VOLUME = 0.5f;

    [Header(":: BGM")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioClip bgm;
    [SerializeField] private AudioMixerGroup bgmGroup;

    [Header(":: SFX")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private SerializableDic<string, AudioClip> sounds;
    [SerializeField] private AudioMixerGroup sfxGroup;

    // 사운드 재생할 오브젝트 생성 ->
    // 사운드 재생 후 삭제, or 오브젝트 풀
    // 오디오 믹서
    [Header(":: Mixer")]
    [SerializeField] private AudioMixer audioMixer;
    private const string PARAM_BGM = "BGM";
    private const string PARAM_SFX = "SFX";

    private void Awake()
    {
        //if (FindObjectsOfType<SoundManager>().Length > 1)
        //{
        //    Destroy(gameObject);
        //    return;
        //}

        base.Awake();
        Initialize();
    }

    private void OnEnable()
    {
        Options.BGMVolume += SetBGMVolume;
        Options.SFXVolume += SetSFXVolume;

        SetBGMVolume(DEFAULT_VOLUME);
        SetSFXVolume(DEFAULT_VOLUME);
    }

    private void OnDisable()
    {
        Options.BGMVolume -= SetBGMVolume;
        Options.SFXVolume -= SetSFXVolume;
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

        if(sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.outputAudioMixerGroup = sfxGroup;
        }

        PlayBGM();
    }

    /// <summary>
    /// BGM/SFX 볼륨 조절
    /// </summary>
    private void SetBGMVolume(float value)
    {
        float db = value > 0f ? Mathf.Log10(value) * 20f : -80f;
        audioMixer.SetFloat(PARAM_BGM, db);
    }

    private void SetSFXVolume(float value)
    {
        float db = value > 0f ? Mathf.Log10(value) * 20f : -80f;
        audioMixer.SetFloat(PARAM_SFX, db);
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

        if (bgmSource.isPlaying) return;

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
            sfxSource.PlayOneShot(clip);
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
