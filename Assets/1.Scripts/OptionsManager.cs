using UnityEngine;
using UnityEngine.UI;
using Utility;

public class OptionsManager : Singleton<OptionsManager>
{
    private const float DEFAULT_VOLUME = 0.5f;

    [Header(":: BGM")]
    [SerializeField] private Slider bgmSlider;

    [Header(":: SFX")]
    [SerializeField] private Slider sfxSlider;

    private void Awake()
    {
        //if (FindObjectsOfType<OptionsManager>().Length > 1)
        //{
        //    Destroy(gameObject);
        //    return;
        //}

        base.Awake();
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        bgmSlider.value = DEFAULT_VOLUME;
        sfxSlider.value = DEFAULT_VOLUME;

        Options.OnBGMVolumeChanged(DEFAULT_VOLUME);
        Options.OnSFXVolumeChanged(DEFAULT_VOLUME);

        bgmSlider.onValueChanged.AddListener(OnBGMChanged);
        sfxSlider.onValueChanged.AddListener(OnSFXChanged);

    }

    private void OnDestroy()
    {
        bgmSlider.onValueChanged.RemoveListener(OnBGMChanged);
        sfxSlider.onValueChanged.RemoveListener(OnSFXChanged);
    }

    private void OnBGMChanged(float value)
    {
        Options.OnBGMVolumeChanged(value);
    }

    private void OnSFXChanged(float value)
    {
        Options.OnSFXVolumeChanged(value);
    }


}
