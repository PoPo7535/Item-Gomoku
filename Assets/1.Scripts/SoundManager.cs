using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using Utility;

public class SoundManager : Singleton<SoundManager>
{
    private const float DEFAULT_VOLUME = 50f;

    public float CurrentMasterVolume { get; private set; } = DEFAULT_VOLUME;
    public float CurrentBGMVolume { get; private set; } = DEFAULT_VOLUME;
    public float CurrentSFXVolume { get; private set; } = DEFAULT_VOLUME;

    [Header(":: BGM")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private SerializableDic<string, AudioClip> bgms;
    [SerializeField] private AudioMixerGroup bgmGroup;

    private List<string> _bgmKeys;
    private int _currentBGMIndex = 0;

    [Header(":: SFX")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private SerializableDic<string, AudioClip> sounds;
    [SerializeField] private AudioMixerGroup sfxGroup;

    [Header(":: Mixer")]
    [SerializeField] private AudioMixer audioMixer;
    private const string PARAM_MASTER = "Master";
    private const string PARAM_BGM = "BGM";
    private const string PARAM_SFX = "SFX";

    // 타이머 사운드 전용 오디오 소스 변수 추가
    private AudioSource timerSource;
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

        // 타이머용 오디오 소스 생성 및 셋팅 (SFX 그룹에 연결, 무한 반복 On)
        if(timerSource == null)
        {
            timerSource = gameObject.AddComponent<AudioSource>();
            timerSource.outputAudioMixerGroup = sfxGroup; 
            timerSource.loop = true; // 무한 반복
        }
        _bgmKeys = bgms.Keys.ToList();

        SetMasterVolume(DEFAULT_VOLUME);
        SetBGMVolume(DEFAULT_VOLUME);
        SetSFXVolume(DEFAULT_VOLUME);

        PlayBGM();
    }

    /// <summary>
    /// Master/BGM/SFX 볼륨 조절
    /// </summary>
    private void SetMasterVolume(float value)
    {
        CurrentMasterVolume = value;

        float db = (value / 100f) > 0f ? Mathf.Log10((value / 100f)) * 20f : -80f;
        audioMixer.SetFloat(PARAM_MASTER, db);
    }

    private void SetBGMVolume(float value)
    {
        CurrentBGMVolume = value;

        float db = (value / 100f) > 0f ? Mathf.Log10((value / 100f)) * 20f : -80f;
        audioMixer.SetFloat(PARAM_BGM, db);
    }

    private void SetSFXVolume(float value)
    {
        CurrentSFXVolume = value;

        float db = (value / 100f) > 0f ? Mathf.Log10((value / 100f)) * 20f : -80f;
        audioMixer.SetFloat(PARAM_SFX, db);
    }

    // ── BGM ────────────────────────────────────────────────────────────────────────────────────

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

        if (bgms.TryGetValue(_bgmKeys[_currentBGMIndex], out AudioClip clip))
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
        _currentBGMIndex = (_currentBGMIndex + 1) % _bgmKeys.Count;
        PlayBGM();
    }

    /// <summary>
    /// 이전 BGM으로 전환
    /// </summary>
    public void PlayPrevBGM()
    {
        _currentBGMIndex = (_currentBGMIndex - 1) % _bgmKeys.Count;
        if(_currentBGMIndex < 0) _currentBGMIndex = _bgmKeys.Count - 1;
        PlayBGM();
    }

    /// <summary>
    /// 현재 재생중인 BGM 제목 Key 반환
    /// </summary>
    public string CurrentBGMTitle()
    {
        if (_bgmKeys == null || _bgmKeys.Count == 0) return string.Empty;
        return _bgmKeys[_currentBGMIndex];
    }

    // ── SFX ────────────────────────────────────────────────────────────────────────────────────

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

    /// <summary>
    /// 타이머 소리 무한 반복 재생 시작
    /// </summary>
    public void PlayTimerSound()
    {
        if (sounds.TryGetValue("timer", out AudioClip clip))
        {
            timerSource.clip = clip;
            // 이미 재생 중이 아닐 때만 재생
            if (!timerSource.isPlaying) 
            {
                timerSource.Play();
            }
        }
    }

    /// <summary>
    /// 타이머 소리 정지
    /// </summary>
    public void StopTimerSound()
    {
        if (timerSource != null && timerSource.isPlaying)
        {
            timerSource.Stop();
        }
    }
}
