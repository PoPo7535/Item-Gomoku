using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using Utility;

public class SoundManager : Singleton<SoundManager>
{
    private const float DEFAULT_VOLUME = 0.5f;

    [Header(":: BGM")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private SerializableDic<string, AudioClip> bgms;
    [SerializeField] private AudioMixerGroup bgmGroup;

    private List<string> _bgmKeys;
    [SerializeField] private int currentIndex = 0;

    [Header(":: SFX")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private SerializableDic<string, AudioClip> sounds;
    [SerializeField] private AudioMixerGroup sfxGroup;

    // 사운드 재생할 오브젝트 생성 ->
    // 사운드 재생 후 삭제, or 오브젝트 풀
    // 오디오 믹서
    [Header(":: Mixer")]
    [SerializeField] private AudioMixer audioMixer;
    private const string PARAM_MASTER = "Master";
    private const string PARAM_BGM = "BGM";
    private const string PARAM_SFX = "SFX";

    private void Awake()
    {
        if (FindObjectsOfType<SoundManager>().Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        base.Awake();
        Initialize();
    }

    private void OnEnable()
    {
        Options.MasterVolume += SetMasterVolume;
        Options.BGMVolume += SetBGMVolume;
        Options.SFXVolume += SetSFXVolume;

        SetMasterVolume(DEFAULT_VOLUME);
        SetBGMVolume(DEFAULT_VOLUME);
        SetSFXVolume(DEFAULT_VOLUME);
    }

    private void OnDisable()
    {
        Options.MasterVolume -= SetMasterVolume;
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

        _bgmKeys = bgms.Keys.ToList();
        PlayBGM();
    }

    /// <summary>
    /// Master/BGM/SFX 볼륨 조절
    /// </summary>
    private void SetMasterVolume(float value)
    {
        float db = value > 0f ? Mathf.Log10(value) * 20f : -80f;
        audioMixer.SetFloat(PARAM_MASTER, db);
    }

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
        if(_bgmKeys == null || _bgmKeys.Count == 0)
        {
            $"[SoundManager] BGM 클립이 연결되지 않았습니다.".Log();
            return;
        }

        // if (bgmSource.isPlaying) return;

        if (bgms.TryGetValue(_bgmKeys[currentIndex], out AudioClip clip))
        {
            bgmSource.clip = clip;
            bgmSource.Play();
        }
    }

    /// <summary>
    /// 다음 BGM으로 전환
    /// </summary>
    public void PlayNextBGM()
    {
        currentIndex = (currentIndex + 1) % _bgmKeys.Count;
        PlayBGM();
    }

    /// <summary>
    /// 이전 BGM으로 전환
    /// </summary>
    public void PlayPrebBGM()
    {
        currentIndex = (currentIndex - 1) % _bgmKeys.Count;
        if(currentIndex < 0) currentIndex = _bgmKeys.Count - 1;
        PlayBGM();
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
